using System.Net;
using FlowAD.Core.Models;

namespace FlowAD.Health;

public static class DomainHealthService
{
    public static async Task<DomainHealthReport> AuditDomainHealthAsync(
        string domainName,
        IEnumerable<(string Host, string Ip)> domainControllers,
        NetworkCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        var report = new DomainHealthReport
        {
            DomainName = domainName
        };

        var dcList = domainControllers.ToList();

        // If no DCs provided, attempt direct connection using domain name as host
        if (dcList.Count == 0)
        {
            try
            {
                var ips = await Dns.GetHostAddressesAsync(domainName, cancellationToken);
                foreach (var ip in ips)
                {
                    dcList.Add((domainName, ip.ToString()));
                }
            }
            catch
            {
                dcList.Add((domainName, domainName));
            }
        }

        // 1. Probe DC Connectivity & Port Availability
        foreach (var (host, ip) in dcList)
        {
            var summary = await DcConnectivityTester.TestDcConnectivityAsync(host, ip, timeoutMs: 2500, cancellationToken: cancellationToken);
            
            // Measure clock skew via LDAP if LDAP port is open
            if (summary.PortStatus.TryGetValue("LDAP", out var ldapOpen) && ldapOpen)
            {
                summary.ClockSkewSeconds = await ClockSkewCalculator.MeasureLdapClockSkewAsync(ip, credential, cancellationToken);
            }

            report.Dcs.Add(summary);
        }

        // 2. Inspect FSMO Roles using first reachable DC
        var reachableDc = report.Dcs.FirstOrDefault(d => d.IsReachable);
        if (reachableDc != null)
        {
            report.FsmoRoles = await FsmoRoleInspector.InspectFsmoRolesAsync(reachableDc.IpAddress, credential, cancellationToken);
        }
        else
        {
            report.FsmoRoles = new FsmoRolesReport
            {
                IsConsensusReached = false,
                UnreachableRoleHolders = ["All Domain Controllers Unreachable"]
            };
        }

        return report;
    }
}
