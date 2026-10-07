namespace FlowAD.Core.Models;

public sealed class DnsEndpointInfo
{
    public string TargetHost { get; set; } = string.Empty;
    public int Port { get; set; }
    public int Priority { get; set; }
    public int Weight { get; set; }
    public List<string> ResolvedIps { get; set; } = [];
}

public sealed class AdSrvRecordCheck
{
    public string RecordName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public CheckStatus Status { get; set; } = CheckStatus.Pass;
    public List<DnsEndpointInfo> Endpoints { get; set; } = [];
    public double LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class DcDnsConsistencyCheck
{
    public string DcHost { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool HasForwardRecord { get; set; }
    public bool HasReversePtr { get; set; }
    public string? PtrResolvedHost { get; set; }
    public CheckStatus Status { get; set; } = CheckStatus.Pass;
    public string Notes { get; set; } = string.Empty;
}

public sealed class DnsAuditReport
{
    public string DomainName { get; set; } = string.Empty;
    public List<string> DnsServersQueried { get; set; } = [];
    public List<AdSrvRecordCheck> SrvRecordChecks { get; set; } = [];
    public List<DcDnsConsistencyCheck> ConsistencyChecks { get; set; } = [];
    public double AverageLatencyMs { get; set; }
    private CheckStatus? _explicitStatus;
    public CheckStatus OverallStatus
    {
        get => _explicitStatus ?? (SrvRecordChecks.Any(s => s.Status == CheckStatus.Fail) || ConsistencyChecks.Any(c => c.Status == CheckStatus.Fail)
            ? CheckStatus.Fail
            : (SrvRecordChecks.Any(s => s.Status == CheckStatus.Warning) || ConsistencyChecks.Any(c => c.Status == CheckStatus.Warning)
                ? CheckStatus.Warning
                : CheckStatus.Pass));
        set => _explicitStatus = value;
    }
}
