using System.ComponentModel;
using System.Net;
using Spectre.Console;
using Spectre.Console.Cli;
using FlowAD.Core.Models;
using FlowAD.Core.Reports;
using FlowAD.Dns;
using FlowAD.Gpo;
using FlowAD.Health;
using FlowAD.Rendering;

namespace FlowAD.Commands;

public sealed class DoctorSettings : CommandSettings
{
    [Description("Target Active Directory Domain name (e.g. contoso.local)")]
    [CommandOption("-d|--domain <DOMAIN>")]
    public string? Domain { get; set; }

    [Description("Domain Controller host or IP address")]
    [CommandOption("-s|--server|--dc <SERVER>")]
    public string? Server { get; set; }

    [Description("Optional path to offline SYSVOL Policies directory")]
    [CommandOption("--offline-path <PATH>")]
    public string? OfflinePath { get; set; }

    [Description("Output format: table, json, markdown")]
    [CommandOption("-f|--format <FORMAT>")]
    [DefaultValue("table")]
    public string Format { get; set; } = "table";

    [Description("Save report output to file")]
    [CommandOption("-o|--output <FILE>")]
    public string? OutputFile { get; set; }

    [Description("Username for Active Directory authentication")]
    [CommandOption("-u|--user <USERNAME>")]
    public string? Username { get; set; }

    [Description("Password for Active Directory authentication")]
    [CommandOption("-p|--password <PASSWORD>")]
    public string? Password { get; set; }
}

public sealed class DoctorCommand : AsyncCommand<DoctorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DoctorSettings settings)
    {
        bool isJson = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            ConsoleRenderer.RenderBanner();
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        string? server = settings.Server;

        NetworkCredential? cred = null;
        if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
        {
            cred = new NetworkCredential(settings.Username, settings.Password, domain);
        }

        var doctorReport = new DoctorReport
        {
            DomainName = domain
        };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Running FlowAD Diagnostic Battery...", async ctx =>
            {
                // 1. GPO Diagnostic
                ctx.Status("[bold cyan]Analyzing Group Policy Objects (GPOs) & Deconfliction...[/]");
                if (!string.IsNullOrEmpty(settings.OfflinePath) && Directory.Exists(settings.OfflinePath))
                {
                    doctorReport.GpoReport = GpoAuditService.AuditOfflineDirectory(settings.OfflinePath);
                }
                else if (!string.IsNullOrEmpty(server))
                {
                    try
                    {
                        doctorReport.GpoReport = await GpoAuditService.AuditActiveDirectoryAsync(server, domain, cred);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Notice:[/] Live GPO LDAP query failed ({ex.Message}). Continuing with DNS and Domain Health checks.");
                    }
                }

                // 2. DNS Diagnostic
                ctx.Status("[bold cyan]Validating Active Directory DNS SRV Records & Consistency...[/]");
                try
                {
                    var dnsServers = !string.IsNullOrEmpty(server) ? new[] { server } : null;
                    doctorReport.DnsReport = await AdSrvRecordValidator.AuditDomainDnsAsync(domain, dnsServers);
                }
                catch (Exception ex)
                {
                    doctorReport.DnsReport.OverallStatus = CheckStatus.Fail;
                    AnsiConsole.MarkupLine($"[yellow]DNS check error:[/] {ex.Message}");
                }

                // 3. Domain Health
                ctx.Status("[bold cyan]Testing Domain Controller Reachability, FSMO & Clock Skew...[/]");
                try
                {
                    var dcs = new List<(string Host, string Ip)>();
                    if (!string.IsNullOrEmpty(server))
                    {
                        dcs.Add((server, server));
                    }
                    doctorReport.HealthReport = await DomainHealthService.AuditDomainHealthAsync(domain, dcs, cred);
                }
                catch (Exception ex)
                {
                    doctorReport.HealthReport.OverallStatus = CheckStatus.Fail;
                    AnsiConsole.MarkupLine($"[yellow]Domain health error:[/] {ex.Message}");
                }
            });

        // Output handling
        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(doctorReport);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, doctorReport);
                AnsiConsole.MarkupLine($"[green]Report written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        else if (settings.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase) || settings.Format.Equals("md", StringComparison.OrdinalIgnoreCase))
        {
            var md = MarkdownReportWriter.GenerateDoctorReport(doctorReport);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await File.WriteAllTextAsync(settings.OutputFile, md);
                AnsiConsole.MarkupLine($"[green]Report written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(md);
            }
        }
        else
        {
            ConsoleRenderer.RenderDoctorDashboard(doctorReport);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, doctorReport);
                AnsiConsole.MarkupLine($"[green]Machine-readable copy written to:[/] {settings.OutputFile}");
            }
        }

        return doctorReport.OverallStatus == CheckStatus.Fail ? 1 : 0;
    }
}
