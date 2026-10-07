using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using FlowAD.Core.Models;
using FlowAD.Core.Reports;
using FlowAD.Dns;
using FlowAD.Rendering;

namespace FlowAD.Commands;

public sealed class DnsSettings : CommandSettings
{
    [Description("Target Active Directory Domain name (e.g. contoso.local)")]
    [CommandOption("-d|--domain <DOMAIN>")]
    public string? Domain { get; set; }

    [Description("Custom DNS server IP address (optional)")]
    [CommandOption("-s|--dns-server <IP>")]
    public string? DnsServer { get; set; }

    [Description("Output format: table, json, markdown")]
    [CommandOption("-f|--format <FORMAT>")]
    [DefaultValue("table")]
    public string Format { get; set; } = "table";

    [Description("Output file path")]
    [CommandOption("-o|--output <FILE>")]
    public string? OutputFile { get; set; }
}

public sealed class DnsCheckCommand : AsyncCommand<DnsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DnsSettings settings)
    {
        bool isJson = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            ConsoleRenderer.RenderBanner();
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        var dnsServers = !string.IsNullOrEmpty(settings.DnsServer) ? new[] { settings.DnsServer } : null;

        DnsAuditReport report = new() { DomainName = domain };

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Validating Active Directory DNS records for '{domain}'...", async _ =>
            {
                report = await AdSrvRecordValidator.AuditDomainDnsAsync(domain, dnsServers);
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
            ConsoleRenderer.RenderDnsAudit(report);
        }

        return report.OverallStatus == CheckStatus.Fail ? 1 : 0;
    }
}
