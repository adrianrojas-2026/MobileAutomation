using MobileAutomation.Tests.Reporting;

// No namespace: a SetUpFixture outside every namespace wraps the whole assembly, so its OneTimeTearDown runs once
// after all tests and the final report covers the full run
[SetUpFixture]
public class ReportSetUpFixture
{
    [OneTimeTearDown]
    public void WriteFinalReports()
    {
        QrBaselineReport.Generate();
    }
}
