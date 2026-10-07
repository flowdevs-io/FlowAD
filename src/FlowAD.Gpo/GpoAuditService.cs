using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.RegularExpressions;
using FlowAD.Core.Models;

namespace FlowAD.Gpo;

public static class GpoAuditService
{
    private static readonly Regex GpLinkRegex = new(@"\[LDAP://(?<dn>[^;]+);(?<options>\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Audits GPOs from a local directory (e.g. exported SYSVOL\Policies folder or GPO backups).
    /// </summary>
    public static GpoAuditReport AuditOfflineDirectory(string sysvolPoliciesPath)
    {
        var report = new GpoAuditReport();
        if (!Directory.Exists(sysvolPoliciesPath))
        {
            throw new DirectoryNotFoundException($"Policies directory not found: {sysvolPoliciesPath}");
        }

        var gpos = new List<GroupPolicyObject>();
        var dirs = Directory.GetDirectories(sysvolPoliciesPath);

        foreach (var dir in dirs)
        {
            var dirName = Path.GetFileName(dir);
            Guid guid = Guid.Empty;
            if (dirName.StartsWith('{') && dirName.EndsWith('}'))
            {
                Guid.TryParse(dirName[1..^1], out guid);
            }
            if (guid == Guid.Empty && !Guid.TryParse(dirName, out guid))
            {
                guid = Guid.NewGuid();
            }

            var gptIniPath = Path.Combine(dir, "GPT.ini");
            var (version, userVer, compVer, displayName) = GptIniReader.ReadFile(gptIniPath);

            var gpo = new GroupPolicyObject
            {
                Guid = guid,
                DisplayName = displayName ?? dirName,
                SysvolPath = dir,
                GptVersion = version,
                GpcVersion = version // in offline mode, assumed in sync unless explicit GPC report provided
            };

            // Read Machine Registry.pol
            var machinePolPath = Path.Combine(dir, "Machine", "Registry.pol");
            if (File.Exists(machinePolPath))
            {
                gpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(machinePolPath, gpo.Guid, gpo.DisplayName, PolicyScope.Computer));
            }

            // Read User Registry.pol
            var userPolPath = Path.Combine(dir, "User", "Registry.pol");
            if (File.Exists(userPolPath))
            {
                gpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(userPolPath, gpo.Guid, gpo.DisplayName, PolicyScope.User));
            }

            // Read Security template GptTmpl.inf
            var secInfPath = Path.Combine(dir, "Machine", "Microsoft", "Windows NT", "SecEdit", "GptTmpl.inf");
            if (File.Exists(secInfPath))
            {
                gpo.SecuritySettings.AddRange(GptTmplReader.ReadFile(secInfPath, gpo.Guid, gpo.DisplayName, PolicyScope.Computer));
            }

            gpos.Add(gpo);
        }

        report.TotalGpos = gpos.Count;
        report.EmptyGpos = gpos.Where(g => g.IsEmpty).ToList();

        // Simulate evaluation of all parsed GPOs against a synthesized domain scope to find collisions
        var evaluatedOrders = gpos
            .Select((g, idx) => new EvaluatedGpoOrder
            {
                Gpo = g,
                ContainerDn = "DC=offline,DC=local",
                ContainerDepth = 1,
                LinkOrder = idx + 1,
                IsEnforced = false,
                PrecedenceRank = (10_000L) - ((idx + 1) * 10L)
            })
            .OrderBy(x => x.PrecedenceRank)
            .ToList();

        report.Conflicts = GpoConflictDetector.AnalyzeConflictsForScope("Domain Scope (Offline)", evaluatedOrders);
        return report;
    }

    /// <summary>
    /// Audits GPOs directly against an Active Directory Domain Controller via LDAP.
    /// </summary>
    public static async Task<GpoAuditReport> AuditActiveDirectoryAsync(
        string domainController,
        string domainName,
        NetworkCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        var report = new GpoAuditReport();
        var gpoMap = new Dictionary<Guid, GroupPolicyObject>();

        var ldapIdentifier = new LdapDirectoryIdentifier(domainController, 389);
        using var connection = credential == null
            ? new LdapConnection(ldapIdentifier)
            : new LdapConnection(ldapIdentifier, credential, AuthType.Negotiate);

        connection.Timeout = TimeSpan.FromSeconds(15);
        connection.Bind();

        // 1. Query Default Naming Context and RootDSE
        var rootDseRequest = new SearchRequest("", "(objectClass=*)", SearchScope.Base, "defaultNamingContext", "configurationNamingContext");
        var rootDseResponse = (SearchResponse)await Task.Factory.FromAsync(connection.BeginSendRequest, connection.EndSendRequest, rootDseRequest, PartialResultProcessing.NoPartialResultSupport, null);
        
        string defaultNamingContext = string.Empty;
        if (rootDseResponse.Entries.Count > 0)
        {
            var entry = rootDseResponse.Entries[0];
            if (entry.Attributes.Contains("defaultNamingContext"))
            {
                defaultNamingContext = (string)entry.Attributes["defaultNamingContext"][0];
            }
        }

        if (string.IsNullOrEmpty(defaultNamingContext))
        {
            defaultNamingContext = string.Join(",", domainName.Split('.').Select(dc => $"DC={dc}"));
        }

        // 2. Query all GPOs in CN=Policies,CN=System,<defaultNamingContext>
        var policiesDn = $"CN=Policies,CN=System,{defaultNamingContext}";
        var gpoRequest = new SearchRequest(
            policiesDn,
            "(objectClass=groupPolicyContainer)",
            SearchScope.Subtree,
            "cn", "displayName", "flags", "versionNumber", "gPCFileSysPath", "gPCMachineExtensionNames", "gPCUserExtensionNames"
        );

        var gpoResponse = (SearchResponse)await Task.Factory.FromAsync(connection.BeginSendRequest, connection.EndSendRequest, gpoRequest, PartialResultProcessing.NoPartialResultSupport, null);

        foreach (SearchResultEntry entry in gpoResponse.Entries)
        {
            var cn = entry.Attributes.Contains("cn") ? (string)entry.Attributes["cn"][0] : string.Empty;
            Guid guid = Guid.Empty;
            if (cn.StartsWith('{') && cn.EndsWith('}'))
            {
                Guid.TryParse(cn[1..^1], out guid);
            }
            if (guid == Guid.Empty && !Guid.TryParse(cn, out guid))
            {
                continue;
            }

            var displayName = entry.Attributes.Contains("displayName") ? (string)entry.Attributes["displayName"][0] : cn;
            int gpcVersion = 0;
            if (entry.Attributes.Contains("versionNumber") && int.TryParse(entry.Attributes["versionNumber"][0].ToString(), out var parsedVer))
            {
                gpcVersion = parsedVer;
            }

            int flags = 0;
            if (entry.Attributes.Contains("flags") && int.TryParse(entry.Attributes["flags"][0].ToString(), out var parsedFlags))
            {
                flags = parsedFlags;
            }

            var sysvolPath = entry.Attributes.Contains("gPCFileSysPath") ? (string)entry.Attributes["gPCFileSysPath"][0] : string.Empty;

            var gpo = new GroupPolicyObject
            {
                Guid = guid,
                DisplayName = displayName,
                GpcPath = entry.DistinguishedName,
                SysvolPath = sysvolPath,
                GpcVersion = gpcVersion,
                IsEnabled = (flags & 3) != 3,
                IsUserDisabled = (flags & 1) != 0,
                IsComputerDisabled = (flags & 2) != 0
            };

            // Inspect SYSVOL GPT.ini if accessible
            if (!string.IsNullOrEmpty(sysvolPath))
            {
                var localGptIni = Path.Combine(sysvolPath, "GPT.ini");
                if (File.Exists(localGptIni))
                {
                    var (gptVer, _, _, _) = GptIniReader.ReadFile(localGptIni);
                    gpo.GptVersion = gptVer;

                    var machPol = Path.Combine(sysvolPath, "Machine", "Registry.pol");
                    if (File.Exists(machPol))
                        gpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(machPol, gpo.Guid, gpo.DisplayName, PolicyScope.Computer));

                    var userPol = Path.Combine(sysvolPath, "User", "Registry.pol");
                    if (File.Exists(userPol))
                        gpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(userPol, gpo.Guid, gpo.DisplayName, PolicyScope.User));

                    var secInf = Path.Combine(sysvolPath, "Machine", "Microsoft", "Windows NT", "SecEdit", "GptTmpl.inf");
                    if (File.Exists(secInf))
                        gpo.SecuritySettings.AddRange(GptTmplReader.ReadFile(secInf, gpo.Guid, gpo.DisplayName, PolicyScope.Computer));
                }
            }

            gpoMap[guid] = gpo;
        }

        // 3. Query all containers with gPLink to establish linking & hierarchy
        var linkRequest = new SearchRequest(
            defaultNamingContext,
            "(gPLink=*)",
            SearchScope.Subtree,
            "distinguishedName", "gPLink", "gPOptions"
        );

        var linkResponse = (SearchResponse)await Task.Factory.FromAsync(connection.BeginSendRequest, connection.EndSendRequest, linkRequest, PartialResultProcessing.NoPartialResultSupport, null);

        foreach (SearchResultEntry entry in linkResponse.Entries)
        {
            var dn = entry.DistinguishedName;
            var gpLinkVal = entry.Attributes.Contains("gPLink") ? (string)entry.Attributes["gPLink"][0] : string.Empty;

            var matches = GpLinkRegex.Matches(gpLinkVal);
            int linkOrder = 1;

            // In AD gPLink string, links are ordered from lowest priority to highest priority or vice versa
            foreach (Match match in matches)
            {
                var targetGpoDn = match.Groups["dn"].Value;
                var optVal = match.Groups["options"].Value;
                int options = int.TryParse(optVal, out var o) ? o : 0;

                bool isLinkDisabled = (options & 1) != 0;
                bool isEnforced = (options & 2) != 0;

                // Extract GUID from target GPO DN
                var guidMatch = Regex.Match(targetGpoDn, @"\{(?<guid>[0-9A-Fa-f\-]+)\}");
                if (guidMatch.Success && Guid.TryParse(guidMatch.Groups["guid"].Value, out var linkedGuid))
                {
                    if (gpoMap.TryGetValue(linkedGuid, out var gpo))
                    {
                        gpo.Links.Add(new GpoLink
                        {
                            TargetContainerDn = dn,
                            Order = linkOrder++,
                            IsEnabled = !isLinkDisabled,
                            IsEnforced = isEnforced
                        });
                    }
                }
            }
        }

        report.TotalGpos = gpoMap.Count;
        report.UnlinkedGpos = gpoMap.Values.Where(g => g.Links.Count == 0).ToList();
        report.EmptyGpos = gpoMap.Values.Where(g => g.IsEmpty).ToList();
        report.DisabledGpos = gpoMap.Values.Where(g => g.IsCompletelyDisabled).ToList();
        report.VersionMismatches = gpoMap.Values.Where(g => g.HasVersionMismatch).ToList();

        // 4. Run conflict detection on each container with linked GPOs
        var containers = gpoMap.Values
            .SelectMany(g => g.Links.Select(l => l.TargetContainerDn))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var container in containers)
        {
            var applied = new List<(GroupPolicyObject Gpo, GpoLink Link, int ContainerDepth)>();
            int depth = container.Split(',').Length;

            foreach (var gpo in gpoMap.Values)
            {
                var link = gpo.Links.FirstOrDefault(l => l.TargetContainerDn.Equals(container, StringComparison.OrdinalIgnoreCase));
                if (link != null)
                {
                    applied.Add((gpo, link, depth));
                }
            }

            var ordered = GpoPrecedenceEngine.ResolvePrecedence(applied);
            var scopeConflicts = GpoConflictDetector.AnalyzeConflictsForScope(container, ordered);
            report.Conflicts.AddRange(scopeConflicts);
        }

        return report;
    }
}
