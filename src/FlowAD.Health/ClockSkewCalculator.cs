using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;

namespace FlowAD.Health;

public static class ClockSkewCalculator
{
    /// <summary>
    /// Measures clock skew between local machine and DC using LDAP RootDSE currentTime attribute.
    /// Returns skew in seconds (positive means DC is ahead, negative means DC is behind).
    /// </summary>
    public static async Task<double> MeasureLdapClockSkewAsync(
        string domainController,
        NetworkCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ldapIdentifier = new LdapDirectoryIdentifier(domainController, 389);
            using var connection = credential == null
                ? new LdapConnection(ldapIdentifier)
                : new LdapConnection(ldapIdentifier, credential, AuthType.Negotiate);

            connection.Timeout = TimeSpan.FromSeconds(5);
            connection.Bind();

            var request = new SearchRequest("", "(objectClass=*)", SearchScope.Base, "currentTime");
            var beforeQuery = DateTime.UtcNow;
            
            var response = (SearchResponse)await Task.Factory.FromAsync(connection.BeginSendRequest, connection.EndSendRequest, request, PartialResultProcessing.NoPartialResultSupport, null);
            var afterQuery = DateTime.UtcNow;

            var roundTripMidpoint = beforeQuery + TimeSpan.FromTicks((afterQuery - beforeQuery).Ticks / 2);

            if (response.Entries.Count > 0 && response.Entries[0].Attributes.Contains("currentTime"))
            {
                var timeStr = (string)response.Entries[0].Attributes["currentTime"][0];
                // LDAP currentTime format: yyyyMMddHHmmss.0Z
                if (DateTime.TryParseExact(timeStr, "yyyyMMddHHmmss.0'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dcTime))
                {
                    return (dcTime - roundTripMidpoint).TotalSeconds;
                }
            }
        }
        catch
        {
            // Fallback or uncontactable
        }

        return 0.0;
    }
}
