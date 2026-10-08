using System.Net;
using System.Text;
using FlowAD.Core.Models;

namespace FlowAD.Core.Reports;

public static class HtmlReportWriter
{
    private const string BaseStyles = @"
    :root {
      --bg: #0f172a;
      --card-bg: #1e293b;
      --border: #334155;
      --text: #f8fafc;
      --text-muted: #94a3b8;
      --cyan: #38bdf8;
      --green: #4ade80;
      --amber: #fbbf24;
      --red: #f87171;
      --purple: #c084fc;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
    body { background: var(--bg); color: var(--text); padding: 2rem; line-height: 1.5; }
    .container { max-width: 1350px; margin: 0 auto; }
    header { margin-bottom: 2rem; border-bottom: 1px solid var(--border); padding-bottom: 1.5rem; display: flex; justify-content: space-between; align-items: flex-start; }
    h1 { font-size: 1.8rem; color: var(--cyan); display: flex; align-items: center; gap: 0.5rem; }
    .subtitle { color: var(--text-muted); font-size: 0.95rem; margin-top: 0.25rem; }
    .header-actions { display: flex; gap: 0.75rem; align-items: center; }
    .btn-print { background: #2563eb; color: #fff; border: none; padding: 0.55rem 1.1rem; border-radius: 6px; font-weight: 600; font-size: 0.9rem; cursor: pointer; display: flex; align-items: center; gap: 0.5rem; transition: background 0.2s; }
    .btn-print:hover { background: #1d4ed8; }
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
    .kpi-card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 8px; padding: 1.25rem; }
    .kpi-title { font-size: 0.82rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.05em; }
    .kpi-value { font-size: 1.8rem; font-weight: 700; margin-top: 0.25rem; }
    .card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 8px; padding: 1.5rem; margin-bottom: 2rem; }
    .card-title { font-size: 1.2rem; font-weight: 600; margin-bottom: 1rem; color: var(--text); display: flex; justify-content: space-between; align-items: center; }
    .filter-bar { display: flex; gap: 0.75rem; margin-bottom: 1rem; align-items: center; flex-wrap: wrap; }
    .search-input { background: #0f172a; border: 1px solid var(--border); color: #fff; padding: 0.5rem 0.85rem; border-radius: 6px; font-size: 0.9rem; flex-grow: 1; max-width: 350px; }
    .pill-btn { background: #0f172a; border: 1px solid var(--border); color: var(--text-muted); padding: 0.4rem 0.85rem; border-radius: 20px; font-size: 0.8rem; cursor: pointer; transition: all 0.2s; }
    .pill-btn:hover, .pill-btn.active { background: var(--border); color: #fff; border-color: var(--cyan); }
    table { width: 100%; border-collapse: collapse; font-size: 0.9rem; text-align: left; }
    th { background: #0f172a; color: var(--text-muted); padding: 0.75rem 1rem; font-weight: 600; border-bottom: 1px solid var(--border); }
    td { padding: 0.85rem 1rem; border-bottom: 1px solid var(--border); vertical-align: top; }
    tr:hover { background: rgba(255, 255, 255, 0.02); }
    .badge { display: inline-block; padding: 0.2rem 0.6rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; text-transform: uppercase; }
    .badge-critical { background: rgba(248, 113, 113, 0.2); color: var(--red); border: 1px solid var(--red); }
    .badge-error { background: rgba(248, 113, 113, 0.15); color: #fca5a5; }
    .badge-warning { background: rgba(251, 191, 36, 0.2); color: var(--amber); }
    .badge-info { background: rgba(56, 189, 248, 0.2); color: var(--cyan); }
    .badge-pass { background: rgba(74, 222, 128, 0.2); color: var(--green); }
    .badge-fail { background: rgba(248, 113, 113, 0.2); color: var(--red); }
    .code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 0.85rem; background: #0f172a; padding: 0.15rem 0.35rem; border-radius: 4px; }
    .winner { color: var(--green); font-weight: 600; }
    .loser { color: var(--red); }
    footer { text-align: center; color: var(--text-muted); font-size: 0.85rem; margin-top: 3rem; }

    /* Print Stylesheet for High-Quality PDF Output */
    @media print {
      @page {
        size: letter landscape;
        margin: 12mm;
      }
      body {
        background: #ffffff !important;
        color: #0f172a !important;
        padding: 0 !important;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
      }
      .no-print {
        display: none !important;
      }
      header {
        border-bottom: 2px solid #0f172a !important;
        margin-bottom: 1rem !important;
      }
      h1 {
        color: #0f172a !important;
        font-size: 1.5rem !important;
      }
      .kpi-card, .card {
        background: #ffffff !important;
        border: 1px solid #cbd5e1 !important;
        color: #0f172a !important;
        box-shadow: none !important;
        page-break-inside: avoid !important;
        margin-bottom: 1rem !important;
      }
      .card-title {
        color: #0f172a !important;
        border-bottom: 1px solid #e2e8f0;
        padding-bottom: 0.5rem;
      }
      th {
        background: #f1f5f9 !important;
        color: #334155 !important;
        border-bottom: 2px solid #cbd5e1 !important;
      }
      td {
        border-bottom: 1px solid #e2e8f0 !important;
        color: #0f172a !important;
      }
      .code {
        background: #f8fafc !important;
        color: #334155 !important;
        border: 1px solid #e2e8f0 !important;
      }
      thead {
        display: table-header-group !important;
      }
      tr {
        page-break-inside: avoid !important;
      }
      .badge-critical {
        background: #fee2e2 !important;
        color: #b91c1c !important;
        border: 1px solid #b91c1c !important;
      }
      .badge-error {
        background: #fee2e2 !important;
        color: #b91c1c !important;
      }
      .badge-warning {
        background: #fef3c7 !important;
        color: #b45309 !important;
      }
      .badge-pass {
        background: #dcfce7 !important;
        color: #15803d !important;
      }
      .badge-info {
        background: #e0f2fe !important;
        color: #0369a1 !important;
      }
      footer {
        color: #64748b !important;
        border-top: 1px solid #cbd5e1;
        padding-top: 0.5rem;
      }
    }
";

    public static string GenerateGpoReport(GpoAuditReport report, string domainName = "Active Directory")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>FlowAD - GPO Audit & Deconfliction Report ({WebUtility.HtmlEncode(domainName)})</title>");
        sb.AppendLine($"  <style>{BaseStyles}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <header>");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <h1>FlowAD GPO Audit & Deconfliction Report</h1>");
        sb.AppendLine($"        <div class=\"subtitle\">Target: <strong>{WebUtility.HtmlEncode(domainName)}</strong> | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"header-actions\">");
        sb.AppendLine("        <span class=\"badge badge-info\">FlowDevs Certified Engine</span>");
        sb.AppendLine("        <button class=\"btn-print no-print\" onclick=\"window.print()\">&#128424; Print to PDF</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        // KPIs
        var criticalCount = report.Conflicts.Count(c => c.Severity == Severity.Critical);
        var errorCount = report.Conflicts.Count(c => c.Severity == Severity.Error);

        sb.AppendLine("    <div class=\"kpi-grid\">");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">Total GPOs</div><div class=\"kpi-value\">{report.TotalGpos}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">Total Conflicts</div><div class=\"kpi-value\" style=\"color: {(report.Conflicts.Count > 0 ? "var(--amber)" : "var(--green)")}\">{report.Conflicts.Count}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">Critical Privilege Overwrites</div><div class=\"kpi-value\" style=\"color: {(criticalCount > 0 ? "var(--red)" : "var(--green)")}\">{criticalCount}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">Direct Contradictions</div><div class=\"kpi-value\" style=\"color: {(errorCount > 0 ? "var(--red)" : "var(--green)")}\">{errorCount}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">AD/SYSVOL Mismatches</div><div class=\"kpi-value\" style=\"color: {(report.VersionMismatches.Count > 0 ? "var(--red)" : "var(--green)")}\">{report.VersionMismatches.Count}</div></div>");
        sb.AppendLine("    </div>");

        // Conflict Matrix Card
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div class=\"card-title\">");
        sb.AppendLine("        <span>GPO Precedence & Conflict Matrix</span>");
        sb.AppendLine($"        <span class=\"badge {(report.OverallStatus == CheckStatus.Pass ? "badge-pass" : "badge-critical")}\">{report.OverallStatus}</span>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"filter-bar no-print\">");
        sb.AppendLine("        <input type=\"text\" id=\"conflictSearch\" class=\"search-input\" placeholder=\"Search settings, GPOs, or scopes...\">");
        sb.AppendLine("        <button class=\"pill-btn active\" onclick=\"filterSeverity('all')\">All</button>");
        sb.AppendLine("        <button class=\"pill-btn\" onclick=\"filterSeverity('critical')\">Critical</button>");
        sb.AppendLine("        <button class=\"pill-btn\" onclick=\"filterSeverity('error')\">Error</button>");
        sb.AppendLine("        <button class=\"pill-btn\" onclick=\"filterSeverity('warning')\">Warning</button>");
        sb.AppendLine("        <button class=\"pill-btn\" onclick=\"filterSeverity('info')\">Info</button>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <table id=\"conflictTable\">");
        sb.AppendLine("        <thead>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <th>Scope</th>");
        sb.AppendLine("            <th>Setting / Key</th>");
        sb.AppendLine("            <th>Conflict Type</th>");
        sb.AppendLine("            <th>Winning GPO &amp; Value</th>");
        sb.AppendLine("            <th>Losing GPO &amp; Value</th>");
        sb.AppendLine("            <th>Severity</th>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </thead>");
        sb.AppendLine("        <tbody>");

        if (report.Conflicts.Count == 0)
        {
            sb.AppendLine("          <tr><td colspan=\"6\" style=\"text-align: center; color: var(--green); padding: 2rem;\">Zero policy collisions detected! All GPO settings are completely deconflicted.</td></tr>");
        }
        else
        {
            foreach (var c in report.Conflicts)
            {
                var sevClass = c.Severity.ToString().ToLowerInvariant();
                sb.AppendLine($"          <tr data-severity=\"{sevClass}\">");
                sb.AppendLine($"            <td><span class=\"code\">{WebUtility.HtmlEncode(c.TargetScope)}</span></td>");
                sb.AppendLine($"            <td><strong>{WebUtility.HtmlEncode(c.SettingIdentifier)}</strong><br><small style=\"color: var(--text-muted);\">{WebUtility.HtmlEncode(c.Explanation)}</small></td>");
                sb.AppendLine($"            <td>{WebUtility.HtmlEncode(c.ConflictType.ToString())}</td>");
                sb.AppendLine($"            <td><span class=\"winner\">{WebUtility.HtmlEncode(c.WinningGpo)}</span><br><span class=\"code\">{WebUtility.HtmlEncode(c.WinningValue)}</span></td>");
                sb.AppendLine($"            <td><span class=\"loser\">{WebUtility.HtmlEncode(c.LosingGpo)}</span><br><span class=\"code\">{WebUtility.HtmlEncode(c.LosingValue)}</span></td>");
                sb.AppendLine($"            <td><span class=\"badge badge-{sevClass}\">{c.Severity}</span></td>");
                sb.AppendLine("          </tr>");
            }
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");

        // Interactive Script
        sb.AppendLine(@"
    <script>
      let currentSev = 'all';
      const searchInput = document.getElementById('conflictSearch');
      const rows = document.querySelectorAll('#conflictTable tbody tr');

      function filterRows() {
        const query = searchInput.value.toLowerCase();
        rows.forEach(row => {
          const sev = row.getAttribute('data-severity') || '';
          const text = row.innerText.toLowerCase();
          const matchesSev = currentSev === 'all' || sev === currentSev;
          const matchesSearch = text.includes(query);
          row.style.display = matchesSev && matchesSearch ? '' : 'none';
        });
      }

      function filterSeverity(sev) {
        currentSev = sev;
        document.querySelectorAll('.pill-btn').forEach(btn => {
          btn.classList.toggle('active', btn.innerText.toLowerCase() === sev);
        });
        filterRows();
      }

      if (searchInput) {
        searchInput.addEventListener('input', filterRows);
      }
    </script>
");

        sb.AppendLine("    <footer>FlowAD Diagnostic Suite &bull; Signed by FlowDevs LLC &bull; Licensed under Apache-2.0</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public static string GenerateDoctorReport(DoctorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>FlowAD - Active Directory Health & Audit Report ({WebUtility.HtmlEncode(report.DomainName)})</title>");
        sb.AppendLine($"  <style>{BaseStyles}</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        var scoreColor = report.OverallScore switch
        {
            >= 85 => "var(--green)",
            >= 60 => "var(--amber)",
            _ => "var(--red)"
        };

        sb.AppendLine("    <header>");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <h1>FlowAD Executive Domain Health Report</h1>");
        sb.AppendLine($"        <div class=\"subtitle\">Target Domain: <strong>{WebUtility.HtmlEncode(report.DomainName)}</strong> | Generated: {report.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"header-actions\">");
        sb.AppendLine("        <span class=\"badge badge-info\">FlowDevs Certified Engine</span>");
        sb.AppendLine("        <button class=\"btn-print no-print\" onclick=\"window.print()\">&#128424; Print to PDF</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        // KPIs
        sb.AppendLine("    <div class=\"kpi-grid\">");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">Health Score</div><div class=\"kpi-value\" style=\"color: {scoreColor}\">{report.OverallScore}/100</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">GPO Conflicts</div><div class=\"kpi-value\" style=\"color: {(report.GpoReport.Conflicts.Count > 0 ? "var(--amber)" : "var(--green)")}\">{report.GpoReport.Conflicts.Count}</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">DNS SRV Tests</div><div class=\"kpi-value\">{report.DnsReport.SrvRecordChecks.Count} passed</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">DNS Latency</div><div class=\"kpi-value\">{report.DnsReport.AverageLatencyMs:F1} ms</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">FSMO Consensus</div><div class=\"kpi-value\" style=\"color: {(report.HealthReport.FsmoRoles.IsConsensusReached ? "var(--green)" : "var(--red)")}\">{(report.HealthReport.FsmoRoles.IsConsensusReached ? "OK" : "SPLIT")}</div></div>");
        sb.AppendLine("    </div>");

        // Section 1: GPO Conflicts
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div class=\"card-title\">");
        sb.AppendLine("        <span>Group Policy (GPO) Precedence & Conflict Matrix</span>");
        sb.AppendLine($"        <span class=\"badge {(report.GpoReport.OverallStatus == CheckStatus.Pass ? "badge-pass" : "badge-critical")}\">{report.GpoReport.OverallStatus}</span>");
        sb.AppendLine("      </div>");

        if (report.GpoReport.Conflicts.Count == 0)
        {
            sb.AppendLine("      <p style=\"color: var(--green); padding: 1rem;\">Zero policy collisions detected! All GPO settings are completely deconflicted.</p>");
        }
        else
        {
            sb.AppendLine("      <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("          <tr>");
            sb.AppendLine("            <th>Scope</th>");
            sb.AppendLine("            <th>Setting</th>");
            sb.AppendLine("            <th>Conflict Type</th>");
            sb.AppendLine("            <th>Winning GPO</th>");
            sb.AppendLine("            <th>Losing GPO</th>");
            sb.AppendLine("            <th>Severity</th>");
            sb.AppendLine("          </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");

            foreach (var c in report.GpoReport.Conflicts)
            {
                var sevClass = c.Severity.ToString().ToLowerInvariant();
                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td><span class=\"code\">{WebUtility.HtmlEncode(c.TargetScope)}</span></td>");
                sb.AppendLine($"            <td><strong>{WebUtility.HtmlEncode(c.SettingIdentifier)}</strong><br><small style=\"color: var(--text-muted);\">{WebUtility.HtmlEncode(c.Explanation)}</small></td>");
                sb.AppendLine($"            <td>{WebUtility.HtmlEncode(c.ConflictType.ToString())}</td>");
                sb.AppendLine($"            <td><span class=\"winner\">{WebUtility.HtmlEncode(c.WinningGpo)}</span> ({WebUtility.HtmlEncode(c.WinningValue)})</td>");
                sb.AppendLine($"            <td><span class=\"loser\">{WebUtility.HtmlEncode(c.LosingGpo)}</span> ({WebUtility.HtmlEncode(c.LosingValue)})</td>");
                sb.AppendLine($"            <td><span class=\"badge badge-{sevClass}\">{c.Severity}</span></td>");
                sb.AppendLine("          </tr>");
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
        }
        sb.AppendLine("    </div>");

        // Section 2: DNS Health
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div class=\"card-title\">");
        sb.AppendLine("        <span>Active Directory DNS & Critical SRV Records</span>");
        sb.AppendLine($"        <span class=\"badge {(report.DnsReport.OverallStatus == CheckStatus.Pass ? "badge-pass" : "badge-critical")}\">{report.DnsReport.OverallStatus}</span>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <table>");
        sb.AppendLine("        <thead>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <th>SRV Record</th>");
        sb.AppendLine("            <th>Service Purpose</th>");
        sb.AppendLine("            <th>Status</th>");
        sb.AppendLine("            <th>Target Endpoints</th>");
        sb.AppendLine("            <th>Latency</th>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </thead>");
        sb.AppendLine("        <tbody>");

        foreach (var srv in report.DnsReport.SrvRecordChecks)
        {
            var targets = string.Join(", ", srv.Endpoints.Select(e => $"{e.TargetHost}:{e.Port}"));
            if (string.IsNullOrEmpty(targets)) targets = "None";
            var statusBadge = srv.Status == CheckStatus.Pass ? "badge-pass" : "badge-fail";

            sb.AppendLine("          <tr>");
            sb.AppendLine($"            <td><span class=\"code\">{WebUtility.HtmlEncode(srv.RecordName)}</span></td>");
            sb.AppendLine($"            <td>{WebUtility.HtmlEncode(srv.Purpose)}</td>");
            sb.AppendLine($"            <td><span class=\"badge {statusBadge}\">{srv.Status}</span></td>");
            sb.AppendLine($"            <td>{WebUtility.HtmlEncode(targets)}</td>");
            sb.AppendLine($"            <td>{srv.LatencyMs:F1} ms</td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");

        // Section 3: Domain Controller Health
        sb.AppendLine("    <div class=\"card\">");
        sb.AppendLine("      <div class=\"card-title\">");
        sb.AppendLine("        <span>Domain Controller Reachability & FSMO Roles</span>");
        sb.AppendLine($"        <span class=\"badge {(report.HealthReport.OverallStatus == CheckStatus.Pass ? "badge-pass" : "badge-critical")}\">{report.HealthReport.OverallStatus}</span>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <table>");
        sb.AppendLine("        <thead>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <th>DC Host</th>");
        sb.AppendLine("            <th>IP Address</th>");
        sb.AppendLine("            <th>Reachable</th>");
        sb.AppendLine("            <th>Clock Skew</th>");
        sb.AppendLine("            <th>SYSVOL / Netlogon</th>");
        sb.AppendLine("            <th>Status</th>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </thead>");
        sb.AppendLine("        <tbody>");

        foreach (var dc in report.HealthReport.Dcs)
        {
            var statusBadge = dc.OverallStatus == CheckStatus.Pass ? "badge-pass" : (dc.OverallStatus == CheckStatus.Warning ? "badge-warning" : "badge-fail");
            sb.AppendLine("          <tr>");
            sb.AppendLine($"            <td><strong>{WebUtility.HtmlEncode(dc.DcName)}</strong></td>");
            sb.AppendLine($"            <td>{WebUtility.HtmlEncode(dc.IpAddress)}</td>");
            sb.AppendLine($"            <td>{(dc.IsReachable ? "Online" : "Unreachable")}</td>");
            sb.AppendLine($"            <td>{dc.ClockSkewSeconds:F2}s</td>");
            sb.AppendLine($"            <td>{(dc.SysvolShareAccessible ? "Accessible" : "Failed")}</td>");
            sb.AppendLine($"            <td><span class=\"badge {statusBadge}\">{dc.OverallStatus}</span></td>");
            sb.AppendLine("          </tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");

        sb.AppendLine("      <div style=\"margin-top: 1.5rem; padding: 1rem; background: #0f172a; border-radius: 6px; font-size: 0.9rem;\">");
        sb.AppendLine($"        <strong>FSMO Roles:</strong> PDC: <code>{WebUtility.HtmlEncode(report.HealthReport.FsmoRoles.PdcEmulator)}</code> | Schema: <code>{WebUtility.HtmlEncode(report.HealthReport.FsmoRoles.SchemaMaster)}</code> | Naming: <code>{WebUtility.HtmlEncode(report.HealthReport.FsmoRoles.DomainNamingMaster)}</code> | RID: <code>{WebUtility.HtmlEncode(report.HealthReport.FsmoRoles.RidMaster)}</code> | Infra: <code>{WebUtility.HtmlEncode(report.HealthReport.FsmoRoles.InfrastructureMaster)}</code>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <footer>FlowAD Diagnostic Suite &bull; Signed by FlowDevs LLC &bull; Licensed under Apache-2.0</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
