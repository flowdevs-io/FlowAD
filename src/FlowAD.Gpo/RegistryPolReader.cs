using System.Text;
using FlowAD.Core.Models;

namespace FlowAD.Gpo;

public static class RegistryPolReader
{
    private static readonly byte[] PRegHeader = [0x50, 0x52, 0x65, 0x67]; // "PReg"

    public static List<PolicyRegistrySetting> ReadFile(string filePath, Guid gpoGuid, string gpoName, PolicyScope scope)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        using var stream = File.OpenRead(filePath);
        return ReadStream(stream, gpoGuid, gpoName, scope, filePath);
    }

    public static List<PolicyRegistrySetting> ReadStream(Stream stream, Guid gpoGuid, string gpoName, PolicyScope scope, string sourceFile = "")
    {
        var settings = new List<PolicyRegistrySetting>();
        if (stream.Length < 8) return settings;

        using var reader = new BinaryReader(stream, Encoding.Unicode, leaveOpen: true);

        // Header check: 4 bytes "PReg"
        var magic = reader.ReadBytes(4);
        if (!magic.SequenceEqual(PRegHeader))
        {
            return settings;
        }

        // Version check: uint32 = 1
        var version = reader.ReadUInt32();
        if (version != 1)
        {
            return settings;
        }

        // Parse records: [key;value;type;size;data]
        while (stream.Position < stream.Length)
        {
            try
            {
                // Opening bracket '[' (0x005B)
                var ch = reader.ReadUInt16();
                if (ch != '[')
                {
                    // Scan forward to next '[' if desynchronized
                    continue;
                }

                var key = ReadNullTerminatedString(reader);
                var sep1 = reader.ReadUInt16(); // ';' (0x003B)

                var valueName = ReadNullTerminatedString(reader);
                var sep2 = reader.ReadUInt16(); // ';'

                var typeVal = reader.ReadUInt32();
                var regType = (RegistryValueType)typeVal;
                var sep3 = reader.ReadUInt16(); // ';'

                var size = reader.ReadUInt32();
                var sep4 = reader.ReadUInt16(); // ';'

                var data = reader.ReadBytes((int)size);
                var closeBracket = reader.ReadUInt16(); // ']' (0x005D)

                object? parsedValue = ParseRegistryData(regType, data);

                // Determine Hive: computer policies default to HKLM, user policies default to HKCU
                var hive = scope == PolicyScope.Computer ? "HKLM" : "HKCU";

                settings.Add(new PolicyRegistrySetting
                {
                    GpoGuid = gpoGuid,
                    GpoName = gpoName,
                    Hive = hive,
                    KeyPath = key,
                    ValueName = valueName,
                    Type = regType,
                    Value = parsedValue,
                    RawBytes = data,
                    Scope = scope,
                    SourceFile = sourceFile
                });
            }
            catch (EndOfStreamException)
            {
                break;
            }
            catch
            {
                // Resilient reading of partially corrupted or non-standard entries
                break;
            }
        }

        return settings;
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var code = reader.ReadUInt16();
            if (code == 0) break;
            sb.Append((char)code);
        }
        return sb.ToString();
    }

    private static object? ParseRegistryData(RegistryValueType type, byte[] data)
    {
        if (data.Length == 0) return null;

        return type switch
        {
            RegistryValueType.RegDword when data.Length >= 4 => BitConverter.ToUInt32(data, 0),
            RegistryValueType.RegQword when data.Length >= 8 => BitConverter.ToUInt64(data, 0),
            RegistryValueType.RegSz or RegistryValueType.RegExpandSz =>
                Encoding.Unicode.GetString(data).TrimEnd('\0'),
            RegistryValueType.RegMultiSz =>
                Encoding.Unicode.GetString(data).Split('\0', StringSplitOptions.RemoveEmptyEntries),
            _ => Convert.ToHexString(data)
        };
    }
}
