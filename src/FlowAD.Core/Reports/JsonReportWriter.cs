using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowAD.Core.Reports;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T report) => JsonSerializer.Serialize(report, Options);

    public static async Task WriteToFileAsync<T>(string filePath, T report, CancellationToken cancellationToken = default)
    {
        var json = Serialize(report);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
