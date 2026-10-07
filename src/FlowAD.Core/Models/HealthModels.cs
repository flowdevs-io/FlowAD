namespace FlowAD.Core.Models;

public sealed class DcHealthSummary
{
    public string DcName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? Site { get; set; }
    public bool IsReachable { get; set; }
    public Dictionary<string, bool> PortStatus { get; set; } = [];
    public double ClockSkewSeconds { get; set; }
    public bool SysvolShareAccessible { get; set; }
    public bool NetlogonShareAccessible { get; set; }
    public CheckStatus OverallStatus
    {
        get
        {
            if (!IsReachable || Math.Abs(ClockSkewSeconds) > 300 || PortStatus.Values.Any(open => !open))
                return CheckStatus.Fail;
            if (Math.Abs(ClockSkewSeconds) > 3 || !SysvolShareAccessible || !NetlogonShareAccessible)
                return CheckStatus.Warning;
            return CheckStatus.Pass;
        }
    }
}

public sealed class FsmoRolesReport
{
    public string SchemaMaster { get; set; } = string.Empty;
    public string DomainNamingMaster { get; set; } = string.Empty;
    public string PdcEmulator { get; set; } = string.Empty;
    public string RidMaster { get; set; } = string.Empty;
    public string InfrastructureMaster { get; set; } = string.Empty;
    public bool IsConsensusReached { get; set; } = true;
    public List<string> UnreachableRoleHolders { get; set; } = [];
    public CheckStatus OverallStatus => !IsConsensusReached || UnreachableRoleHolders.Count > 0
        ? CheckStatus.Fail
        : CheckStatus.Pass;
}

public sealed class ReplicationPartner
{
    public string SourceDc { get; set; } = string.Empty;
    public string TargetDc { get; set; } = string.Empty;
    public string NamingContext { get; set; } = string.Empty;
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int LastResultCode { get; set; }
    public CheckStatus Status => ConsecutiveFailures > 0 || LastResultCode != 0
        ? CheckStatus.Fail
        : CheckStatus.Pass;
}

public sealed class DomainHealthReport
{
    public string DomainName { get; set; } = string.Empty;
    public List<DcHealthSummary> Dcs { get; set; } = [];
    public FsmoRolesReport FsmoRoles { get; set; } = new();
    public List<ReplicationPartner> ReplicationPartners { get; set; } = [];
    public int HealthScore
    {
        get
        {
            int total = 100;
            if (FsmoRoles.OverallStatus == CheckStatus.Fail) total -= 25;
            foreach (var dc in Dcs)
            {
                if (dc.OverallStatus == CheckStatus.Fail) total -= 20;
                else if (dc.OverallStatus == CheckStatus.Warning) total -= 5;
            }
            foreach (var repl in ReplicationPartners)
            {
                if (repl.Status == CheckStatus.Fail) total -= 10;
            }
            return Math.Clamp(total, 0, 100);
        }
    }

    private CheckStatus? _explicitStatus;
    public CheckStatus OverallStatus
    {
        get => _explicitStatus ?? (HealthScore >= 85 ? CheckStatus.Pass : (HealthScore >= 60 ? CheckStatus.Warning : CheckStatus.Fail));
        set => _explicitStatus = value;
    }
}
