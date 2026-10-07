using Spectre.Console;
using FlowAD.Core.Models;

namespace FlowAD.Rendering;

public static class ConsoleRenderer
{
    public static void RenderBanner()
    {
        AnsiConsole.Write(
            new FigletText("FlowAD")
                .Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold cyan]FlowAD Diagnostic Suite[/] [grey]| FlowDevs Certified Active Directory Engine v1.0.0[/]");
        AnsiConsole.MarkupLine("[grey]Lightweight Active Directory, GPO Deconfliction, and DNS Health Diagnostic Suite[/]\n");
    }

    public static void RenderDoctorDashboard(DoctorReport report)
    {
        var scoreColor = report.OverallScore switch
        {
            >= 85 => "green",
            >= 60 => "yellow",
            _ => "red"
        };

        var panel = new Panel(new Markup($"[bold]Target Domain:[/] [cyan]{report.DomainName}[/]\n" +
                                         $"[bold]Timestamp:[/] {report.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
                                         $"[bold]Overall Health Score:[/] [{scoreColor} bold]{report.OverallScore}/100[/] ([{scoreColor}]{report.OverallStatus}[/])\n\n" +
                                         $"[bold]GPO Engine:[/] {FormatStatus(report.GpoReport.OverallStatus)} ({report.GpoReport.TotalGpos} GPOs, {report.GpoReport.Conflicts.Count} conflicts)\n" +
                                         $"[bold]DNS Engine:[/] {FormatStatus(report.DnsReport.OverallStatus)} ({report.DnsReport.SrvRecordChecks.Count} SRV tests, {report.DnsReport.AverageLatencyMs:F1}ms avg)\n" +
                                         $"[bold]Domain Health:[/] {FormatStatus(report.HealthReport.OverallStatus)} ({report.HealthReport.Dcs.Count} DCs, FSMO: {(report.HealthReport.FsmoRoles.IsConsensusReached ? "[green]OK[/]" : "[red]SPLIT-BRAIN[/]")})"))
        {
            Header = new PanelHeader("[bold white]Executive Health Dashboard[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        RenderGpoAudit(report.GpoReport);
        RenderDnsAudit(report.DnsReport);
        RenderDomainHealth(report.HealthReport);
    }

    public static void RenderGpoAudit(GpoAuditReport report)
    {
        AnsiConsole.MarkupLine("[bold cyan]=== Group Policy (GPO) Hygiene & Audit ===[/]");

        var summaryTable = new Table().Border(TableBorder.Rounded);
        summaryTable.AddColumn("[bold]Metric[/]");
        summaryTable.AddColumn("[bold]Value[/]");
        summaryTable.AddColumn("[bold]Status[/]");

        summaryTable.AddRow("Total GPOs Analyzed", report.TotalGpos.ToString(), "[blue]Info[/]");
        summaryTable.AddRow("Unlinked GPOs", report.UnlinkedGpos.Count.ToString(), report.UnlinkedGpos.Count > 0 ? "[yellow]Warning (Orphan)[/]" : "[green]Clean[/]");
        summaryTable.AddRow("AD / SYSVOL Version Mismatches", report.VersionMismatches.Count.ToString(), report.VersionMismatches.Count > 0 ? "[red]Critical (Replication Desync)[/]" : "[green]In Sync[/]");
        summaryTable.AddRow("Empty GPOs", report.EmptyGpos.Count.ToString(), report.EmptyGpos.Count > 0 ? "[yellow]Bloat[/]" : "[green]Clean[/]");
        summaryTable.AddRow("Disabled GPOs", report.DisabledGpos.Count.ToString(), "[grey]Notice[/]");
        summaryTable.AddRow("Detected Policy Conflicts", report.Conflicts.Count.ToString(), report.Conflicts.Count > 0 ? "[yellow]Action Required[/]" : "[green]No Collisions[/]");

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        if (report.Conflicts.Count > 0)
        {
            RenderGpoConflicts(report.Conflicts);
        }
    }

    public static void RenderGpoConflicts(List<GpoConflictResult> conflicts)
    {
        AnsiConsole.MarkupLine("[bold yellow]=== GPO Conflict & Precedence Deconfliction Matrix ===[/]");

        var conflictTable = new Table().Border(TableBorder.Rounded);
        conflictTable.AddColumn("[bold]Scope[/]");
        conflictTable.AddColumn("[bold]Setting / Key[/]");
        conflictTable.AddColumn("[bold]Type[/]");
        conflictTable.AddColumn("[bold green]Winning GPO (Value)[/]");
        conflictTable.AddColumn("[bold red]Losing GPO (Value)[/]");
        conflictTable.AddColumn("[bold]Severity[/]");

        foreach (var c in conflicts)
        {
            var sevMarkup = c.Severity switch
            {
                Severity.Critical => "[bold red]CRITICAL[/]",
                Severity.Error => "[red]ERROR[/]",
                Severity.Warning => "[yellow]WARN[/]",
                _ => "[blue]INFO[/]"
            };

            var typeMarkup = c.ConflictType switch
            {
                ConflictType.DirectContradiction => "[red bold]Contradiction[/]",
                ConflictType.UserRightsCollision => "[bold red]Privilege Overwrite[/]",
                ConflictType.DuplicateSetting => "[grey]Duplicate[/]",
                _ => "[yellow]Overwrite[/]"
            };

            conflictTable.AddRow(
                Markup.Escape(c.TargetScope),
                Markup.Escape(c.SettingIdentifier),
                typeMarkup,
                $"[green]{Markup.Escape(c.WinningGpo)}[/]\n[grey]({Markup.Escape(c.WinningValue)})[/]",
                $"[red]{Markup.Escape(c.LosingGpo)}[/]\n[grey]({Markup.Escape(c.LosingValue)})[/]",
                sevMarkup
            );
        }

        AnsiConsole.Write(conflictTable);
        AnsiConsole.WriteLine();
    }

    public static void RenderDnsAudit(DnsAuditReport report)
    {
        AnsiConsole.MarkupLine("[bold cyan]=== Active Directory DNS Health & SRV Validation ===[/]");
        AnsiConsole.MarkupLine($"[grey]Authoritative Servers Queried: {string.Join(", ", report.DnsServersQueried)} | Avg Latency: {report.AverageLatencyMs:F1}ms[/]");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]SRV Record[/]");
        table.AddColumn("[bold]Service Purpose[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Target Endpoints[/]");
        table.AddColumn("[bold]Latency[/]");

        foreach (var srv in report.SrvRecordChecks)
        {
            var targets = string.Join("\n", srv.Endpoints.Select(e => $"{e.TargetHost}:{e.Port} [grey]({string.Join(",", e.ResolvedIps)})[/]"));
            if (string.IsNullOrEmpty(targets)) targets = "[red]None[/]";

            table.AddRow(
                Markup.Escape(srv.RecordName),
                Markup.Escape(srv.Purpose),
                FormatStatus(srv.Status),
                targets,
                $"{srv.LatencyMs:F1} ms"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (report.ConsistencyChecks.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold cyan]=== Domain Controller Reverse DNS (PTR) Consistency ===[/]");
            var ptrTable = new Table().Border(TableBorder.Rounded);
            ptrTable.AddColumn("[bold]DC Host[/]");
            ptrTable.AddColumn("[bold]IP Address[/]");
            ptrTable.AddColumn("[bold]Forward A[/]");
            ptrTable.AddColumn("[bold]Reverse PTR[/]");
            ptrTable.AddColumn("[bold]Status / Notes[/]");

            foreach (var check in report.ConsistencyChecks)
            {
                ptrTable.AddRow(
                    Markup.Escape(check.DcHost),
                    Markup.Escape(check.IpAddress),
                    check.HasForwardRecord ? "[green]Resolved[/]" : "[red]Missing[/]",
                    check.HasReversePtr ? $"[green]{Markup.Escape(check.PtrResolvedHost ?? "")}[/]" : "[yellow]Missing[/]",
                    $"{FormatStatus(check.Status)} {Markup.Escape(check.Notes)}"
                );
            }

            AnsiConsole.Write(ptrTable);
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderDomainHealth(DomainHealthReport report)
    {
        AnsiConsole.MarkupLine("[bold cyan]=== Domain Controller Health & Connectivity ===[/]");

        var dcTable = new Table().Border(TableBorder.Rounded);
        dcTable.AddColumn("[bold]DC Host[/]");
        dcTable.AddColumn("[bold]IP Address[/]");
        dcTable.AddColumn("[bold]Reachable[/]");
        dcTable.AddColumn("[bold]Clock Skew[/]");
        dcTable.AddColumn("[bold]SYSVOL / Netlogon[/]");
        dcTable.AddColumn("[bold]Key Ports (DNS,KDC,RPC,LDAP,SMB)[/]");
        dcTable.AddColumn("[bold]Status[/]");

        foreach (var dc in report.Dcs)
        {
            var portsSummary = string.Join(" ", dc.PortStatus.Select(kv => kv.Value ? $"[green]{kv.Key}[/]" : $"[red]!{kv.Key}[/]"));
            var skewColor = Math.Abs(dc.ClockSkewSeconds) > 3 ? "yellow" : "green";

            dcTable.AddRow(
                Markup.Escape(dc.DcName),
                Markup.Escape(dc.IpAddress),
                dc.IsReachable ? "[green]Online[/]" : "[red]Unreachable[/]",
                $"[{skewColor}]{dc.ClockSkewSeconds:F2}s[/]",
                dc.SysvolShareAccessible ? "[green]Accessible[/]" : "[red]Failed[/]",
                portsSummary,
                FormatStatus(dc.OverallStatus)
            );
        }

        AnsiConsole.Write(dcTable);
        AnsiConsole.WriteLine();

        // FSMO Roles
        var fsmoPanel = new Panel(
            new Markup($"[bold]PDC Emulator:[/] [cyan]{Markup.Escape(report.FsmoRoles.PdcEmulator)}[/]\n" +
                       $"[bold]Schema Master:[/] [cyan]{Markup.Escape(report.FsmoRoles.SchemaMaster)}[/]\n" +
                       $"[bold]Domain Naming Master:[/] [cyan]{Markup.Escape(report.FsmoRoles.DomainNamingMaster)}[/]\n" +
                       $"[bold]RID Master:[/] [cyan]{Markup.Escape(report.FsmoRoles.RidMaster)}[/]\n" +
                       $"[bold]Infrastructure Master:[/] [cyan]{Markup.Escape(report.FsmoRoles.InfrastructureMaster)}[/]\n" +
                       $"[bold]Consensus State:[/] {(report.FsmoRoles.IsConsensusReached ? "[green]Consensus Validated[/]" : "[red]Consensus Divergence / Unreachable Roles[/]")}"))
        {
            Header = new PanelHeader("[bold white]Active Directory FSMO Role Holders[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(fsmoPanel);
        AnsiConsole.WriteLine();
    }

    private static string FormatStatus(CheckStatus status) => status switch
    {
        CheckStatus.Pass => "[bold green]PASS[/]",
        CheckStatus.Warning => "[bold yellow]WARN[/]",
        CheckStatus.Fail => "[bold red]FAIL[/]",
        _ => "[grey]SKIP[/]"
    };
}
