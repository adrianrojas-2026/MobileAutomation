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
    // With showTutorial the Tutorial is reset first, so GET STARTED leads to it even on an emulator that has already
    // skipped it. Returns the app, or null when it opened somewhere else
    public static IWebDriver? StartOnWelcome(bool showTutorial = false)
    {
        var driverApp = Launch();
        var onboarding = new DriverOnboardingPage(driverApp);
        if (!onboarding.IsWelcomeScreenDisplayed(60)) return null;
        if (showTutorial && driverApp is AndroidDriver android)
        {
            ResetTutorial(android);
            if (!onboarding.IsWelcomeScreenDisplayed(60)) return null;
        }
        return driverApp;
    }

    // Driver app preference that SKIP sets to false (Capacitor Preferences, Driver-App util/constants.ts)
    private const string OpenTutorialKey = "open-tutorial-after-login";

    // The Welcome page reads the Tutorial preference when it loads, so the preference is removed in the app's WebView
    // and the app cold-started to read it again. A failed reset is only logged; the caller sees no Tutorial
    private static void ResetTutorial(AndroidDriver android)
    {
        DriverFactory.SwitchToWebViewContext(android);
        try
        {
            var error = ((IJavaScriptExecutor)android).ExecuteAsyncScript(@"
                var done = arguments[arguments.length - 1];
                var cap = window.Capacitor;
                if (!cap) { done('window.Capacitor is missing'); return; }
                var args = { key: arguments[0] };
                var removal = cap.Plugins && cap.Plugins.Preferences
                    ? cap.Plugins.Preferences.remove(args)
                    : cap.nativePromise('Preferences', 'remove', args);
                removal.then(function () { done(null); }, function (e) { done(String((e && e.message) || e)); });",
                OpenTutorialKey) as string;
            Console.WriteLine(error == null
                ? $"🚚 Driver app: Tutorial reset ('{OpenTutorialKey}' removed)."
                : $"🚚 Driver app: could not reset the Tutorial ({error}).");
        }
        catch (WebDriverException e)
        {
            Console.WriteLine($"🚚 Driver app: could not reset the Tutorial ({e.Message.Split('\n')[0]}).");
        }
        finally
        {
            android.Context = "NATIVE_APP";
        }

        string package = DriverPackage(android);
        android.TerminateApp(package);
        android.ActivateApp(package);
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
        // An emulator that froze mid-test passes the session-start check and only shows up here
        if (!android.Context.StartsWith("WEBVIEW", StringComparison.Ordinal)
            && AdbHelper.GetUdid(android) is { } udid && !AdbHelper.IsResponsive(udid))
        {
            Assert.Fail($"Could not switch to the Driver app's WEBVIEW context: {udid} is not responding to adb. Restart that emulator.");
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
