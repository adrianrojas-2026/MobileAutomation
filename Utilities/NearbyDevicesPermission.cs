using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Utilities;

// Android 12+ "Nearby devices" runtime permission (BLUETOOTH_SCAN / ADVERTISE / CONNECT) that the BLE handoff asks
// for the first time it uses Bluetooth: "Allow <app> to find, connect to, and determine the relative position of
// nearby devices?". Reset lets a test see the question again; AllowWhenAsked answers it like a user would.
public static class NearbyDevicesPermission
{
    public static readonly string[] Flex = ["android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT"];
    public static readonly string[] Driver = ["android.permission.BLUETOOTH_ADVERTISE", "android.permission.BLUETOOTH_CONNECT"];

    private static readonly By Dialog = By.XPath(
        "//*[@resource-id='com.android.permissioncontroller:id/permission_message'][contains(@text,'nearby devices')]");
    private static readonly By AllowButton = By.XPath(
        "//*[@resource-id='com.android.permissioncontroller:id/permission_allow_button']" +
        " | //android.widget.Button[translate(@text,'ALOW','alow')='allow']");
    private static readonly By DenyButton = By.XPath(
        "//*[@resource-id='com.android.permissioncontroller:id/permission_deny_button'" +
        " or @resource-id='com.android.permissioncontroller:id/permission_deny_and_dont_ask_again_button']" +
        " | //android.widget.Button[starts-with(translate(@text,'DON','don'),'don')]"); // "Don't allow" / "Don’t allow"

    // Revokes the permissions and clears "user set / don't ask again", so the app asks again. Android stops an app
    // whose permission is revoked: call this before the app is (re)launched
    public static void Reset(string udid, string package, IEnumerable<string> permissions)
    {
        foreach (string permission in permissions)
        {
            AdbHelper.Run(udid, $"shell pm revoke {package} {permission}");
            AdbHelper.Run(udid, $"shell pm clear-permission-flags {package} {permission} user-set user-fixed");
        }
        Console.WriteLine($"🔒 {udid}: Nearby devices permission reset for {package}, so Android asks again.");
    }

    // Grants the permissions without asking; unlike revoking, granting does not stop the app. On Android 11 and older
    // pm grant just fails, as these permissions don't exist there
    public static void Grant(string udid, string package, IEnumerable<string> permissions)
    {
        foreach (string permission in permissions)
        {
            AdbHelper.Run(udid, $"shell pm grant {package} {permission}");
        }
    }

    public static bool IsGranted(string udid, string package, IEnumerable<string> permissions)
    {
        string dump = AdbHelper.Read(udid, $"shell dumpsys package {package}");
        return permissions.All(p => dump.Contains($"{p}: granted=true", StringComparison.Ordinal));
    }

    // Waits for the system dialog and taps Allow; returns whether Android asked. The page objects run in the WEBVIEW
    // context, so it hops to NATIVE_APP for the dialog and back
    public static bool AllowWhenAsked(AndroidDriver driver, int timeoutSeconds = 15) =>
        AnswerWhenAsked(driver, AllowButton, "Allow", timeoutSeconds);

    // Same, tapping Don't allow: the user refusing Nearby devices access
    public static bool DenyWhenAsked(AndroidDriver driver, int timeoutSeconds = 15) =>
        AnswerWhenAsked(driver, DenyButton, "Don't allow", timeoutSeconds);

    private static bool AnswerWhenAsked(AndroidDriver driver, By button, string buttonName, int timeoutSeconds)
    {
        string context = driver.Context;
        driver.Context = "NATIVE_APP";
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            IWebElement? dialog = null;
            try
            {
                dialog = wait.Until(d => d.FindElements(Dialog).FirstOrDefault(e => e.Displayed));
            }
            catch (WebDriverTimeoutException)
            {
            }
            if (dialog == null) return false;

            Console.WriteLine($"📱 Android asks: '{dialog.Text.Trim()}'. Tapping {buttonName}...");
            bool Closed(int seconds)
            {
                var closed = new WebDriverWait(driver, TimeSpan.FromSeconds(seconds));
                closed.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
                try
                {
                    closed.Until(d => !d.FindElements(Dialog).Any());
                    return true;
                }
                catch (WebDriverTimeoutException)
                {
                    return false;
                }
            }

            // Android ignores a tap on the permission dialog while another window covers it (e.g. a toast) or while
            // a busy emulator's System UI lags, so tap again until the dialog closes
            for (int tap = 1; tap <= 2; tap++)
            {
                driver.FindElements(button).FirstOrDefault(b => b.Displayed)?.Click();
                if (Closed(5)) return true;
                Console.WriteLine($"📱 The dialog is still open after tap {tap} on {buttonName}.");
            }

            // Seen on the Android 15 emulators: the dialog has focus but its window gets no touches, which go to the
            // app's window behind it and are dropped ("Not sending touch gesture to ...ActivityRecordInputSink ...
            // NO_INPUT_CHANNEL"). Keys go to the focused window instead: Back closes the dialog as a refusal, the same
            // answer as Don't allow for this request. Allow has no key, so it is tapped with adb input at the
            // button's centre
            string udid = AdbHelper.GetUdid(driver)!;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (button == DenyButton)
                {
                    Console.WriteLine($"📱 Taps are not reaching the dialog; pressing Back to refuse (attempt {attempt})...");
                    AdbHelper.Run(udid, "shell input keyevent KEYCODE_BACK");
                }
                else if (driver.FindElements(button).FirstOrDefault(b => b.Displayed) is { } target)
                {
                    var r = target.Rect;
                    Console.WriteLine($"📱 Taps are not reaching the dialog; tapping {buttonName} with adb input (attempt {attempt})...");
                    AdbHelper.Run(udid, $"shell input tap {r.X + r.Width / 2} {r.Y + r.Height / 2}");
                }
                if (Closed(5)) return true;
            }
            throw new WebDriverTimeoutException(
                $"Android's Nearby devices dialog stayed open after tapping {buttonName} twice and the adb fallback three times. " +
                "Check the emulator's screen: taps may not be reaching the dialog.");
        }
        finally
        {
            if (context.StartsWith("WEBVIEW", StringComparison.Ordinal))
            {
                DriverFactory.SwitchToWebViewContext(driver);
            }
        }
    }
}
