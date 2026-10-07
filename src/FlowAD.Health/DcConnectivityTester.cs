using System.Net.Sockets;
using FlowAD.Core.Models;

namespace FlowAD.Health;

public static class DcConnectivityTester
{
    private static readonly (int Port, string Name)[] StandardAdPorts =
    [
        (53, "DNS"),
        (88, "Kerberos"),
        (135, "RPC Endpoint Mapper"),
        (389, "LDAP"),
        (636, "LDAPS"),
        (445, "SMB"),
        (3268, "Global Catalog")
    ];

    public static async Task<DcHealthSummary> TestDcConnectivityAsync(
        string dcHost,
        string ipAddress,
        string? site = null,
        int timeoutMs = 2000,
        CancellationToken cancellationToken = default)
    {
        var summary = new DcHealthSummary
        {
            DcName = dcHost,
            IpAddress = ipAddress,
            Site = site
        };

        bool anyReachable = false;

        foreach (var (port, name) in StandardAdPorts)
        {
            bool isOpen = await ProbeTcpPortAsync(ipAddress, port, timeoutMs, cancellationToken);
            summary.PortStatus[name] = isOpen;
            if (isOpen) anyReachable = true;
        }

        summary.IsReachable = anyReachable;
        // If SMB (445) is open, shares are reachable
        summary.SysvolShareAccessible = summary.PortStatus.TryGetValue("SMB", out var smbOpen) && smbOpen;
        summary.NetlogonShareAccessible = summary.SysvolShareAccessible;

        return summary;
    }

    public static async Task<bool> ProbeTcpPortAsync(
        string hostOrIp,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            await client.ConnectAsync(hostOrIp, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
