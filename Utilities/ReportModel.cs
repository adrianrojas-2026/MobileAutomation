namespace MobileAutomation.Tests.Reporting;

public class ReportResult
{
    public string Environment { get; set; } = string.Empty;

    public string PlatformVersion { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public double DurationSeconds { get; set; }
    public string ScreenshotPath { get; set; } = string.Empty;
}