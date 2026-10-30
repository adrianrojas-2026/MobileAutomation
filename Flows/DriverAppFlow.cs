using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Flows;

// Driver app side shared by the QR handoff tests and the QR baselines: start it on Home, restart it
public static class DriverAppFlow
{
    // -------------------------------------------------------------------------
    // STEP 21: Start the Driver app (second browser / emulator), clear its onboarding and enter its WebView
    // -------------------------------------------------------------------------
    public static (IWebDriver App, DriverHomePage Home) StartOnHome()
    {
        var driverApp = Launch();
        var driverOnboarding = new DriverOnboardingPage(driverApp);
        driverOnboarding.CompleteOnboardingIfPresent();

        Assert.That(driverOnboarding.IsHomeDisplayed(), Is.True,
            $"The Driver app did not reach Home after GET STARTED > SKIP ({driverOnboarding.DescribeLocation()}).");

        // Onboarding runs on native screens; the Driver pages read the app's DOM, so move into its WebView
        EnterWebView(driverApp);
        return (driverApp, new DriverHomePage(driverApp));
    }

    // Starts the Driver app and leaves it on the Welcome screen (a cold start opens there, before GET STARTED).
    // Returns the app, or null when it opened somewhere else
    public static IWebDriver? StartOnWelcome()
    {
        var driverApp = Launch();
        return new DriverOnboardingPage(driverApp).IsWelcomeScreenDisplayed(60) ? driverApp : null;
    }

    private static IWebDriver Launch()
    {
        var driverApp = DriverFactory.StartDriverApp();
        if (driverApp is AndroidDriver androidDriverApp)
        {
            // On the first session after an emulator boot Appium's 'am start' can hang and leave the launcher in
            // front, so bring the app up when it isn't
            string package = DriverPackage(androidDriverApp);
            if (androidDriverApp.CurrentPackage != package)
            {
                Console.WriteLine($"🚚 Driver app not in front after the session started ({androidDriverApp.CurrentPackage}). Launching {package}...");
                androidDriverApp.ActivateApp(package);
            }
            PermissionHelper.HandleSystemPermissions(androidDriverApp);
        }
        return driverApp;
    }

    public static string DriverPackage(AndroidDriver driverApp) =>
        driverApp.Capabilities.GetCapability("appPackage") as string ?? "com.tetratech.recoverytracdriver";

    // SwitchToWebViewContext only logs a failed switch (e.g. an adb timeout on a busy emulator), so retry it and
    // fail here rather than at the first DOM step
    private static void EnterWebView(IWebDriver driverApp)
    {
        if (driverApp is not AndroidDriver android) return;

        for (int attempt = 1; attempt <= 3 && !android.Context.StartsWith("WEBVIEW", StringComparison.Ordinal); attempt++)
        {
            DriverFactory.SwitchToWebViewContext(driverApp);
        }
        Assert.That(android.Context, Does.StartWith("WEBVIEW"), "Could not switch to the Driver app's WEBVIEW context.");
    }

    // Force-stops and relaunches the Driver app, so a load that is still shown afterwards was stored, not only rendered
    public static DriverHomePage Restart(IWebDriver driverApp)
    {
        if (driverApp is not AndroidDriver android)
        {
            Assert.Ignore("Restarting the Driver app needs a device (RunMode=Mobile).");
            return null!;
        }

        android.Context = "NATIVE_APP";
        string package = android.CurrentPackage;
        android.TerminateApp(package);
        android.ActivateApp(package);
        Console.WriteLine($"🔁 Driver app {package} restarted.");

        // A cold start opens on Welcome again (GET STARTED), even after onboarding was completed
        var onboarding = new DriverOnboardingPage(driverApp);
        onboarding.CompleteOnboardingIfPresent();
        Assert.That(onboarding.IsHomeDisplayed(), Is.True,
            $"The Driver app did not come back to Home after a restart ({onboarding.DescribeLocation()}).");
        EnterWebView(driverApp);
        return new DriverHomePage(driverApp);
    }
}
