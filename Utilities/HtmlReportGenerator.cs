using System.Text;

namespace MobileAutomation.Tests.Reporting;

public static class HtmlReportGenerator
{
    private static readonly List<ReportResult> Results = new();

    public static void AddResult(ReportResult result)
    {
        Results.Add(result);
    }

    public static void Generate()
    {
        Console.WriteLine(
            "Generating HTML Report...");

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
Generated: {DateTime.Now}
</p>

<table>

<tr>
<th>Environment</th>
<th>Android Version</th>
<th>Device</th>
<th>Duration (s)</th>
<th>Result</th>
<th>Screenshot</th>
<th>Error</th>
</tr>
""");

        foreach (var result in Results)
        {
            html.Append($"""
<tr>
<td>{result.Environment}</td>
<td>{result.PlatformVersion}</td>
<td>{result.DeviceName}</td>
<td>{result.DurationSeconds:F2}</td>
<td class="{(result.Result == "PASS" ? "pass" : "fail")}">
{result.Result}
</td>
<td>
<a href="{result.ScreenshotPath}" target="_blank">
    View Screenshot
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