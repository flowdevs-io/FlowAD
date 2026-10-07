using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.RegularExpressions;
using FlowAD.Core.Models;

namespace FlowAD.Health;

public static class FsmoRoleInspector
{
    public static async Task<FsmoRolesReport> InspectFsmoRolesAsync(
        string domainController,
        NetworkCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        var report = new FsmoRolesReport();

        try
        {
            var ldapIdentifier = new LdapDirectoryIdentifier(domainController, 389);
            using var connection = credential == null
                ? new LdapConnection(ldapIdentifier)
                : new LdapConnection(ldapIdentifier, credential, AuthType.Negotiate);

            connection.Timeout = TimeSpan.FromSeconds(10);
            connection.Bind();

            // 1. Get RootDSE
            var rootDseReq = new SearchRequest("", "(objectClass=*)", SearchScope.Base, "defaultNamingContext", "configurationNamingContext", "schemaNamingContext");
            var rootDseResp = (SearchResponse)await Task.Factory.FromAsync(connection.BeginSendRequest, connection.EndSendRequest, rootDseReq, PartialResultProcessing.NoPartialResultSupport, null);

            if (rootDseResp.Entries.Count == 0)
            {
                report.IsConsensusReached = false;
                return report;
            }

            var defaultNc = (string)rootDseResp.Entries[0].Attributes["defaultNamingContext"][0];
            var configNc = (string)rootDseResp.Entries[0].Attributes["configurationNamingContext"][0];
            var schemaNc = (string)rootDseResp.Entries[0].Attributes["schemaNamingContext"][0];

            // 2. Schema Master (fSMORoleOwner on schema partition)
            report.SchemaMaster = await QueryRoleOwnerAsync(connection, schemaNc);

            // 3. Domain Naming Master (fSMORoleOwner on CN=Partitions,configNc)
            report.DomainNamingMaster = await QueryRoleOwnerAsync(connection, $"CN=Partitions,{configNc}");

            // 4. PDC Emulator (fSMORoleOwner on domain root)
            report.PdcEmulator = await QueryRoleOwnerAsync(connection, defaultNc);

            // 5. RID Master (fSMORoleOwner on CN=RID Manager$,CN=System,defaultNc)
            report.RidMaster = await QueryRoleOwnerAsync(connection, $"CN=RID Manager$,CN=System,{defaultNc}");

            // 6. Infrastructure Master (fSMORoleOwner on CN=Infrastructure,defaultNc)
            report.InfrastructureMaster = await QueryRoleOwnerAsync(connection, $"CN=Infrastructure,{defaultNc}");

            report.IsConsensusReached = !string.IsNullOrEmpty(report.PdcEmulator) && !string.IsNullOrEmpty(report.SchemaMaster);
        }
        catch
        {
            report.IsConsensusReached = false;
        }

        return report;
    }

    private static async Task<string> QueryRoleOwnerAsync(LdapConnection conn, string dn)
    {
        try
        {
            var req = new SearchRequest(dn, "(objectClass=*)", SearchScope.Base, "fSMORoleOwner");
            var resp = (SearchResponse)await Task.Factory.FromAsync(conn.BeginSendRequest, conn.EndSendRequest, req, PartialResultProcessing.NoPartialResultSupport, null);
            if (resp.Entries.Count > 0 && resp.Entries[0].Attributes.Contains("fSMORoleOwner"))
            {
                var ownerDn = (string)resp.Entries[0].Attributes["fSMORoleOwner"][0];
                // Extract CN of server: CN=NTDS Settings,CN=DC01,CN=Servers,CN=Default-First-Site-Name,CN=Sites...
                var match = Regex.Match(ownerDn, @"CN=NTDS Settings,CN=(?<dc>[^,]+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups["dc"].Value : ownerDn;
            }
        }
        catch
        {
            // Ignore
        }
        return "Unknown";
    }
}
