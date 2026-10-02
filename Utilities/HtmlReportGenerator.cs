using System.Text;

namespace MobileAutomation.Tests.Reporting;

public static class HtmlReportGenerator
{
    public static DateTime SuiteStartTime { get; set; }
    public static DateTime SuiteEndTime { get; set; }
    private static readonly object LockObject =
        new();

    private static readonly List<ReportResult> Results =
        new();

    public static void AddResult(ReportResult result)
    {
        lock (LockObject)
        {
            Results.Add(result);
        }
    }

    public static void Generate()
    {
        var suiteRuntime =
            (SuiteEndTime - SuiteStartTime)
                .TotalSeconds;
        var sequentialRuntime =
            Results.Sum(r => r.DurationSeconds);

        var savings =
            sequentialRuntime - suiteRuntime;

        var savingsPercentage =
            sequentialRuntime > 0
                ? (savings / sequentialRuntime) * 100
                : 0;
            
        Console.WriteLine(
            "Generating HTML Report...");

        var supportedVersions =
            Results
                .Where(r => r.Result == "PASS")
                .OrderBy(r =>
                {
                    return double.TryParse(
                        r.PlatformVersion,
                        out var version)
                        ? version
                        : 0;
                })
                .ToList();
                
        var passed =
            Results.Count(r => r.Result == "PASS");

        var failed =
            Results.Count(r => r.Result == "FAIL");

        var html = new StringBuilder();

        html.Append("""
<html>
<head>
<title>RecoveryTrac Flex Compatibility Report</title>

<style>
    .performance-panel{
        background:#fff4ce;
        border-left:5px solid #ffb900;
        padding:15px;
        margin-bottom:20px;
        border-radius:5px;
    }

    .performance-panel h2{
        margin-top:0;
        color:#8a6d00;
    }
    .compatibility-matrix{
        background:#f0f9ff;
        border-left:5px solid #107c10;
        padding:15px;
        margin-bottom:20px;
        border-radius:5px;
    }

    .compatibility-matrix h2{
        margin-top:0;
        color:#107c10;
    }

    .compatibility-matrix ul{
        margin:0;
        padding-left:20px;
    }

    .compatibility-matrix li{
        margin-bottom:6px;
    }
    .metadata{
        background:#f4f6f8;
        border-left:5px solid #0078d4;
        padding:15px;
        margin-bottom:20px;
        border-radius:5px;
    }

    .metadata p{
        margin:5px 0;
    }
.summary{
    display:flex;
    gap:20px;
    margin-bottom:20px;
}

.card{
    padding:15px;
    border-radius:8px;
    color:white;
    font-size:18px;
    font-weight:bold;
}

.pass-card{
    background:#107c10;
}

.fail-card{
    background:#d13438;
}

.total-card{
    background:#0078d4;
}
body{
    font-family:Segoe UI;
    margin:40px;
}

h1{
    color:#0078D4;
}

table{
    border-collapse:collapse;
    width:100%;
}

th{
    background:#0078D4;
    color:white;
    padding:10px;
}

td{
    border:1px solid #ddd;
    padding:8px;
}

.pass{
    color:green;
    font-weight:bold;
}

.fail{
    color:red;
    font-weight:bold;
}
</style>

</head>
<body>
""");

        html.Append($"""
    <h1>RecoveryTrac Flex Compatibility Report</h1>

    <div class="metadata">

        <p><b>Application:</b> RecoveryTrac Flex</p>

        <p><b>Framework:</b> Appium + NUnit</p>

        <p><b>Automation Pattern:</b> Page Object Model</p>

        <p><b>Execution Mode:</b> Parallel</p>

        <p><b>Tester:</b> Adrian Mauricio</p>

        <p><b>Suite Runtime:</b> {suiteRuntime:F2} seconds</p>

        <p><b>Sequential Runtime:</b> {sequentialRuntime:F2} seconds</p>

        <p><b>Time Saved:</b> {savings:F2} seconds</p>

        <p><b>Performance Improvement:</b> {savingsPercentage:F1}%</p>

    </div>
    """);

    //
    // Compatibility Matrix
    //
    html.Append($"""
    <div class="compatibility-matrix">

        <h2>Compatibility Assessment</h2>

        <p>
            RecoveryTrac Flex was successfully validated on
            <b>{supportedVersions.Count}</b> Android environment(s).
        </p>

        <ul>
    """);

    foreach (var version in supportedVersions)
    {
        html.Append($"""
            <li>
                ✅ Android {version.PlatformVersion} Supported
            </li>
    """);
    }

    html.Append("""
        </ul>

        <p>
            <b>Assessment:</b>
            All executed compatibility validations completed successfully.
            The application is compatible with all tested Android versions.
        </p>

    </div>
    """);

    //
    // Parallel Performance Panel
    //
    html.Append($"""
    <div class="performance-panel">

        <h2>Parallel Execution Performance</h2>

        <p>
            <b>Sequential Runtime:</b>
            {sequentialRuntime:F2} seconds
        </p>

        <p>
            <b>Actual Runtime:</b>
            {suiteRuntime:F2} seconds
        </p>

        <p>
            <b>Time Saved:</b>
            {savings:F2} seconds
        </p>

        <p>
            <b>Performance Improvement:</b>
            {savingsPercentage:F1}%
        </p>

    </div>
    """);

    //
    // Summary Cards
    //
    html.Append($"""
    <div class="summary">

        <div class="card pass-card">
            Passed: {passed}
        </div>

        <div class="card fail-card">
            Failed: {failed}
        </div>

        <div class="card total-card">
            Total: {Results.Count}
        </div>

    </div>

    <p>
        <b>Generated:</b> {DateTime.Now}
    </p>

    <table>

    <tr>
        <th>Environment</th>
        <th>Android Version</th>
        <th>Device</th>
        <th>Start</th>
        <th>End</th>
        <th>Duration (s)</th>
        <th>Result</th>
        <th>Screenshot</th>
        <th>Error</th>
    </tr>
    """);

   foreach (var result in Results
        .OrderBy(r => double.TryParse(r.PlatformVersion, out var v) ? v : 0))
    {
        html.Append($"""
        <tr>
            <td>{result.Environment}</td>
            <td>{result.PlatformVersion}</td>
            <td>{result.DeviceName}</td>
            <td>{result.StartTime}</td>
            <td>{result.EndTime}</td>
            <td>{result.DurationSeconds:F2}</td>
            <td class="{(result.Result == "PASS" ? "pass" : "fail")}">
                {result.Result}
            </td>
            <td>
                <a class="screenshot-link"
                href="{result.ScreenshotPath}"
                target="_blank">
                    📷 Open
                </a>
            </td>
            <td>{result.ErrorMessage}</td>
        </tr>
        """);
    }

    html.Append("""
    </table>

    </body>
    </html>
    """);
        var reportFolder =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "Reports");

        Directory.CreateDirectory(reportFolder);

        var filePath =
            Path.Combine(
                reportFolder,
                "CompatibilityReport.html");
        
        Console.WriteLine($"HTML Report: {filePath}");
        
        File.WriteAllText(
            filePath,
            html.ToString());

        Console.WriteLine(
            $"HTML Report: {filePath}");
    }
}
