using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Scenarios;

// 💡 This instructs NUnit to run two instances of this test class in parallel
[TestFixture("Grid_Node1_Driver", "driver")]
[TestFixture("Grid_Node1_Flex", "flex")]
public class ParallelDriverTrackingTests
{
    private readonly string _targetDevice;
    private readonly string _targetApp;

    // The constructor captures the variables passed from the TestFixture tags above
    public ParallelDriverTrackingTests(string targetDevice, string targetApp)
    {
        _targetDevice = targetDevice;
        _targetApp = targetApp;
    }

    [SetUp]
    public void Setup()
    {
        // Launches the designated device profile cleanly in its isolated worker thread
        DriverFactory.Initialize(_targetDevice, _targetApp);
    }

    [Test]
    public void VerifyAppLaunchesAndInitializesCorrectly()
    {
        var driver = DriverFactory.AndroidDriver;
        Thread.Sleep(2000); // Allow some time for the app to stabilize
        // Both emulators will now execute this step at the exact same time on different ports!
        Console.WriteLine($"Running parallel execution on: {DriverFactory.CurrentDeviceName} with UDID: {DriverFactory.CurrentUdid}");
        
        Assert.That(driver.PageSource, Is.Not.Null, "The application page canvas failed to render cleanly.");
    }

    [TearDown]
    public void Teardown()
    {
        DriverFactory.Quit();
    }
}
