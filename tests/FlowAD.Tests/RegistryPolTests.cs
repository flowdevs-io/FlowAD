using System.Text;
using FlowAD.Core.Models;
using FlowAD.Gpo;
using Xunit;

namespace FlowAD.Tests;

public class RegistryPolTests
{
    [Fact]
    public void ReadStream_ValidPRegBuffer_ParsesRecordsCorrectly()
    {
        // Construct a valid PReg binary stream
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.Unicode, leaveOpen: true);

        // Header: "PReg" (4 bytes)
        writer.Write((byte)0x50);
        writer.Write((byte)0x52);
        writer.Write((byte)0x65);
        writer.Write((byte)0x67);

        // Version: uint32 = 1
        writer.Write((uint)1);

        // Record 1: [Software\Policies\Microsoft\Windows\System;DisableTaskMgr;REG_DWORD;4;1]
        WriteRecord(writer, "Software\\Policies\\Microsoft\\Windows\\System", "DisableTaskMgr", 4, BitConverter.GetBytes((uint)1));

        // Record 2: [Software\Policies\Microsoft\Windows\System;SupportContact;REG_SZ;size;FlowDevs Support]
        var strBytes = Encoding.Unicode.GetBytes("FlowDevs Support\0");
        WriteRecord(writer, "Software\\Policies\\Microsoft\\Windows\\System", "SupportContact", 1, strBytes);

        writer.Flush();
        ms.Position = 0;

        var gpoGuid = Guid.NewGuid();
        var settings = RegistryPolReader.ReadStream(ms, gpoGuid, "Test Policy", PolicyScope.Computer);

        Assert.Equal(2, settings.Count);

        var dwordSetting = settings[0];
        Assert.Equal("Software\\Policies\\Microsoft\\Windows\\System", dwordSetting.KeyPath);
        Assert.Equal("DisableTaskMgr", dwordSetting.ValueName);
        Assert.Equal(RegistryValueType.RegDword, dwordSetting.Type);
        Assert.Equal(1u, dwordSetting.Value);
        Assert.Equal("HKLM\\SOFTWARE\\POLICIES\\MICROSOFT\\WINDOWS\\SYSTEM\\DISABLETASKMGR", dwordSetting.NormalizedKey);

        var szSetting = settings[1];
        Assert.Equal("SupportContact", szSetting.ValueName);
        Assert.Equal(RegistryValueType.RegSz, szSetting.Type);
        Assert.Equal("FlowDevs Support", szSetting.Value);
    }

    private static void WriteRecord(BinaryWriter writer, string key, string valueName, uint type, byte[] data)
    {
        // '['
        writer.Write((ushort)'[');

        // Key + null terminator
        foreach (char c in key) writer.Write((ushort)c);
        writer.Write((ushort)0);

        // ';'
        writer.Write((ushort)';');

        // ValueName + null terminator
        foreach (char c in valueName) writer.Write((ushort)c);
        writer.Write((ushort)0);

        // ';'
        writer.Write((ushort)';');

        // Type
        writer.Write(type);

        // ';'
        writer.Write((ushort)';');

        // Size
        writer.Write((uint)data.Length);

        // ';'
        writer.Write((ushort)';');

        // Data
        writer.Write(data);

        // ']'
        writer.Write((ushort)']');
    }
}
