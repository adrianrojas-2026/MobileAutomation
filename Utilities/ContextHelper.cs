using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Utilities;

public static class ContextHelper
{
    public static void SwitchToWebView(AndroidDriver driver)
        {
        foreach (var context in driver.Contexts)
        {
            Console.WriteLine($"Found Context: {context}");

            if (context.Contains("WEBVIEW"))
            {
                driver.Context = context;

                Console.WriteLine($"Switched to {context}");

                return;
            }
        }

        throw new Exception("WEBVIEW context was not found.");
    }
    public static void SwitchToNative(AndroidDriver driver)
    {
        driver.Context = "NATIVE_APP";
    }
}