using System.Text;
using FlowAD.Core.Models;
using FlowAD.Gpo;
using Xunit;

namespace FlowAD.Tests;

public class TestDataGeneratorTests
{
    [Fact]
    public void GenerateTestDataAndAuditOfflineDirectory_DetectsExpectedConflicts()
    {
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../TestData/SysvolPolicies"));
        var financeMach = Path.Combine(basePath, "{A1234567-89AB-CDEF-0123-456789ABCDEF}", "Machine");
        var devMach = Path.Combine(basePath, "{B1234567-89AB-CDEF-0123-456789ABCDEF}", "Machine");

        Directory.CreateDirectory(financeMach);
        Directory.CreateDirectory(devMach);

        // Generate Finance Registry.pol: DisableCMD = 1
        WritePRegFile(
            Path.Combine(financeMach, "Registry.pol"),
            "Software\\Policies\\Microsoft\\Windows\\System",
            "DisableCMD",
            4,
            BitConverter.GetBytes((uint)1)
        );

        // Generate Dev Registry.pol: DisableCMD = 0
        WritePRegFile(
            Path.Combine(devMach, "Registry.pol"),
            "Software\\Policies\\Microsoft\\Windows\\System",
            "DisableCMD",
            4,
            BitConverter.GetBytes((uint)0)
        );

        // Now run offline audit against the test sysvol folder
        var report = GpoAuditService.AuditOfflineDirectory(basePath);

        Assert.Equal(4, report.TotalGpos);
        Assert.True(report.Conflicts.Count > 0);

        var cmdConflict = report.Conflicts.FirstOrDefault(c => c.SettingIdentifier.Contains("DISABLECMD"));
        Assert.NotNull(cmdConflict);
        Assert.Equal(ConflictType.DirectContradiction, cmdConflict.ConflictType);

        var secConflict = report.Conflicts.FirstOrDefault(c => c.SettingIdentifier.Contains("SEDEBUGPRIVILEGE"));
        Assert.NotNull(secConflict);
        Assert.Equal(ConflictType.UserRightsCollision, secConflict.ConflictType);
    }

    private static void WritePRegFile(string path, string key, string valueName, uint type, byte[] data)
    {
        using var fs = File.Create(path);
        using var writer = new BinaryWriter(fs, Encoding.Unicode);

        // "PReg"
        writer.Write((byte)0x50);
        writer.Write((byte)0x52);
        writer.Write((byte)0x65);
        writer.Write((byte)0x67);

        // Version = 1
        writer.Write((uint)1);

        // '['
        writer.Write((ushort)'[');
        foreach (char c in key) writer.Write((ushort)c);
        writer.Write((ushort)0);
        writer.Write((ushort)';');
        foreach (char c in valueName) writer.Write((ushort)c);
        writer.Write((ushort)0);
        writer.Write((ushort)';');
        writer.Write(type);
        writer.Write((ushort)';');
        writer.Write((uint)data.Length);
        writer.Write((ushort)';');
        writer.Write(data);
        writer.Write((ushort)']');
    }
}
