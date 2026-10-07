using System.ComponentModel;
using System.Net;
using Spectre.Console;
using Spectre.Console.Cli;
using FlowAD.Core.Models;
using FlowAD.Core.Reports;
using FlowAD.Health;
using FlowAD.Rendering;

namespace FlowAD.Commands;

public sealed class HealthSettings : CommandSettings
{
    [Description("Target Active Directory Domain name (e.g. contoso.local)")]
    [CommandOption("-d|--domain <DOMAIN>")]
    public string? Domain { get; set; }

    [Description("Domain Controller host or IP address")]
    [CommandOption("-s|--server|--dc <SERVER>")]
    public string? Server { get; set; }

    [Description("Output format: table, json, markdown")]
    [CommandOption("-f|--format <FORMAT>")]
    [DefaultValue("table")]
    public string Format { get; set; } = "table";

    [Description("Output file path")]
    [CommandOption("-o|--output <FILE>")]
    public string? OutputFile { get; set; }

    [Description("Username for Active Directory authentication")]
    [CommandOption("-u|--user <USERNAME>")]
    public string? Username { get; set; }

    [Description("Password for Active Directory authentication")]
    [CommandOption("-p|--password <PASSWORD>")]
    public string? Password { get; set; }
}

public sealed class HealthCommand : AsyncCommand<HealthSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HealthSettings settings)
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

        DomainHealthReport report = new() { DomainName = domain };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Auditing Domain Health for '{domain}'...", async _ =>
            {
                var dcs = new List<(string Host, string Ip)>();
                if (!string.IsNullOrEmpty(server))
                {
                    dcs.Add((server, server));
                }
                report = await DomainHealthService.AuditDomainHealthAsync(domain, dcs, cred);
            });

        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(report);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, report);
                AnsiConsole.MarkupLine($"[green]Report written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        else
        {
            ConsoleRenderer.RenderDomainHealth(report);
        }

        return report.OverallStatus == CheckStatus.Fail ? 1 : 0;
    }
}
