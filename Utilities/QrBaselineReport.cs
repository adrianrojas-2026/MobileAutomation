using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MobileAutomation.Tests.Utilities;

namespace MobileAutomation.Tests.Reporting;

// One executed test as the final report shows it
public record BaselineTestResult(
    string TestName,
    IReadOnlyList<string> ScenarioIds,
    string Node,
    string Outcome,
    string Message,
    double DurationSeconds,
    IReadOnlyList<string> BaselinePaths,
    IReadOnlyList<string> EvidencePaths,
    DateTime FinishedAt);

// Final report of a QR baseline / BLE readiness run: every test's outcome, the baselines it recorded (the expected
// results for the BLE handoff tests), failure screenshots, the devices' Bluetooth readiness, and coverage of the
// BLE POC requirements. Tests register themselves (StartTest / RecordTest); ReportSetUpFixture writes it once
// after the whole run.
public static class QrBaselineReport
{
    // Part A scenarios of docs/testing/test-scenarios-ble-ticket-handoff.md
    private record Scenario(string Title, string Requirements, string BleCounterpart);

    private static readonly Dictionary<string, Scenario> Scenarios = new()
    {
        ["QR-A01"] = new("QR E2E: collection → Driver → disposal → receipt", "Check 5", "TC-27"),
        ["QR-B01"] = new("Collection QR payload fingerprint (≤ 4,200 chars)", "FR-4", "TC-13, TC-14"),
        ["QR-B02"] = new("Driver load after a QR scan", "FR-6", "TC-12, TC-13"),
        ["QR-B03"] = new("Driver load persists after an app restart", "FR-6", "TC-18"),
        ["QR-B04"] = new("Second ticket while a load is active", "FR-6, Check 4", "TC-17"),
        ["QR-B05"] = new("Same collection QR scanned twice", "FR-7", "TC-22"),
        ["QR-B06"] = new("Non-ticket QR is rejected, nothing saved", "FR-5, Check 3", "TC-15"),
        ["QR-B07"] = new("Damaged ticket payload handling", "FR-5, Check 3", "TC-15, TC-16"),
        ["QR-B08"] = new("QR reopens from the submitted card with the same payload", "FR-7", "TC-19, TC-22"),
        ["QR-B09"] = new("QR handoff with both devices in airplane mode", "Demo env", "TC-29"),
        ["QR-B10"] = new("Device Bluetooth readiness", "Risk: BLE support", "Lanes E / D"),
        // Part B: the BLE handoff itself; the last column names the QR baseline it is compared with
        ["TC-12"] = new("BLE happy path: ticket sent via Bluetooth becomes the Driver's current load", "FR-6, Check 2", "QR-B02"),
        ["TC-13"] = new("BLE payload parity: received load carries the ticket's QR payload unchanged", "FR-4, FR-6", "QR-B01")
    };

    private static readonly ConcurrentQueue<BaselineTestResult> Results = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> BaselinesByTest = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> NotesByTest = new();
    private static readonly ThreadLocal<DateTime> TestStart = new();
    private static readonly DateTime RunStart = DateTime.Now;

    public static void StartTest() => TestStart.Value = DateTime.Now;

    // Called by BaselineRecorder for the test that is running
    public static void AddBaseline(string path) =>
        BaselinesByTest.GetOrAdd(TestContext.CurrentContext.Test.ID, _ => new()).Enqueue(path);

    // A finding worth showing in the report's Message column for a test that still passes
    public static void AddNote(string note)
    {
        Console.WriteLine($"📝 {note}");
        NotesByTest.GetOrAdd(TestContext.CurrentContext.Test.ID, _ => new()).Enqueue(note);
    }

    // Call from a [TearDown] after failure evidence was saved
    public static void RecordTest(string node)
    {
        var test = TestContext.CurrentContext.Test;
        var result = TestContext.CurrentContext.Result;
        string workDirectory = TestContext.CurrentContext.WorkDirectory;

        var evidence = Directory.Exists(workDirectory)
            ? Directory.GetFiles(workDirectory, $"Failure_{test.Name}_*.png").OrderBy(p => p).ToList()
            : [];
        var baselines = BaselinesByTest.TryGetValue(test.ID, out var paths) ? paths.ToList() : [];
        var notes = NotesByTest.TryGetValue(test.ID, out var noted) ? noted.ToList() : [];
        string message = string.Join("\n", new[] { result.Message ?? string.Empty }.Concat(notes).Where(m => m.Length > 0));

        Results.Enqueue(new BaselineTestResult(
            test.Name,
            ScenarioIdsOf(test.MethodName ?? test.Name),
            node,
            result.Outcome.Status.ToString(),
            message,
            TestStart.Value == default ? 0 : (DateTime.Now - TestStart.Value).TotalSeconds,
            baselines,
            evidence,
            DateTime.Now));
    }

    // QR_B01_B08_... -> QR-B01, QR-B08; BLE_TC12_TC13_... -> TC-12, TC-13; the original QR E2E is QR-A01
    private static IReadOnlyList<string> ScenarioIdsOf(string methodName)
    {
        if (methodName.StartsWith("Test_LoadDisposalCollectionFlow", StringComparison.Ordinal)) return ["QR-A01"];
        var qr = Regex.Match(methodName, @"^QR_((?:[AB]\d{2}_)+)");
        if (qr.Success) return qr.Groups[1].Value.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(id => $"QR-{id}").ToList();
        var ble = Regex.Match(methodName, @"^BLE_((?:TC\d{2}_)+)");
        return ble.Success
            ? ble.Groups[1].Value.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(id => $"TC-{id[2..]}").ToList()
            : [];
    }

    // Writes the HTML report (timestamped + _latest) and returns its path; null when no test was recorded.
    // Tests not run this time keep their latest earlier result, so re-running one failed test still gives a full report
    public static string? Generate()
    {
        var current = Results.ToList();
        if (current.Count == 0) return null;

        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Reports");
        Directory.CreateDirectory(directory);
        var results = MergeWithEarlierRuns(current, Path.Combine(directory, "QrBaselineReport_results.json"));
        string path = Path.Combine(directory, $"QrBaselineReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        string html = BuildHtml(results, ProbeDevices());
        File.WriteAllText(path, html);
        File.WriteAllText(Path.Combine(directory, "QrBaselineReport_latest.html"), html);
        Console.WriteLine($"📊 QR baseline / BLE readiness report: {path}");
        return path;
    }

    // Latest result per test: this run's results replace earlier ones with the same name; the merged list is saved
    // for the next run
    private static List<BaselineTestResult> MergeWithEarlierRuns(List<BaselineTestResult> current, string resultsPath)
    {
        var merged = new Dictionary<string, BaselineTestResult>();
        try
        {
            if (File.Exists(resultsPath))
            {
                foreach (var earlier in JsonSerializer.Deserialize<List<BaselineTestResult>>(File.ReadAllText(resultsPath)) ?? [])
                {
                    merged[earlier.TestName] = earlier;
                }
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ Ignoring unreadable earlier report results ({ex.Message}).");
        }

        foreach (var result in current) merged[result.TestName] = result;
        var results = merged.Values.OrderBy(r => r.FinishedAt).ToList();
        File.WriteAllText(resultsPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return results;
    }

    private static List<BluetoothReadiness> ProbeDevices()
    {
        try
        {
            // A frozen device would stall every probe command for its full adb timeout
            return AdbHelper.OnlineDevices()
                .Where(udid => AdbHelper.IsResponsive(udid) || Skipped(udid))
                .Select(udid => BluetoothHelper.Probe(udid, ["com.tetratech.recoverytracflex", "com.tetratech.recoverytracdriver"]))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not probe devices for the report: {ex.Message}");
            return [];
        }
    }

    private static bool Skipped(string udid)
    {
        Console.WriteLine($"⚠️ {udid} is not responding to adb; left out of the report's device table.");
        return false;
    }

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    // Failed beats Warning beats Skipped/Inconclusive beats Passed
    private static int Severity(string outcome) => outcome switch
    {
        "Failed" => 3,
        "Warning" => 2,
        "Passed" => 0,
        _ => 1
    };

    private static string Badge(string outcome) =>
        $"<span class=\"badge {outcome.ToLowerInvariant()}\">{E(outcome == "Inconclusive" ? "Not run" : outcome)}</span>";

    private static string BuildHtml(List<BaselineTestResult> results, List<BluetoothReadiness> devices)
    {
        int Count(string outcome) => results.Count(r => r.Outcome == outcome);
        int carriedOver = results.Count(r => r.FinishedAt < RunStart);
        var byScenario = Scenarios.Keys.ToDictionary(id => id, id => results.Where(r => r.ScenarioIds.Contains(id)).ToList());
        string ScenarioOutcome(string id) => byScenario[id].Count == 0
            ? "Inconclusive"
            : byScenario[id].OrderByDescending(r => Severity(r.Outcome)).First().Outcome;

        var html = new StringBuilder();
        html.Append($$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>QR Baseline Report</title>
<style>
  body { font-family: "Segoe UI", sans-serif; margin: 32px; color: #1b1b1b; background: #fff; }
  h1 { margin-bottom: 4px; }
  h2 { margin-top: 32px; border-bottom: 1px solid #ddd; padding-bottom: 4px; }
  .meta { color: #555; margin-bottom: 20px; }
  .summary { display: flex; flex-wrap: wrap; gap: 12px; margin: 16px 0; }
  .card { padding: 12px 18px; border-radius: 8px; color: #fff; font-weight: 600; min-width: 110px; }
  .card span { display: block; font-size: 26px; }
  .total { background: #0078d4; } .pass { background: #107c10; } .fail { background: #d13438; }
  .warn { background: #b46a00; } .skip { background: #6b6b6b; }
  table { border-collapse: collapse; width: 100%; margin-top: 8px; }
  th, td { border: 1px solid #ddd; padding: 6px 8px; text-align: left; vertical-align: top; font-size: 14px; }
  th { background: #f4f6f8; }
  .badge { padding: 2px 8px; border-radius: 10px; color: #fff; font-size: 12px; font-weight: 600; white-space: nowrap; }
  .badge.passed { background: #107c10; } .badge.failed { background: #d13438; } .badge.warning { background: #b46a00; }
  .badge.skipped, .badge.inconclusive { background: #6b6b6b; }
  .msg { white-space: pre-wrap; font-family: Consolas, monospace; font-size: 12px; color: #444; max-width: 640px; }
  pre { background: #f6f8fa; padding: 10px; overflow-x: auto; font-size: 12px; max-height: 420px; }
  details { margin: 8px 0; } summary { cursor: pointer; font-weight: 600; }
  img.evidence { max-width: 260px; border: 1px solid #ccc; margin: 4px; }
  .note { background: #f0f6ff; border-left: 4px solid #0078d4; padding: 10px 14px; }
</style>
</head>
<body>
<h1>QR Baseline &amp; BLE Readiness Report</h1>
<div class="meta">BLE Ticket Handoff POC · Part A (current QR functionality) · Report generated {{DateTime.Now:yyyy-MM-dd HH:mm}} on {{E(Environment.MachineName)}}</div>
<div class="note">These tests record how the QR handoff behaves today. Each recorded baseline is the expected result for the BLE test named in the <em>BLE counterpart</em> column, since the BLE transfer must behave "as after a QR scan".{{(carriedOver > 0 ? $" {carriedOver} of {results.Count} results come from earlier runs (the latest result of each test is shown, with the time it ran)." : "")}}</div>
<div class="summary">
  <div class="card total"><span>{{results.Count}}</span>Tests</div>
  <div class="card pass"><span>{{Count("Passed")}}</span>Passed</div>
  <div class="card fail"><span>{{Count("Failed")}}</span>Failed</div>
  <div class="card warn"><span>{{Count("Warning")}}</span>Warnings</div>
  <div class="card skip"><span>{{results.Count - Count("Passed") - Count("Failed") - Count("Warning")}}</span>Skipped</div>
</div>
""");

        // Requirement coverage: worst outcome of the scenarios that serve as each requirement's baseline
        html.Append("<h2>Requirement coverage</h2><table><tr><th>Requirement / check</th><th>Baseline scenarios</th><th>Status</th></tr>");
        var requirements = Scenarios
            .SelectMany(s => s.Value.Requirements.Split(", ").Select(req => (Requirement: req, Id: s.Key)))
            .GroupBy(x => x.Requirement)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            var ids = requirement.Select(x => x.Id).ToList();
            string worst = ids.Select(ScenarioOutcome).OrderByDescending(Severity).First();
            html.Append($"<tr><td>{E(requirement.Key)}</td><td>{string.Join(", ", ids.Select(id => $"{id} {Badge(ScenarioOutcome(id))}"))}</td><td>{Badge(worst)}</td></tr>");
        }
        html.Append("</table>");

        html.Append("<h2>Scenario results</h2><table><tr><th>Scenario</th><th>What it records</th><th>Requirement</th><th>BLE counterpart</th><th>Status</th><th>Test · node · duration</th><th>Message</th></tr>");
        foreach (var (id, scenario) in Scenarios)
        {
            var runs = byScenario[id];
            string tests = runs.Count == 0
                ? "not run"
                : string.Join("<br>", runs.Select(r => $"{E(r.TestName)} · {E(r.Node)} · {r.DurationSeconds:0}s · {r.FinishedAt:MMM d HH:mm}"));
            string messages = string.Join("\n", runs.Select(r => r.Message).Where(m => m.Length > 0).Distinct());
            html.Append($"<tr><td><b>{id}</b></td><td>{E(scenario.Title)}</td><td>{E(scenario.Requirements)}</td><td>{E(scenario.BleCounterpart)}</td>" +
                        $"<td>{Badge(ScenarioOutcome(id))}</td><td>{tests}</td><td class=\"msg\">{E(Shorten(messages, 900))}</td></tr>");
        }
        html.Append("</table>");

        html.Append("<h2>Recorded baselines</h2>");
        var baselines = results.SelectMany(r => r.BaselinePaths).Where(File.Exists).ToList();
        if (baselines.Count == 0) html.Append("<p>No baseline was recorded in this run.</p>");
        foreach (string baseline in baselines)
        {
            string json = File.ReadAllText(baseline);
            string scenarioId = TryRead(json, "scenario") ?? Path.GetFileNameWithoutExtension(baseline);
            html.Append($"<details><summary>{E(scenarioId)} — {E(Path.GetFileName(baseline))}</summary><pre>{E(json)}</pre></details>");
        }

        html.Append("<h2>Devices (probed at the end of the run)</h2>");
        if (devices.Count == 0)
        {
            html.Append("<p>No device online in adb.</p>");
        }
        else
        {
            html.Append("<table><tr><th>Device</th><th>Android</th><th>BLE hardware</th><th>Bluetooth on</th><th>Airplane mode</th><th>Apps (Bluetooth permissions)</th></tr>");
            foreach (var d in devices)
            {
                string apps = string.Join("<br>", d.Apps.Select(a =>
                    $"{E(a.Key.Split('.').Last())} {E(a.Value.VersionName)}: " +
                    (a.Value.BluetoothPermissions.Count == 0
                        ? "<i>none requested</i>"
                        : E(string.Join(", ", a.Value.BluetoothPermissions.Select(p => $"{p.Key.Split('.').Last()}={(p.Value ? "granted" : "not granted")}"))))));
                html.Append($"<tr><td>{E(d.Udid)} ({E(d.Model)})</td><td>{E(d.AndroidVersion)} (API {d.SdkLevel})</td><td>{(d.HasBleFeature ? "yes" : "<b>no</b>")}</td>" +
                            $"<td>{(d.BluetoothOn ? "yes" : "no")}</td><td>{(d.AirplaneModeOn ? "<b>on</b>" : "off")}</td><td>{apps}</td></tr>");
            }
            html.Append("</table>");
        }

        var failures = results.Where(r => r.EvidencePaths.Count > 0).ToList();
        if (failures.Count > 0)
        {
            html.Append("<h2>Failure evidence</h2>");
            foreach (var r in failures)
            {
                html.Append($"<h3>{E(r.TestName)}</h3><div class=\"msg\">{E(Shorten(r.Message, 1200))}</div><div>");
                foreach (string png in r.EvidencePaths.Where(File.Exists))
                {
                    html.Append($"<a href=\"file:///{E(png.Replace('\\', '/'))}\"><img class=\"evidence\" alt=\"{E(Path.GetFileName(png))}\" title=\"{E(Path.GetFileName(png))}\" " +
                                $"src=\"data:image/png;base64,{Convert.ToBase64String(File.ReadAllBytes(png))}\"></a>");
                }
                html.Append("</div>");
            }
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + " …";

    private static string? TryRead(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
