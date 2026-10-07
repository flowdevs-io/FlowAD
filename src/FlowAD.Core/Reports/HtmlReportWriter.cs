using System.Net;
using System.Text;
using FlowAD.Core.Models;

namespace FlowAD.Core.Reports;

public static class HtmlReportWriter
{
    public static string GenerateGpoReport(GpoAuditReport report, string domainName = "Active Directory")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>FlowAD - GPO Audit & Deconfliction Report ({WebUtility.HtmlEncode(domainName)})</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(@"
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
    .container { max-width: 1300px; margin: 0 auto; }
    header { margin-bottom: 2rem; border-bottom: 1px solid var(--border); padding-bottom: 1.5rem; display: flex; justify-content: space-between; align-items: flex-start; }
    h1 { font-size: 1.8rem; color: var(--cyan); display: flex; align-items: center; gap: 0.5rem; }
    .subtitle { color: var(--text-muted); font-size: 0.95rem; margin-top: 0.25rem; }
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
    .kpi-card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 8px; padding: 1.25rem; }
    .kpi-title { font-size: 0.85rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.05em; }
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
    .code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 0.85rem; background: #0f172a; padding: 0.15rem 0.35rem; border-radius: 4px; }
    .winner { color: var(--green); font-weight: 600; }
    .loser { color: var(--red); }
    footer { text-align: center; color: var(--text-muted); font-size: 0.85rem; margin-top: 3rem; }
");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <header>");
        sb.AppendLine("      <div>");
        sb.AppendLine("        <h1>FlowAD GPO Audit & Deconfliction Report</h1>");
        sb.AppendLine($"        <div class=\"subtitle\">Target: <strong>{WebUtility.HtmlEncode(domainName)}</strong> | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div><span class=\"badge badge-info\">FlowDevs Certified Engine</span></div>");
        sb.AppendLine("    </header>");

        // KPIs
        var criticalCount = report.Conflicts.Count(c => c.Severity == Severity.Critical);
        var errorCount = report.Conflicts.Count(c => c.Severity == Severity.Error);
        var warningCount = report.Conflicts.Count(c => c.Severity == Severity.Warning);

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

        sb.AppendLine("      <div class=\"filter-bar\">");
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

        // Interactive Script for instant filtering
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
}
