using Spectre.Console.Cli;
using FlowAD.Commands;

namespace FlowAD;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("flowad");

            config.AddCommand<DoctorCommand>("doctor")
                .WithDescription("Run the complete Active Directory health, GPO deconfliction, and DNS diagnostic battery.");

            config.AddBranch("gpo", gpo =>
            {
                gpo.SetDescription("Audit Group Policy Objects (GPOs) and evaluate precedence collisions.");
                
                gpo.AddCommand<GpoAuditCommand>("audit")
                    .WithDescription("Audit GPO hygiene, replication version skew, empty, and unlinked policies.");

                gpo.AddCommand<GpoDeconflictCommand>("deconflict")
                    .WithDescription("Evaluate LSDOU precedence and detect conflicting registry settings & user rights assignments.");

                gpo.AddCommand<GpoDeconflictCommand>("conflicts")
                    .WithDescription("Alias for deconflict.");

                gpo.AddCommand<GpoInspectCommand>("inspect")
                    .WithDescription("Inspect and dump every parsed setting within a specific GPO.");
            });

            config.AddBranch("dns", dns =>
            {
                dns.SetDescription("Active Directory DNS diagnostics, SRV records, and DC PTR consistency.");

                dns.AddCommand<DnsCheckCommand>("check")
                    .WithDescription("Verify AD SRV records, DC forward A/AAAA records, and reverse PTR lookups.");
            });

            config.AddBranch("health", health =>
            {
                health.SetDescription("Domain controller connectivity, FSMO consensus, and clock skew.");

                health.AddCommand<HealthCommand>("domain")
                    .WithDescription("Check DC reachability, port availability, FSMO roles, and Kerberos time sync.");
            });
        });

        // If no args provided, show help
        if (args.Length == 0)
        {
            return await app.RunAsync(["--help"]);
        }

        return await app.RunAsync(args);
    }
}
