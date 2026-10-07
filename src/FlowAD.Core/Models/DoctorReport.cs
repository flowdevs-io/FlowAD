namespace FlowAD.Core.Models;

public sealed class DoctorReport
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string DomainName { get; set; } = string.Empty;
    public GpoAuditReport GpoReport { get; set; } = new();
    public DnsAuditReport DnsReport { get; set; } = new();
    public DomainHealthReport HealthReport { get; set; } = new();

    public int OverallScore
    {
        get
        {
            int score = HealthReport.HealthScore;
            if (GpoReport.OverallStatus == CheckStatus.Fail) score -= 20;
            else if (GpoReport.OverallStatus == CheckStatus.Warning) score -= 5;

            if (DnsReport.OverallStatus == CheckStatus.Fail) score -= 20;
            else if (DnsReport.OverallStatus == CheckStatus.Warning) score -= 5;

            return Math.Clamp(score, 0, 100);
        }
    }

    public CheckStatus OverallStatus => OverallScore >= 80 ? CheckStatus.Pass : (OverallScore >= 55 ? CheckStatus.Warning : CheckStatus.Fail);
}
