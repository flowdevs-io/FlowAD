namespace FlowAD.Core.Models;

public enum Severity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum CheckStatus
{
    Pass,
    Warning,
    Fail,
    Skipped
}

public enum PolicyScope
{
    Computer,
    User
}

public enum RegistryValueType
{
    RegNone = 0,
    RegSz = 1,
    RegExpandSz = 2,
    RegBinary = 3,
    RegDword = 4,
    RegMultiSz = 7,
    RegQword = 11,
    Unknown = 99
}

public enum ConflictType
{
    /// <summary>
    /// Two or more GPOs set contradictory values for the same setting (e.g. 1 vs 0).
    /// </summary>
    DirectContradiction,

    /// <summary>
    /// A higher-precedence GPO overwrites a setting from a lower-precedence GPO.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Multiple GPOs redundantly define the exact same setting with identical value.
    /// </summary>
    DuplicateSetting,

    /// <summary>
    /// A security policy or User Rights Assignment (e.g. SeDebugPrivilege) is completely overwritten rather than merged.
    /// </summary>
    UserRightsCollision,

    /// <summary>
    /// A GPO sets an invalid, deprecated, or malformed key.
    /// </summary>
    MalformedSetting
}
