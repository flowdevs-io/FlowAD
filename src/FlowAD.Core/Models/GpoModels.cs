namespace FlowAD.Core.Models;

public sealed class GroupPolicyObject
{
    public Guid Guid { get; set; } = Guid.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GpcPath { get; set; } = string.Empty;
    public string SysvolPath { get; set; } = string.Empty;
    public int GpcVersion { get; set; }
    public int GptVersion { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsUserDisabled { get; set; }
    public bool IsComputerDisabled { get; set; }
    public string? WmiFilter { get; set; }
    public List<GpoLink> Links { get; set; } = [];
    public List<PolicyRegistrySetting> RegistrySettings { get; set; } = [];
    public List<PolicySecuritySetting> SecuritySettings { get; set; } = [];

    public bool HasVersionMismatch => GpcVersion != GptVersion && GptVersion > 0;
    public bool IsCompletelyDisabled => !IsEnabled || (IsUserDisabled && IsComputerDisabled);
    public bool IsEmpty => RegistrySettings.Count == 0 && SecuritySettings.Count == 0;
}

public sealed class GpoLink
{
    public string TargetContainerDn { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsEnforced { get; set; }
}

public sealed class PolicyRegistrySetting
{
    public Guid GpoGuid { get; set; }
    public string GpoName { get; set; } = string.Empty;
    public string Hive { get; set; } = "HKLM";
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public RegistryValueType Type { get; set; } = RegistryValueType.RegDword;
    public object? Value { get; set; }
    public byte[]? RawBytes { get; set; }
    public PolicyScope Scope { get; set; } = PolicyScope.Computer;
    public string SourceFile { get; set; } = string.Empty;

    public string NormalizedKey => $"{Hive}\\{KeyPath.Trim('\\')}\\{ValueName}".ToUpperInvariant();
}

public sealed class PolicySecuritySetting
{
    public Guid GpoGuid { get; set; }
    public string GpoName { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public List<string> AssignedIdentities { get; set; } = [];
    public PolicyScope Scope { get; set; } = PolicyScope.Computer;
    public string SourceFile { get; set; } = string.Empty;

    public string NormalizedKey => $"[SECURITY]\\{Section}\\{SettingName}".ToUpperInvariant();
}

public sealed class GpoConflictResult
{
    public string TargetScope { get; set; } = string.Empty;
    public string SettingIdentifier { get; set; } = string.Empty;
    public ConflictType ConflictType { get; set; }
    public string WinningGpo { get; set; } = string.Empty;
    public string WinningValue { get; set; } = string.Empty;
    public string LosingGpo { get; set; } = string.Empty;
    public string LosingValue { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Warning;
}

public sealed class GpoAuditReport
{
    public int TotalGpos { get; set; }
    public List<GroupPolicyObject> UnlinkedGpos { get; set; } = [];
    public List<GroupPolicyObject> VersionMismatches { get; set; } = [];
    public List<GroupPolicyObject> EmptyGpos { get; set; } = [];
    public List<GroupPolicyObject> DisabledGpos { get; set; } = [];
    public List<GpoConflictResult> Conflicts { get; set; } = [];
    public CheckStatus OverallStatus => Conflicts.Any(c => c.Severity == Severity.Error || c.Severity == Severity.Critical) || VersionMismatches.Count > 0
        ? CheckStatus.Fail
        : (Conflicts.Count > 0 || UnlinkedGpos.Count > 0 ? CheckStatus.Warning : CheckStatus.Pass);
}
