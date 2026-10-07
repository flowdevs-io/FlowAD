using System.ComponentModel;
using System.Net;
using Spectre.Console;
using Spectre.Console.Cli;
using FlowAD.Core.Models;
using FlowAD.Core.Reports;
using FlowAD.Gpo;
using FlowAD.Rendering;

namespace FlowAD.Commands;

public class GpoSettings : CommandSettings
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

    [Description("Output format: table, json, html, csv, markdown")]
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

public sealed class GpoInspectSettings : GpoSettings
{
    [Description("GPO Display Name or GUID to inspect")]
    [CommandOption("-n|--name <NAME>")]
    public string? Name { get; set; }
}

public sealed class GpoAuditCommand : AsyncCommand<GpoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GpoSettings settings)
    {
        bool isCleanOutput = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                            settings.Format.Equals("html", StringComparison.OrdinalIgnoreCase) ||
                            settings.Format.Equals("csv", StringComparison.OrdinalIgnoreCase);

        if (!isCleanOutput)
        {
            ConsoleRenderer.RenderBanner();
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        GpoAuditReport report;

        if (!string.IsNullOrEmpty(settings.OfflinePath))
        {
            if (!isCleanOutput)
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

            if (!isCleanOutput)
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

        // Format Dispatch
        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(report);
            await OutputContentAsync(json, settings.OutputFile);
        }
        else if (settings.Format.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            var html = HtmlReportWriter.GenerateGpoReport(report, domain);
            await OutputContentAsync(html, settings.OutputFile ?? "gpo-report.html");
            if (!isCleanOutput || string.IsNullOrEmpty(settings.OutputFile))
            {
                AnsiConsole.MarkupLine($"[green]Interactive HTML report generated:[/] {settings.OutputFile ?? "gpo-report.html"}");
            }
        }
        else if (settings.Format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = CsvReportWriter.GenerateConflictsCsv(report.Conflicts);
            await OutputContentAsync(csv, settings.OutputFile ?? "gpo-conflicts.csv");
            if (!isCleanOutput || string.IsNullOrEmpty(settings.OutputFile))
            {
                AnsiConsole.MarkupLine($"[green]CSV report generated:[/] {settings.OutputFile ?? "gpo-conflicts.csv"}");
            }
        }
        else
        {
            ConsoleRenderer.RenderGpoAudit(report);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, report);
                AnsiConsole.MarkupLine($"[green]Machine-readable copy written to:[/] {settings.OutputFile}");
            }
        }

        return report.OverallStatus == CheckStatus.Fail ? 1 : 0;
    }

    private static async Task OutputContentAsync(string content, string? outputFile)
    {
        if (!string.IsNullOrEmpty(outputFile))
        {
            var dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(outputFile, content);
        }
        else
        {
            Console.WriteLine(content);
        }
    }
}

public sealed class GpoDeconflictCommand : AsyncCommand<GpoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GpoSettings settings)
    {
        bool isCleanOutput = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                            settings.Format.Equals("html", StringComparison.OrdinalIgnoreCase) ||
                            settings.Format.Equals("csv", StringComparison.OrdinalIgnoreCase);

        if (!isCleanOutput)
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

        // Format Dispatch
        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(report.Conflicts);
            await OutputContentAsync(json, settings.OutputFile);
        }
        else if (settings.Format.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            var html = HtmlReportWriter.GenerateGpoReport(report, domain);
            await OutputContentAsync(html, settings.OutputFile ?? "gpo-deconfliction.html");
            if (!isCleanOutput || string.IsNullOrEmpty(settings.OutputFile))
            {
                AnsiConsole.MarkupLine($"[green]Interactive HTML deconfliction report written to:[/] {settings.OutputFile ?? "gpo-deconfliction.html"}");
            }
        }
        else if (settings.Format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = CsvReportWriter.GenerateConflictsCsv(report.Conflicts);
            await OutputContentAsync(csv, settings.OutputFile ?? "gpo-deconfliction.csv");
            if (!isCleanOutput || string.IsNullOrEmpty(settings.OutputFile))
            {
                AnsiConsole.MarkupLine($"[green]CSV deconfliction report written to:[/] {settings.OutputFile ?? "gpo-deconfliction.csv"}");
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

            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await JsonReportWriter.WriteToFileAsync(settings.OutputFile, report.Conflicts);
                AnsiConsole.MarkupLine($"[green]Machine-readable copy written to:[/] {settings.OutputFile}");
            }
        }

        return report.Conflicts.Any(c => c.Severity == Severity.Error || c.Severity == Severity.Critical) ? 1 : 0;
    }

    private static async Task OutputContentAsync(string content, string? outputFile)
    {
        if (!string.IsNullOrEmpty(outputFile))
        {
            var dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(outputFile, content);
        }
        else
        {
            Console.WriteLine(content);
        }
    }
}

public sealed class GpoInspectCommand : AsyncCommand<GpoInspectSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GpoInspectSettings settings)
    {
        bool isCleanOutput = settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                            settings.Format.Equals("csv", StringComparison.OrdinalIgnoreCase);

        if (!isCleanOutput)
        {
            ConsoleRenderer.RenderBanner();
        }

        if (string.IsNullOrEmpty(settings.OfflinePath) && string.IsNullOrEmpty(settings.Server))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please specify --offline-path <DIR> or --server <DC>.");
            return 1;
        }

        if (string.IsNullOrEmpty(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Please specify --name <NAME_OR_GUID> of the GPO to inspect.");
            return 1;
        }

        string domain = settings.Domain ?? Environment.GetEnvironmentVariable("USERDNSDOMAIN") ?? "local.domain";
        GpoAuditReport auditReport;

        if (!string.IsNullOrEmpty(settings.OfflinePath))
        {
            auditReport = GpoAuditService.AuditOfflineDirectory(settings.OfflinePath);
        }
        else
        {
            NetworkCredential? cred = null;
            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
            {
                cred = new NetworkCredential(settings.Username, settings.Password, domain);
            }
            auditReport = await GpoAuditService.AuditActiveDirectoryAsync(settings.Server!, domain, cred);
        }

        // Locate GPO by name or GUID substring
        var targetGpo = auditReport.UnlinkedGpos
            .Concat(auditReport.EmptyGpos)
            .Concat(auditReport.DisabledGpos)
            .Concat(auditReport.VersionMismatches)
            .FirstOrDefault(g => g.DisplayName.Contains(settings.Name, StringComparison.OrdinalIgnoreCase) ||
                                 g.Guid.ToString().Contains(settings.Name, StringComparison.OrdinalIgnoreCase));

        // If not found in special lists, check the all GPO map by inspecting directories
        if (targetGpo == null && !string.IsNullOrEmpty(settings.OfflinePath))
        {
            var allDirs = Directory.GetDirectories(settings.OfflinePath);
            foreach (var dir in allDirs)
            {
                var dirName = Path.GetFileName(dir);
                var gptIni = Path.Combine(dir, "GPT.ini");
                var (_, _, _, dispName) = GptIniReader.ReadFile(gptIni);
                var resolvedName = dispName ?? dirName;

                if (resolvedName.Contains(settings.Name, StringComparison.OrdinalIgnoreCase) || dirName.Contains(settings.Name, StringComparison.OrdinalIgnoreCase))
                {
                    targetGpo = new GroupPolicyObject
                    {
                        Guid = Guid.TryParse(dirName.Trim('{', '}'), out var g) ? g : Guid.NewGuid(),
                        DisplayName = resolvedName,
                        SysvolPath = dir
                    };

                    var machPol = Path.Combine(dir, "Machine", "Registry.pol");
                    if (File.Exists(machPol))
                        targetGpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(machPol, targetGpo.Guid, targetGpo.DisplayName, PolicyScope.Computer));

                    var userPol = Path.Combine(dir, "User", "Registry.pol");
                    if (File.Exists(userPol))
                        targetGpo.RegistrySettings.AddRange(RegistryPolReader.ReadFile(userPol, targetGpo.Guid, targetGpo.DisplayName, PolicyScope.User));

                    var secInf = Path.Combine(dir, "Machine", "Microsoft", "Windows NT", "SecEdit", "GptTmpl.inf");
                    if (File.Exists(secInf))
                        targetGpo.SecuritySettings.AddRange(GptTmplReader.ReadFile(secInf, targetGpo.Guid, targetGpo.DisplayName, PolicyScope.Computer));

                    break;
                }
            }
        }

        if (targetGpo == null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] GPO matching '{settings.Name}' was not found.");
            return 1;
        }

        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonReportWriter.Serialize(targetGpo);
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                await File.WriteAllTextAsync(settings.OutputFile, json);
                AnsiConsole.MarkupLine($"[green]GPO inspection JSON written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
            return 0;
        }

        // Render Inspector Table
        AnsiConsole.MarkupLine($"[bold cyan]=== Detailed Policy Inspector: {Markup.Escape(targetGpo.DisplayName)} ===[/]");
        AnsiConsole.MarkupLine($"[grey]GUID:[/] {targetGpo.Guid} | [grey]Version (GPT/GPC):[/] {targetGpo.GptVersion}/{targetGpo.GpcVersion} | [grey]Settings Count:[/] {targetGpo.RegistrySettings.Count + targetGpo.SecuritySettings.Count}");
        AnsiConsole.WriteLine();

        if (targetGpo.SecuritySettings.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Security Template Settings & User Rights Assignments:[/]");
            var secTable = new Table().Border(TableBorder.Rounded);
            secTable.AddColumn("[bold]Section[/]");
            secTable.AddColumn("[bold]Setting / Privilege[/]");
            secTable.AddColumn("[bold]Assigned Identities / Value[/]");

            foreach (var sec in targetGpo.SecuritySettings)
            {
                secTable.AddRow(
                    Markup.Escape(sec.Section),
                    Markup.Escape(sec.SettingName),
                    Markup.Escape(string.Join(", ", sec.AssignedIdentities))
                );
            }

            AnsiConsole.Write(secTable);
            AnsiConsole.WriteLine();
        }

        if (targetGpo.RegistrySettings.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold cyan]Administrative Templates & Registry Settings:[/]");
            var regTable = new Table().Border(TableBorder.Rounded);
            regTable.AddColumn("[bold]Scope[/]");
            regTable.AddColumn("[bold]Hive[/]");
            regTable.AddColumn("[bold]Registry Key Path[/]");
            regTable.AddColumn("[bold]Value Name[/]");
            regTable.AddColumn("[bold]Type[/]");
            regTable.AddColumn("[bold]Value[/]");

            foreach (var reg in targetGpo.RegistrySettings)
            {
                regTable.AddRow(
                    reg.Scope.ToString(),
                    reg.Hive,
                    Markup.Escape(reg.KeyPath),
                    Markup.Escape(reg.ValueName),
                    reg.Type.ToString(),
                    Markup.Escape(reg.Value?.ToString() ?? "<null>")
                );
            }

            AnsiConsole.Write(regTable);
            AnsiConsole.WriteLine();
        }

        if (targetGpo.IsEmpty)
        {
            AnsiConsole.MarkupLine("[yellow]Notice: This GPO contains zero configured registry or security settings.[/]");
        }

        return 0;
    }
}
