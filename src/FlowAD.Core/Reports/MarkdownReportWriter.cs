using System.Text;
using FlowAD.Core.Models;

namespace FlowAD.Core.Reports;

public static class MarkdownReportWriter
{
    public static string GenerateDoctorReport(DoctorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# FlowAD Domain Health & Audit Report");
        sb.AppendLine($"**Domain:** `{report.DomainName}` | **Generated:** {report.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Overall Score:** {report.OverallScore}/100 (**{report.OverallStatus}**)");
        sb.AppendLine();

        sb.AppendLine("## 1. Group Policy (GPO) Audit & Deconfliction");
        sb.AppendLine($"- **Total GPOs Analyzed:** {report.GpoReport.TotalGpos}");
        sb.AppendLine($"- **Unlinked GPOs:** {report.GpoReport.UnlinkedGpos.Count}");
        sb.AppendLine($"- **AD/SYSVOL Version Mismatches:** {report.GpoReport.VersionMismatches.Count}");
        sb.AppendLine($"- **Empty GPOs:** {report.GpoReport.EmptyGpos.Count}");
        sb.AppendLine($"- **Detected Conflicts:** {report.GpoReport.Conflicts.Count}");
        sb.AppendLine();

        if (report.GpoReport.Conflicts.Count > 0)
        {
            sb.AppendLine("### Detected GPO Conflicts");
            sb.AppendLine("| Scope | Setting | Type | Winning GPO (Value) | Losing GPO (Value) | Severity |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");
            foreach (var c in report.GpoReport.Conflicts)
            {
                sb.AppendLine($"| `{c.TargetScope}` | `{c.SettingIdentifier}` | {c.ConflictType} | **{c.WinningGpo}** (`{c.WinningValue}`) | {c.LosingGpo} (`{c.LosingValue}`) | **{c.Severity}** |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 2. Active Directory DNS Health");
        sb.AppendLine($"- **Overall Status:** {report.DnsReport.OverallStatus}");
        sb.AppendLine($"- **Average Resolution Latency:** {report.DnsReport.AverageLatencyMs:F1} ms");
        sb.AppendLine();
        sb.AppendLine("### Critical AD SRV Records");
        sb.AppendLine("| SRV Record | Purpose | Status | Target Endpoints | Latency |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
        foreach (var srv in report.DnsReport.SrvRecordChecks)
        {
            var targets = string.Join(", ", srv.Endpoints.Select(e => $"{e.TargetHost}:{e.Port}"));
            if (string.IsNullOrEmpty(targets)) targets = "None";
            sb.AppendLine($"| `{srv.RecordName}` | {srv.Purpose} | {srv.Status} | `{targets}` | {srv.LatencyMs:F1} ms |");
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Domain Controller Connectivity & Health");
        sb.AppendLine($"- **Health Score:** {report.HealthReport.HealthScore}/100");
        sb.AppendLine($"- **PDC Emulator:** `{report.HealthReport.FsmoRoles.PdcEmulator}`");
        sb.AppendLine($"- **FSMO Role Consensus:** {(report.HealthReport.FsmoRoles.IsConsensusReached ? "OK" : "SPLIT-BRAIN")}");
        sb.AppendLine();
        sb.AppendLine("### Domain Controllers");
        sb.AppendLine("| DC Host | IP | Reachable | Clock Skew | SYSVOL | Netlogon | Status |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
        foreach (var dc in report.HealthReport.Dcs)
        {
            sb.AppendLine($"| `{dc.DcName}` | {dc.IpAddress} | {(dc.IsReachable ? "Yes" : "No")} | {dc.ClockSkewSeconds:F2}s | {(dc.SysvolShareAccessible ? "OK" : "FAIL")} | {(dc.NetlogonShareAccessible ? "OK" : "FAIL")} | **{dc.OverallStatus}** |");
        }
        sb.AppendLine();

        return sb.ToString();
    }
}
