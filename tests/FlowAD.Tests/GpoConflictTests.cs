using FlowAD.Core.Models;
using FlowAD.Gpo;
using Xunit;

namespace FlowAD.Tests;

public class GpoConflictTests
{
    [Fact]
    public void AnalyzeConflictsForScope_ContradictoryDwordValues_DetectsContradictionError()
    {
        var gpoA = new GroupPolicyObject
        {
            DisplayName = "GPO-Baseline",
            RegistrySettings =
            [
                new PolicyRegistrySetting
                {
                    Hive = "HKLM",
                    KeyPath = "Software\\Policies\\Microsoft\\Windows\\System",
                    ValueName = "DisableCMD",
                    Type = RegistryValueType.RegDword,
                    Value = 1u
                }
            ]
        };

        var gpoB = new GroupPolicyObject
        {
            DisplayName = "GPO-DevOverrides",
            RegistrySettings =
            [
                new PolicyRegistrySetting
                {
                    Hive = "HKLM",
                    KeyPath = "Software\\Policies\\Microsoft\\Windows\\System",
                    ValueName = "DisableCMD",
                    Type = RegistryValueType.RegDword,
                    Value = 0u
                }
            ]
        };

        var ordered = new List<EvaluatedGpoOrder>
        {
            new() { Gpo = gpoA, ContainerDn = "OU=Dev,DC=flowdevs,DC=local", ContainerDepth = 1, LinkOrder = 2, IsEnforced = false, PrecedenceRank = 100 },
            new() { Gpo = gpoB, ContainerDn = "OU=Dev,DC=flowdevs,DC=local", ContainerDepth = 2, LinkOrder = 1, IsEnforced = false, PrecedenceRank = 200 }
        };

        var conflicts = GpoConflictDetector.AnalyzeConflictsForScope("OU=Dev,DC=flowdevs,DC=local", ordered);

        Assert.Single(conflicts);
        var conflict = conflicts[0];
        Assert.Equal(ConflictType.DirectContradiction, conflict.ConflictType);
        Assert.Equal("GPO-DevOverrides", conflict.WinningGpo);
        Assert.Equal("0", conflict.WinningValue);
        Assert.Equal("GPO-Baseline", conflict.LosingGpo);
        Assert.Equal("1", conflict.LosingValue);
        Assert.Equal(Severity.Error, conflict.Severity);
    }

    [Fact]
    public void AnalyzeConflictsForScope_IdenticalValues_DetectsDuplicateInfo()
    {
        var gpoA = new GroupPolicyObject
        {
            DisplayName = "GPO-Alpha",
            RegistrySettings =
            [
                new PolicyRegistrySetting
                {
                    Hive = "HKLM",
                    KeyPath = "Software\\Policies\\Microsoft\\Windows\\System",
                    ValueName = "SupportPhone",
                    Type = RegistryValueType.RegSz,
                    Value = "800-555-FLOW"
                }
            ]
        };

        var gpoB = new GroupPolicyObject
        {
            DisplayName = "GPO-Beta",
            RegistrySettings =
            [
                new PolicyRegistrySetting
                {
                    Hive = "HKLM",
                    KeyPath = "Software\\Policies\\Microsoft\\Windows\\System",
                    ValueName = "SupportPhone",
                    Type = RegistryValueType.RegSz,
                    Value = "800-555-FLOW"
                }
            ]
        };

        var ordered = new List<EvaluatedGpoOrder>
        {
            new() { Gpo = gpoA, ContainerDn = "OU=All,DC=flowdevs,DC=local", ContainerDepth = 1, LinkOrder = 2, IsEnforced = false, PrecedenceRank = 100 },
            new() { Gpo = gpoB, ContainerDn = "OU=All,DC=flowdevs,DC=local", ContainerDepth = 1, LinkOrder = 1, IsEnforced = false, PrecedenceRank = 200 }
        };

        var conflicts = GpoConflictDetector.AnalyzeConflictsForScope("OU=All,DC=flowdevs,DC=local", ordered);

        Assert.Single(conflicts);
        Assert.Equal(ConflictType.DuplicateSetting, conflicts[0].ConflictType);
        Assert.Equal(Severity.Info, conflicts[0].Severity);
    }

    [Fact]
    public void AnalyzeConflictsForScope_ConflictingUserRights_DetectsCriticalPrivilegeCollision()
    {
        var gpoA = new GroupPolicyObject
        {
            DisplayName = "GPO-Tier1-Admins",
            SecuritySettings =
            [
                new PolicySecuritySetting
                {
                    Section = "Privilege Rights",
                    SettingName = "SeDebugPrivilege",
                    AssignedIdentities = ["*S-1-5-32-544", "*S-1-5-21-12345-512"]
                }
            ]
        };

        var gpoB = new GroupPolicyObject
        {
            DisplayName = "GPO-Security-Hardening",
            SecuritySettings =
            [
                new PolicySecuritySetting
                {
                    Section = "Privilege Rights",
                    SettingName = "SeDebugPrivilege",
                    AssignedIdentities = ["*S-1-5-32-544"]
                }
            ]
        };

        var ordered = new List<EvaluatedGpoOrder>
        {
            new() { Gpo = gpoA, ContainerDn = "OU=Servers,DC=flowdevs,DC=local", ContainerDepth = 1, LinkOrder = 2, IsEnforced = false, PrecedenceRank = 100 },
            new() { Gpo = gpoB, ContainerDn = "OU=Servers,DC=flowdevs,DC=local", ContainerDepth = 2, LinkOrder = 1, IsEnforced = false, PrecedenceRank = 200 }
        };

        var conflicts = GpoConflictDetector.AnalyzeConflictsForScope("OU=Servers,DC=flowdevs,DC=local", ordered);

        Assert.Single(conflicts);
        Assert.Equal(ConflictType.UserRightsCollision, conflicts[0].ConflictType);
        Assert.Equal(Severity.Critical, conflicts[0].Severity);
        Assert.Contains("CRITICAL User Rights collision", conflicts[0].Explanation);
    }
}
