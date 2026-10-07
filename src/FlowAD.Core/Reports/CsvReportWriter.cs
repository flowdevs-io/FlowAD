using System.Text;
using FlowAD.Core.Models;

namespace FlowAD.Core.Reports;

public static class CsvReportWriter
{
    public static string GenerateConflictsCsv(IEnumerable<GpoConflictResult> conflicts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Scope,SettingIdentifier,ConflictType,WinningGPO,WinningValue,LosingGPO,LosingValue,Severity,Explanation");

        foreach (var c in conflicts)
        {
            sb.AppendLine(string.Join(",",
                Escape(c.TargetScope),
                Escape(c.SettingIdentifier),
                Escape(c.ConflictType.ToString()),
                Escape(c.WinningGpo),
                Escape(c.WinningValue),
                Escape(c.LosingGpo),
                Escape(c.LosingValue),
                Escape(c.Severity.ToString()),
                Escape(c.Explanation)
            ));
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
