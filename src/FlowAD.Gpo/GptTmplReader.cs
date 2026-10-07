using FlowAD.Core.Models;

namespace FlowAD.Gpo;

public static class GptTmplReader
{
    public static List<PolicySecuritySetting> ReadFile(string filePath, Guid gpoGuid, string gpoName, PolicyScope scope = PolicyScope.Computer)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var lines = File.ReadAllLines(filePath);
        var settings = new List<PolicySecuritySetting>();
        string currentSection = "General";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var settingName = parts[0].Trim();
            var rawValue = parts[1].Trim();

            // Identifiers / SIDs separated by comma
            var identities = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            settings.Add(new PolicySecuritySetting
            {
                GpoGuid = gpoGuid,
                GpoName = gpoName,
                Section = currentSection,
                SettingName = settingName,
                AssignedIdentities = identities,
                Scope = scope,
                SourceFile = filePath
            });
        }

        return settings;
    }
}
