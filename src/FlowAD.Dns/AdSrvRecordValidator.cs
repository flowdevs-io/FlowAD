using System.Diagnostics;
using System.Net;
using DnsClient;
using FlowAD.Core.Models;

namespace FlowAD.Dns;

public static class AdSrvRecordValidator
{
    private static readonly (string SrvSuffix, string Purpose)[] CriticalSrvRecords =
    [
        ("_ldap._tcp.dc._msdcs", "Active Directory Domain Controller Locator"),
        ("_kerberos._tcp.dc._msdcs", "Kerberos Key Distribution Center (KDC) Locator"),
        ("_kpasswd._tcp", "Kerberos Password Change Service"),
        ("_gc._tcp", "Active Directory Global Catalog Service"),
        ("_ldap._tcp.pdc._msdcs", "Primary Domain Controller (PDC) Emulator Locator")
    ];

    public static async Task<DnsAuditReport> AuditDomainDnsAsync(
        string domainName,
        IEnumerable<string>? customDnsServers = null,
        CancellationToken cancellationToken = default)
    {
        var report = new DnsAuditReport
        {
            DomainName = domainName
        };

        var dnsServers = new List<IPAddress>();
        if (customDnsServers != null)
        {
            foreach (var server in customDnsServers)
            {
                if (IPAddress.TryParse(server, out var ip))
                {
                    dnsServers.Add(ip);
                    report.DnsServersQueried.Add(ip.ToString());
                }
            }
        }

        ILookupClient client = dnsServers.Count > 0
            ? new LookupClient(dnsServers.ToArray())
            : new LookupClient();

        if (report.DnsServersQueried.Count == 0)
        {
            foreach (var endpoint in client.NameServers)
            {
                report.DnsServersQueried.Add(endpoint.ToString());
            }
        }

        var totalLatency = 0.0;
        var srvCount = 0;
        var discoveredDcHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Audit Critical SRV Records
        foreach (var (suffix, purpose) in CriticalSrvRecords)
        {
            var recordFqdn = $"{suffix}.{domainName.TrimEnd('.')}.";
            var check = new AdSrvRecordCheck
            {
                RecordName = recordFqdn,
                Purpose = purpose
            };

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.QueryAsync(recordFqdn, QueryType.SRV, cancellationToken: cancellationToken);
                sw.Stop();
                check.LatencyMs = sw.Elapsed.TotalMilliseconds;
                totalLatency += check.LatencyMs;
                srvCount++;

                var srvRecords = response.Answers.SrvRecords().ToList();
                if (srvRecords.Count == 0)
                {
                    check.Status = CheckStatus.Fail;
                    check.ErrorMessage = "No SRV records returned by DNS server.";
                }
                else
                {
                    foreach (var srv in srvRecords)
                    {
                        var targetHost = srv.Target.Value.TrimEnd('.');
                        discoveredDcHosts.Add(targetHost);

                        var endpointInfo = new DnsEndpointInfo
                        {
                            TargetHost = targetHost,
                            Port = srv.Port,
                            Priority = srv.Priority,
                            Weight = srv.Weight
                        };

                        // Resolve IP addresses for this target host
                        try
                        {
                            var aResponse = await client.QueryAsync(targetHost, QueryType.A, cancellationToken: cancellationToken);
                            endpointInfo.ResolvedIps.AddRange(aResponse.Answers.ARecords().Select(a => a.Address.ToString()));
                        }
                        catch
                        {
                            // IP resolution failure will be captured in consistency check
                        }

                        check.Endpoints.Add(endpointInfo);
                    }
                    check.Status = CheckStatus.Pass;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                check.LatencyMs = sw.Elapsed.TotalMilliseconds;
                check.Status = CheckStatus.Fail;
                check.ErrorMessage = ex.Message;
            }

            report.SrvRecordChecks.Add(check);
        }

        // 2. Perform Forward & Reverse (PTR) Consistency Check for discovered DCs
        foreach (var dcHost in discoveredDcHosts)
        {
            var forwardIps = new List<IPAddress>();
            try
            {
                var fwdResponse = await client.QueryAsync(dcHost, QueryType.A, cancellationToken: cancellationToken);
                forwardIps.AddRange(fwdResponse.Answers.ARecords().Select(a => a.Address));
            }
            catch
            {
                // Unresolvable forward record
            }

            if (forwardIps.Count == 0)
            {
                report.ConsistencyChecks.Add(new DcDnsConsistencyCheck
                {
                    DcHost = dcHost,
                    IpAddress = "Unresolved",
                    HasForwardRecord = false,
                    HasReversePtr = false,
                    Status = CheckStatus.Fail,
                    Notes = $"Forward A record for DC '{dcHost}' is missing."
                });
                continue;
            }

            foreach (var ip in forwardIps)
            {
                var consistency = new DcDnsConsistencyCheck
                {
                    DcHost = dcHost,
                    IpAddress = ip.ToString(),
                    HasForwardRecord = true
                };

                try
                {
                    var ptrResponse = await client.QueryReverseAsync(ip, cancellationToken: cancellationToken);
                    var ptrRecord = ptrResponse.Answers.PtrRecords().FirstOrDefault();

                    if (ptrRecord != null)
                    {
                        consistency.HasReversePtr = true;
                        consistency.PtrResolvedHost = ptrRecord.PtrDomainName.Value.TrimEnd('.');

                        if (string.Equals(consistency.PtrResolvedHost, dcHost, StringComparison.OrdinalIgnoreCase))
                        {
                            consistency.Status = CheckStatus.Pass;
                            consistency.Notes = "Forward and Reverse PTR match perfectly.";
                        }
                        else
                        {
                            consistency.Status = CheckStatus.Warning;
                            consistency.Notes = $"PTR mismatch: IP resolves to '{consistency.PtrResolvedHost}' instead of '{dcHost}'.";
                        }
                    }
                    else
                    {
                        consistency.HasReversePtr = false;
                        consistency.Status = CheckStatus.Warning;
                        consistency.Notes = $"Reverse DNS (PTR) record missing for IP {ip}. May degrade Kerberos authentication.";
                    }
                }
                catch (Exception ex)
                {
                    consistency.HasReversePtr = false;
                    consistency.Status = CheckStatus.Warning;
                    consistency.Notes = $"PTR lookup failed: {ex.Message}";
                }

                report.ConsistencyChecks.Add(consistency);
            }
        }

        report.AverageLatencyMs = srvCount > 0 ? totalLatency / srvCount : 0.0;
        return report;
    }
}
