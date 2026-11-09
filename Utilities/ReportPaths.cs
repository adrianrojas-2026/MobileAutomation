namespace MobileAutomation.Tests.Reporting;

// Where the generated reports live: TestResults/Reports at the test project's root, whichever build ran the tests
// (Test Explorer's Debug or Run-Suite's Release), so every run adds to the same reports and index. Falls back to
// <bin>/Reports when the project folder can't be found (e.g. the tests were copied elsewhere)
public static class ReportPaths
{
    private static readonly Lazy<string> ReportsDirectory = new(() =>
    {
        string start = TestContext.CurrentContext.WorkDirectory;
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MobileAutomation.Tests.csproj")))
            {
                return Path.Combine(dir.FullName, "TestResults", "Reports");
            }
        }
        return Path.Combine(start, "Reports");
    });

    public static string Directory
    {
        get
        {
            System.IO.Directory.CreateDirectory(ReportsDirectory.Value);
            return ReportsDirectory.Value;
        }
    }
}
