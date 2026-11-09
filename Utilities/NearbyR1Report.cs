using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MobileAutomation.Tests.Utilities;
using static MobileAutomation.Tests.Reporting.QrBaselineReport;

namespace MobileAutomation.Tests.Reporting;

// One executed #10082 Nearby test as the report shows it. Requirements maps each requirement ID to its status in
// this run: Met, NotMet, Pending, NotChecked or NotRun
public record NearbyTestResult(
    string TestName,
    string Category,
    bool Pending,
    string Node,
    string Outcome,
    string Message,
    double DurationSeconds,
    IReadOnlyDictionary<string, string> Requirements,
    IReadOnlyList<string> BaselinePaths,
    IReadOnlyList<string> EvidencePaths,
    DateTime FinishedAt);

// Feature #10082 R1 report: each requirement (NDT-x-yyy) with the tests that check it and whether it was met. A
// requirement is 'not met' when a test's result names it ('10082-NDT-2-P14 / N04 not met: ...'), 'not checked' when a
// warning says so or the test errored before its checks, and 'met' when the test ran and did not report it. Tests in a
// '*-Pending' category show their unmet requirements as Pending (the feature is not built yet). Tests mark themselves
// with [Requirements(...)]; the latest result of each test is kept across runs, like the QR baseline report.
public static class NearbyR1Report
{
    // Feature titles of the #10082 requirements plan
    private static readonly Dictionary<string, string> Features = new()
    {
        ["NDT-1"] = "Driver App Nearby discoverability",
        ["NDT-2"] = "Flex Nearby discovery menu",
        ["NDT-3"] = "Driver App identity cards",
        ["NDT-5"] = "Nearby transfer supported clients",
        ["NDT-6"] = "Flex Nearby runtime access",
        ["NDT-10"] = "Transfer progress and cancellation"
    };

    private const string Met = "Met", NotMet = "NotMet", PendingStatus = "Pending", NotChecked = "NotChecked", NotRun = "NotRun";

    private static readonly ConcurrentQueue<NearbyTestResult> Results = new();
    private static readonly DateTime RunStart = DateTime.Now;

    // A full ID with optional shorthand followers: 'NDT-2-P14 / N04', 'NDT-6-P01 / N01'
    private static readonly Regex IdGroup = new(@"NDT-(\d+)-([PNE]\d{2})((?:\s*/\s*[PNE]\d{2})*)", RegexOptions.Compiled);
    private static readonly Regex Shorthand = new(@"[PNE]\d{2}", RegexOptions.Compiled);
    // NUnit's 'Multiple failures or warnings in test:' list: '  1)  ...', '  2)  ...'
    private static readonly Regex Numbered = new(@"(?m)^\s*\d+\)\s+", RegexOptions.Compiled);

    // Call from a [TearDown] after failure evidence was saved; records only tests marked with [Requirements]
    // setupFailure: the test's [SetUp] threw (its result isn't set yet, and its [TearDown] won't run)
    public static void RecordTest(string node, Exception? setupFailure = null)
    {
        var test = TestContext.CurrentContext.Test;
        if (!test.Properties.ContainsKey(RequirementsAttribute.ReportPropertyName)) return;

        var result = TestContext.CurrentContext.Result;
        var declared = test.Properties[RequirementsAttribute.PropertyName].Cast<string>().ToList();
        string category = CategoriesOf(test.ClassName, test.Properties["Category"].Cast<string>()).FirstOrDefault(c => c.StartsWith("BLE-R1", StringComparison.Ordinal)) ?? string.Empty;
        bool pending = category.EndsWith("-Pending", StringComparison.Ordinal);
        string outcome = setupFailure != null ? "Failed" : result.Outcome.Status.ToString();
        // Names no requirement, so its requirements show as Not checked
        string message = setupFailure != null ? $"Setup failed: {setupFailure.GetType().Name}: {setupFailure.Message}" : result.Message ?? string.Empty;

        string workDirectory = TestContext.CurrentContext.WorkDirectory;
        var evidence = Directory.Exists(workDirectory)
            ? Directory.GetFiles(workDirectory, $"Failure_{test.Name}_*.png").OrderBy(p => p).ToList()
            : [];
        DateTime started = TestStartedAt;

        Results.Enqueue(new NearbyTestResult(
            test.Name,
            category,
            pending,
            node,
            outcome,
            message,
            started == default ? 0 : (DateTime.Now - started).TotalSeconds,
            StatusOfRequirements(declared, outcome, message, pending),
            BaselinesFor(test.ID),
            evidence,
            DateTime.Now));
    }

    // The fixture's categories (CategoryAttribute on the class) and the test's own
    private static IEnumerable<string> CategoriesOf(string? className, IEnumerable<string> testCategories)
    {
        var type = className == null ? null : typeof(NearbyR1Report).Assembly.GetType(className);
        var onClass = type?.GetCustomAttributes<CategoryAttribute>(true).Select(c => c.Name) ?? [];
        return onClass.Concat(testCategories);
    }

    private static Dictionary<string, string> StatusOfRequirements(List<string> declared, string outcome, string message, bool pending)
    {
        var status = declared.Distinct().ToDictionary(id => id, _ => NotRun);
        if (outcome is not ("Passed" or "Failed" or "Warning")) return status;

        var segments = message.StartsWith("Multiple failures", StringComparison.Ordinal)
            ? Numbered.Split(message).Skip(1).ToList()
            : [message];
        bool anyNotMet = false;
        foreach (string segment in segments)
        {
            string? finding = segment.Contains("not met", StringComparison.OrdinalIgnoreCase) ? NotMet
                : segment.Contains("not checked", StringComparison.OrdinalIgnoreCase) ? NotChecked
                : null;
            if (finding == null) continue;
            foreach (string id in IdsIn(segment))
            {
                // A finding about another requirement than the declared ones still belongs in the report
                status[id] = finding == NotMet && pending ? PendingStatus : finding;
                anyNotMet |= finding == NotMet;
            }
        }

        // A failure that names no requirement is the test itself breaking (setup, exception): nothing was checked
        bool errored = outcome == "Failed" && !anyNotMet;
        foreach (string id in status.Keys.ToList().Where(id => status[id] == NotRun))
        {
            status[id] = errored ? NotChecked : Met;
        }
        return status;
    }

    private static IEnumerable<string> IdsIn(string text)
    {
        foreach (Match m in IdGroup.Matches(text))
        {
            string feature = $"NDT-{m.Groups[1].Value}";
            yield return $"{feature}-{m.Groups[2].Value}";
            foreach (Match s in Shorthand.Matches(m.Groups[3].Value)) yield return $"{feature}-{s.Value}";
        }
    }

    // Writes the HTML report (timestamped + _latest) and returns its path; null when no Nearby test ran this time.
    // Tests not run this time keep their latest earlier result
    public static string? Generate()
    {
        var current = Results.ToList();
        if (current.Count == 0) return null;

        string directory = ReportPaths.Directory;
        var results = MergeWithEarlierRuns(current, Path.Combine(directory, "NearbyR1Report_results.json"));
        string path = Path.Combine(directory, $"NearbyR1Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        string html = BuildHtml(results, ProbeDevices());
        File.WriteAllText(path, html);
        File.WriteAllText(Path.Combine(directory, "NearbyR1Report_latest.html"), html);
        Console.WriteLine($"📊 Nearby R1 requirements report: {path}");
        return path;
    }

    private static List<NearbyTestResult> MergeWithEarlierRuns(List<NearbyTestResult> current, string resultsPath)
    {
        var merged = new Dictionary<string, NearbyTestResult>();
        try
        {
            if (File.Exists(resultsPath))
            {
                foreach (var earlier in JsonSerializer.Deserialize<List<NearbyTestResult>>(File.ReadAllText(resultsPath)) ?? [])
                {
                    merged[earlier.TestName] = earlier;
                }
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ Ignoring unreadable earlier Nearby report results ({ex.Message}).");
        }

        foreach (var result in current) merged[result.TestName] = result;
        var results = merged.Values.OrderBy(r => r.FinishedAt).ToList();
        File.WriteAllText(resultsPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return results;
    }

    // Worst first: a requirement's overall status is the worst of its tests'
    private static int Rank(string status) => status switch
    {
        NotMet => 5,
        PendingStatus => 4,
        NotChecked => 3,
        Met => 2,
        _ => 1
    };

    private static string Label(string status) => status switch
    {
        Met => "Met",
        NotMet => "Not met",
        PendingStatus => "Pending",
        NotChecked => "Not checked",
        _ => "Not run"
    };

    private static string StatusBadge(string status) => $"<span class=\"badge {status.ToLowerInvariant()}\">{Label(status)}</span>";

    private static string OutcomeBadge(string outcome) =>
        $"<span class=\"badge {outcome.ToLowerInvariant()}\">{E(outcome == "Inconclusive" ? "Not run" : outcome)}</span>";

    // NDT-2-P14 before NDT-10-P01, and P01 before P02 / E01 / N01 (as the plan lists them)
    private static (int Feature, int Kind, int Number) SortKey(string id)
    {
        var parts = id.Split('-');
        int kind = parts[2][0] switch { 'P' => 0, 'E' => 1, _ => 2 };
        return (int.Parse(parts[1]), kind, int.Parse(parts[2][1..]));
    }

    private static string BuildHtml(List<NearbyTestResult> results, List<BluetoothReadiness> devices)
    {
        var requirementRows = results
            .SelectMany(r => r.Requirements.Select(kv => (Id: kv.Key, Status: kv.Value, Result: r)))
            .GroupBy(x => x.Id)
            .OrderBy(g => SortKey(g.Key))
            .Select(g => (Id: g.Key, Overall: g.Select(x => x.Status).OrderByDescending(Rank).First(), Checks: g.ToList()))
            .ToList();
        int CountReq(string status) => requirementRows.Count(r => r.Overall == status);
        int carriedOver = results.Count(r => r.FinishedAt < RunStart);
        string pendingCategories = string.Join(", ", results.Where(r => r.Pending).Select(r => r.Category).Distinct());

        var html = new StringBuilder();
        html.Append($$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Nearby R1 Report</title>
<style>{{Css}}</style>
</head>
<body>
<h1>Nearby R1 Requirements Report</h1>
<div class="meta">Feature #10082 · Release 1 (Driver App discoverability, Flex discovery, access, transfer) · Report generated {{DateTime.Now:yyyy-MM-dd HH:mm}} on {{E(Environment.MachineName)}}</div>
<div class="note">Each requirement is traced to the tests that check it. <b>Not met</b>: a test reported it ('NDT-x-yyy not met'). <b>Pending</b>: not met, in a test category waiting for the feature to be built ({{E(pendingCategories.Length > 0 ? pendingCategories : "none in these results")}}). <b>Not checked</b>: a warning said so, or the test stopped before its checks. <b>Met</b>: the test ran and did not report it.{{(carriedOver > 0 ? $" {carriedOver} of {results.Count} test results come from earlier runs (the latest result of each test is shown, with the time it ran)." : "")}}</div>
<div class="summary">
  <div class="card total"><span>{{requirementRows.Count}}</span>Requirements</div>
  <div class="card pass"><span>{{CountReq(Met)}}</span>Met</div>
  <div class="card fail"><span>{{CountReq(NotMet)}}</span>Not met</div>
  <div class="card pend"><span>{{CountReq(PendingStatus)}}</span>Pending</div>
  <div class="card warn"><span>{{CountReq(NotChecked)}}</span>Not checked</div>
  <div class="card skip"><span>{{CountReq(NotRun)}}</span>Not run</div>
  <div class="card total"><span>{{results.Count}}</span>Tests</div>
</div>
""");

        html.Append("<h2>Requirement coverage</h2><table><tr><th>Feature</th><th>Requirement</th><th>Status</th><th>Checked by</th><th>Finding</th></tr>");
        foreach (var row in requirementRows)
        {
            string feature = string.Join('-', row.Id.Split('-').Take(2));
            string checks = string.Join("<br>", row.Checks.Select(c => $"{StatusBadge(c.Status)} {E(c.Result.TestName)} · {c.Result.FinishedAt:MMM d HH:mm}"));
            // The test result segment that names this requirement, for anything but Met
            string finding = row.Overall is NotMet or PendingStatus or NotChecked
                ? string.Join("\n", row.Checks.Where(c => c.Status == row.Overall).Select(c => FindingFor(row.Id, c.Result.Message)).Where(f => f.Length > 0).Distinct())
                : string.Empty;
            html.Append($"<tr><td>{E(feature)} · {E(Features.GetValueOrDefault(feature, string.Empty))}</td><td><b>{E(row.Id)}</b></td><td>{StatusBadge(row.Overall)}</td>" +
                        $"<td>{checks}</td><td class=\"msg\">{E(Shorten(finding, 600))}</td></tr>");
        }
        html.Append("</table>");

        html.Append("<h2>Test results</h2><table><tr><th>Test</th><th>Category</th><th>Outcome</th><th>Node · duration · ran</th><th>Requirements</th><th>Message</th></tr>");
        foreach (var r in results.OrderBy(r => r.Category).ThenBy(r => r.TestName))
        {
            string reqs = r.Requirements.Count == 0
                ? "<i>control (no requirement of its own)</i>"
                : string.Join(" ", r.Requirements.OrderBy(kv => SortKey(kv.Key)).Select(kv => $"{E(kv.Key)}&nbsp;{StatusBadge(kv.Value)}"));
            string outcome = r.Pending && r.Outcome == "Failed" ? StatusBadge(PendingStatus) : OutcomeBadge(r.Outcome);
            html.Append($"<tr><td>{E(r.TestName)}</td><td>{E(r.Category)}</td><td>{outcome}</td>" +
                        $"<td>{E(r.Node)} · {r.DurationSeconds:0}s · {r.FinishedAt:MMM d HH:mm}</td><td>{reqs}</td><td class=\"msg\">{E(Shorten(r.Message, 900))}</td></tr>");
        }
        html.Append("</table>");

        html.Append("<h2>Recorded baselines</h2>");
        var baselines = results.SelectMany(r => r.BaselinePaths).Where(File.Exists).ToList();
        if (baselines.Count == 0) html.Append("<p>No baseline was recorded.</p>");
        foreach (string baseline in baselines)
        {
            html.Append($"<details><summary>{E(Path.GetFileName(baseline))}</summary><pre>{E(File.ReadAllText(baseline))}</pre></details>");
        }

        html.Append(DevicesHtml(devices));

        var failures = results.Where(r => r.EvidencePaths.Count > 0).ToList();
        if (failures.Count > 0)
        {
            html.Append("<h2>Failure evidence</h2>");
            foreach (var r in failures)
            {
                html.Append($"<h3>{E(r.TestName)}</h3><div class=\"msg\">{E(Shorten(r.Message, 1200))}</div><div>{EvidenceHtml(r.EvidencePaths)}</div>");
            }
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    // The part of a test's result that names the requirement (one numbered item of a multiple-failure message)
    private static string FindingFor(string id, string message)
    {
        var segments = message.StartsWith("Multiple failures", StringComparison.Ordinal) ? Numbered.Split(message).Skip(1) : [message];
        string? segment = segments.FirstOrDefault(s => IdsIn(s).Contains(id));
        if (segment == null) return string.Empty;
        // Up to the NUnit 'Assert.That(...)' echo that follows the message
        int echo = segment.IndexOf("\nAssert.", StringComparison.Ordinal);
        return (echo > 0 ? segment[..echo] : segment).Trim();
    }
}
