using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Scenarios;

// 💡 This instructs NUnit to run two instances of this test class in parallel
[TestFixture("Grid_Node1_Driver", "driver")]
[TestFixture("Grid_Node1_Flex", "flex")]
[Parallelizable(ParallelScope.Self)] // 🚀 Ensures these two fixtures run concurrently
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
        // Both emulators will now execute this step at the exact same time on different ports!
        Console.WriteLine($"Running parallel execution on: {DriverFactory.CurrentDeviceName} with UDID: {DriverFactory.CurrentUdid}");
        
        Assert.That(driver.PageSource, Is.Not.Null, "The application page canvas failed to render cleanly.");
    }

    [TearDown]
    public void Teardown()
    {
        Console.WriteLine($"[Teardown] Gracefully force-closing {_targetApp} application from device memory...");
        try
        {
            // 🟢 Force-close the app from the emulator RAM before dropping the driver connection
            if (DriverFactory.Driver != null)
            {
                string targetPackage = string.Equals(_targetApp, "driver", StringComparison.OrdinalIgnoreCase)
                    ? "com.tetratech.recoverytracdriver"
                    : "com.tetratech.recoverytracflex";

                DriverFactory.AndroidDriver.TerminateApp(targetPackage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Non-blocking cleanup notification: {ex.Message}");
        }
        finally
        {
            // 🟢 Clean the ThreadLocal allocation context handles to prevent memory bloat
            DriverFactory.Quit();
        }
    }
}
