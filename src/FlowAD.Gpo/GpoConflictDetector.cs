using FlowAD.Core.Models;

namespace FlowAD.Gpo;

public static class GpoConflictDetector
{
    public static List<GpoConflictResult> AnalyzeConflictsForScope(
        string targetScopeDn,
        List<EvaluatedGpoOrder> orderedGpos)
    {
        var conflicts = new List<GpoConflictResult>();

        // 1. Analyze Registry Settings conflicts
        var regSettingsByKey = new Dictionary<string, List<(EvaluatedGpoOrder Order, PolicyRegistrySetting Setting)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var eval in orderedGpos)
        {
            foreach (var reg in eval.Gpo.RegistrySettings)
            {
                var key = reg.NormalizedKey;
                if (!regSettingsByKey.TryGetValue(key, out var list))
                {
                    list = [];
                    regSettingsByKey[key] = list;
                }
                list.Add((eval, reg));
            }
        }

        foreach (var (key, instances) in regSettingsByKey)
        {
            if (instances.Count <= 1) continue;

            // instances are in ascending precedence order; last one wins!
            var winning = instances[^1];

            for (int i = 0; i < instances.Count - 1; i++)
            {
                var losing = instances[i];
                var winValStr = FormatValue(winning.Setting.Value);
                var loseValStr = FormatValue(losing.Setting.Value);

                if (string.Equals(winValStr, loseValStr, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add(new GpoConflictResult
                    {
                        TargetScope = targetScopeDn,
                        SettingIdentifier = key,
                        ConflictType = ConflictType.DuplicateSetting,
                        WinningGpo = winning.Order.Gpo.DisplayName,
                        WinningValue = winValStr,
                        LosingGpo = losing.Order.Gpo.DisplayName,
                        LosingValue = loseValStr,
                        Explanation = $"Redundant configuration: Both '{winning.Order.Gpo.DisplayName}' and '{losing.Order.Gpo.DisplayName}' set the identical value '{winValStr}'.",
                        Severity = Severity.Info
                    });
                }
                else
                {
                    bool isContradiction = IsOpposingValue(winning.Setting.Value, losing.Setting.Value);
                    conflicts.Add(new GpoConflictResult
                    {
                        TargetScope = targetScopeDn,
                        SettingIdentifier = key,
                        ConflictType = isContradiction ? ConflictType.DirectContradiction : ConflictType.Overwrite,
                        WinningGpo = winning.Order.Gpo.DisplayName,
                        WinningValue = winValStr,
                        LosingGpo = losing.Order.Gpo.DisplayName,
                        LosingValue = loseValStr,
                        Explanation = isContradiction
                            ? $"Direct Contradiction: '{winning.Order.Gpo.DisplayName}' sets '{winValStr}' but '{losing.Order.Gpo.DisplayName}' sets opposing value '{loseValStr}'. '{winning.Order.Gpo.DisplayName}' wins due to precedence rank {winning.Order.PrecedenceRank} vs {losing.Order.PrecedenceRank}."
                            : $"Setting Overwrite: '{winning.Order.Gpo.DisplayName}' overwrites setting from '{losing.Order.Gpo.DisplayName}'.",
                        Severity = isContradiction ? Severity.Error : Severity.Warning
                    });
                }
            }
        }

        // 2. Analyze Security Settings / User Rights Assignment conflicts
        var secSettingsByKey = new Dictionary<string, List<(EvaluatedGpoOrder Order, PolicySecuritySetting Setting)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var eval in orderedGpos)
        {
            foreach (var sec in eval.Gpo.SecuritySettings)
            {
                var key = sec.NormalizedKey;
                if (!secSettingsByKey.TryGetValue(key, out var list))
                {
                    list = [];
                    secSettingsByKey[key] = list;
                }
                list.Add((eval, sec));
            }
        }

        foreach (var (key, instances) in secSettingsByKey)
        {
            if (instances.Count <= 1) continue;

            var winning = instances[^1];

            for (int i = 0; i < instances.Count - 1; i++)
            {
                var losing = instances[i];
                var winIdentities = string.Join(", ", winning.Setting.AssignedIdentities);
                var loseIdentities = string.Join(", ", losing.Setting.AssignedIdentities);

                bool identical = winning.Setting.AssignedIdentities.OrderBy(x => x).SequenceEqual(losing.Setting.AssignedIdentities.OrderBy(x => x));

                if (identical)
                {
                    conflicts.Add(new GpoConflictResult
                    {
                        TargetScope = targetScopeDn,
                        SettingIdentifier = key,
                        ConflictType = ConflictType.DuplicateSetting,
                        WinningGpo = winning.Order.Gpo.DisplayName,
                        WinningValue = winIdentities,
                        LosingGpo = losing.Order.Gpo.DisplayName,
                        LosingValue = loseIdentities,
                        Explanation = $"Redundant security policy: Both GPOs assign identical identities to {winning.Setting.SettingName}.",
                        Severity = Severity.Info
                    });
                }
                else
                {
                    conflicts.Add(new GpoConflictResult
                    {
                        TargetScope = targetScopeDn,
                        SettingIdentifier = key,
                        ConflictType = ConflictType.UserRightsCollision,
                        WinningGpo = winning.Order.Gpo.DisplayName,
                        WinningValue = winIdentities,
                        LosingGpo = losing.Order.Gpo.DisplayName,
                        LosingValue = loseIdentities,
                        Explanation = $"CRITICAL User Rights collision: Windows Security Templates do NOT merge user rights assignments! '{winning.Order.Gpo.DisplayName}' completely replaces '{losing.Order.Gpo.DisplayName}', causing identities [{loseIdentities}] to be excluded.",
                        Severity = Severity.Critical
                    });
                }
            }
        }

        return conflicts;
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "<null>";
        if (value is string[] array) return string.Join(";", array);
        return value.ToString() ?? "<empty>";
    }

    private static bool IsOpposingValue(object? val1, object? val2)
    {
        if (val1 is uint u1 && val2 is uint u2)
        {
            return (u1 == 0 && u2 != 0) || (u1 != 0 && u2 == 0);
        }
        if (val1 is int i1 && val2 is int i2)
        {
            return (i1 == 0 && i2 != 0) || (i1 != 0 && i2 == 0);
        }
        if (val1 is string s1 && val2 is string s2)
        {
            var l1 = s1.Trim().ToLowerInvariant();
            var l2 = s2.Trim().ToLowerInvariant();
            if ((l1 == "enabled" && l2 == "disabled") || (l1 == "disabled" && l2 == "enabled")) return true;
            if ((l1 == "1" && l2 == "0") || (l1 == "0" && l2 == "1")) return true;
            if ((l1 == "true" && l2 == "false") || (l1 == "false" && l2 == "true")) return true;
        }
        return false;
    }
}
