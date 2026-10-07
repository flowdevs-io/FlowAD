namespace FlowAD.Gpo;

public static class GptIniReader
{
    public static (int Version, int UserVersion, int ComputerVersion, string? DisplayName) ReadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (0, 0, 0, null);
        }

        var lines = File.ReadAllLines(filePath);
        int totalVersion = 0;
        string? displayName = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('['))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var val = parts[1].Trim();

            if (key.Equals("Version", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var parsedVersion))
            {
                totalVersion = parsedVersion;
            }
            else if (key.Equals("displayName", StringComparison.OrdinalIgnoreCase))
            {
                displayName = val;
            }
        }

        // High 16 bits = User version, Low 16 bits = Computer version
        int userVer = (totalVersion >> 16) & 0xFFFF;
        int compVer = totalVersion & 0xFFFF;

        return (totalVersion, userVer, compVer, displayName);
    }
}
