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

    public static bool IsGranted(string udid, string package, IEnumerable<string> permissions)
    {
        string dump = AdbHelper.Read(udid, $"shell dumpsys package {package}");
        return permissions.All(p => dump.Contains($"{p}: granted=true", StringComparison.Ordinal));
    }

    // Waits for the system dialog and taps Allow; returns whether Android asked. The page objects run in the WEBVIEW
    // context, so it hops to NATIVE_APP for the dialog and back
    public static bool AllowWhenAsked(AndroidDriver driver, int timeoutSeconds = 15)
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

            Console.WriteLine($"📱 Android asks: '{dialog.Text.Trim()}'. Tapping Allow...");
            driver.FindElement(AllowButton).Click();
            new WebDriverWait(driver, TimeSpan.FromSeconds(10)).Until(d => !d.FindElements(Dialog).Any());
            return true;
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
