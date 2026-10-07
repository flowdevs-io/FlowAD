using System.ComponentModel;
using System.Net;
using Spectre.Console;
using Spectre.Console.Cli;
using FlowAD.Core.Models;
using FlowAD.Core.Reports;
using FlowAD.Gpo;
using FlowAD.Rendering;

namespace FlowAD.Commands;

public sealed class GpoSettings : CommandSettings
{
    [Description("Target Active Directory Domain name")]
    [CommandOption("-d|--domain <DOMAIN>")]
    public string? Domain { get; set; }

    [Description("Domain Controller host or IP")]
    [CommandOption("-s|--server|--dc <SERVER>")]
    public string? Server { get; set; }

    [Description("Path to offline SYSVOL Policies folder or GPO backups")]
    [CommandOption("--offline-path <PATH>")]
    public string? OfflinePath { get; set; }

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

public sealed class GpoAuditCommand : AsyncCommand<GpoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GpoSettings settings)
    {
        bool isJson = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            ConsoleRenderer.RenderBanner();
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        GpoAuditReport report;

        if (!string.IsNullOrEmpty(settings.OfflinePath))
        {
            if (!isJson)
            {
                AnsiConsole.MarkupLine($"[cyan]Running offline GPO audit on directory:[/] {settings.OfflinePath}");
            }
            report = GpoAuditService.AuditOfflineDirectory(settings.OfflinePath);
        }
        else if (!string.IsNullOrEmpty(settings.Server))
        {
            NetworkCredential? cred = null;
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
            {
                cred = new NetworkCredential(settings.Username, settings.Password, domain);
            }

            if (!isJson)
            {
                AnsiConsole.MarkupLine($"[cyan]Querying Active Directory GPOs from DC:[/] {settings.Server}");
            }
            report = await GpoAuditService.AuditActiveDirectoryAsync(settings.Server, domain, cred);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please specify either --server <DC> for live query or --offline-path <DIR> for offline analysis.");
            return 1;
        }

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
            ConsoleRenderer.RenderGpoAudit(report);
        }

        return report.OverallStatus == CheckStatus.Fail ? 1 : 0;
    }
}

public sealed class GpoDeconflictCommand : AsyncCommand<GpoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GpoSettings settings)
    {
        bool isJson = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            ConsoleRenderer.RenderBanner();
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        GpoAuditReport report;

        if (!string.IsNullOrEmpty(settings.OfflinePath))
        {
            report = GpoAuditService.AuditOfflineDirectory(settings.OfflinePath);
        }
        else if (!string.IsNullOrEmpty(settings.Server))
        {
            NetworkCredential? cred = null;
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
            {
                cred = new NetworkCredential(settings.Username, settings.Password, domain);
            }
            report = await GpoAuditService.AuditActiveDirectoryAsync(settings.Server, domain, cred);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please specify either --server <DC> or --offline-path <DIR>.");
            return 1;
        }

        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(report.Conflicts);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, report.Conflicts);
                AnsiConsole.MarkupLine($"[green]Report written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        else
        {
            if (report.Conflicts.Count == 0)
            {
                AnsiConsole.MarkupLine("[bold green]Zero GPO collisions detected! All evaluated policy settings are clean.[/]");
            }
            else
            {
                ConsoleRenderer.RenderGpoConflicts(report.Conflicts);
            }
        }

        return report.Conflicts.Any(c => c.Severity == Severity.Error || c.Severity == Severity.Critical) ? 1 : 0;
    }
}
