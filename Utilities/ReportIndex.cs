using System.Text;
using static MobileAutomation.Tests.Reporting.QrBaselineReport;

namespace MobileAutomation.Tests.Reporting;

// Reports/index.html: one entry point to the generated reports, each with what it covers and when it was last
// written. A report is listed only once some run has produced it
public static class ReportIndex
{
    private record Entry(string File, string Title, string Covers);

    private static readonly Entry[] Reports =
    [
        new("NearbyR1Report_latest.html", "Nearby R1 Requirements Report",
            "Feature #10082 Release 1: each NDT requirement (Driver App discoverability, Flex discovery menu, identity cards, " +
            "supported clients, runtime access, transfer progress) with the tests that check it and whether it is met."),
        new("QrBaselineReport_latest.html", "QR Baseline & BLE Readiness Report",
            "BLE Ticket Handoff POC: the QR handoff baselines (QR-A01, QR-B01…B10), their BLE counterparts (TC-12 / TC-13) " +
            "and the devices' Bluetooth readiness.")
    ];

    // directory: the reports folder; the shared TestResults/Reports when omitted (a test run)
    public static string Generate(string? directory = null)
    {
        directory ??= ReportPaths.Directory;

        var html = new StringBuilder($$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Test Reports</title>
<style>{{Css}}</style>
</head>
<body>
<h1>RecoveryTrac Mobile Automation · Test Reports</h1>
<div class="meta">Index generated {{DateTime.Now:yyyy-MM-dd HH:mm}} on {{E(Environment.MachineName)}}</div>
<table><tr><th>Report</th><th>What it covers</th><th>Last updated</th></tr>
""");
        foreach (var report in Reports)
        {
            string path = Path.Combine(directory, report.File);
            string updated = File.Exists(path) ? $"{File.GetLastWriteTime(path):yyyy-MM-dd HH:mm}" : "not generated yet";
            string title = File.Exists(path) ? $"<a href=\"{E(report.File)}\">{E(report.Title)}</a>" : E(report.Title);
            html.Append($"<tr><td><b>{title}</b></td><td>{E(report.Covers)}</td><td>{updated}</td></tr>");
        }
        html.Append("</table></body></html>");

        string indexPath = Path.Combine(directory, "index.html");
        File.WriteAllText(indexPath, html.ToString());
        Console.WriteLine($"📊 Report index: {indexPath}");
        return indexPath;
    }
}
