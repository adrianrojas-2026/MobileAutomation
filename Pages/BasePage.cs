using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Pages;

public abstract class BasePage
{
     protected readonly IWebDriver Driver;

    protected BasePage(IWebDriver driver)
    {
        Driver = driver;
    }

    protected IWebElement Find(By locator)
    {
        var wait = new WebDriverWait(
            Driver,
            TimeSpan.FromSeconds(30));

        // FindElements returns an empty list instead of throwing, so polling doesn't trip the debugger
        return wait.Until(d =>
            d.FindElements(locator).FirstOrDefault());
    }

    protected void Click(By locator)
    {
        Find(locator).Click();
    }

    // Centers the element in its scroll container so Ionic footers/toolbars don't intercept the click
    protected void ScrollIntoView(IWebElement element)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", element);
    }

    // Waits for a visible element, scrolls it into view and clicks it, retrying while overlays/animations intercept the click
    // jsClickWhenIntercepted: for fixed elements (footer buttons) a floating overlay such as the chat bubble can
    // permanently cover, where scrolling cannot help
    protected void ScrollAndClick(By locator, int timeoutSeconds = 15, bool jsClickWhenIntercepted = false)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));

        wait.Until(d =>
        {
            // FindElements returns an empty list while the element is still rendering, instead of throwing
            var element = d.FindElements(locator).FirstOrDefault();
            if (element == null) return false;

            try
            {
                // Scroll first: Ionic reports content outside the visible scroll area as not displayed
                ScrollIntoView(element);
                if (!element.Displayed) return false;
                element.Click();
                return true;
            }
            catch (ElementClickInterceptedException) when (jsClickWhenIntercepted)
            {
                ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", element);
                return true;
            }
            catch (Exception ex) when (ex is StaleElementReferenceException
                                          or ElementClickInterceptedException
                                          or ElementNotInteractableException)
            {
                // Overlay, animation or re-render in progress - poll again
                return false;
            }
        });
    }

    // Returns the first visible match, or null once the timeout elapses
    protected IWebElement? FindVisible(By locator, int timeoutSeconds)
    {
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            return wait.Until(d => d.FindElements(locator).FirstOrDefault(e => e.Displayed));
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
    }

    // "Performing initial sync" / "Syncing latest data" overlay; it blocks every tap on the screen underneath
    private bool IsSyncOverlayDisplayed(IWebDriver d)
    {
        try
        {
            var loaders = Driver is AndroidDriver
                ? d.FindElements(By.XPath("//*[contains(@text, 'Performing initial sync')] | //*[contains(@text, 'Syncing latest data')]"))
                : d.FindElements(By.CssSelector("ion-loading, .loading-wrapper, .ion-loading-overlay"));
            return loaders.Any(l => l.Displayed);
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    // Gives the sync overlay a short window to appear, then waits (up to timeoutSeconds) for it to clear
    public void WaitForSyncToComplete(int appearSeconds = 3, int timeoutSeconds = 180)
    {
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(appearSeconds)).Until(IsSyncOverlayDisplayed);
            Console.WriteLine("Sync spinner detected on screen. Waiting for database sync completion...");
        }
        catch (WebDriverTimeoutException)
        {
            return;
        }

        new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(d => !IsSyncOverlayDisplayed(d));

        // Give the background thread pool 2 seconds to ensure no late micro-sync operations pop back up
        Console.WriteLine("Sync completed. Stability hold active...");
        Thread.Sleep(2000);
    }

    // After the initial sync the app pins the side menu open as an ion-split-pane (when = true) on the phone, squeezing
    // the agreement/dashboard pages and pushing I AGREE off-screen. A pinned split pane ignores taps and swipes, so it
    // can only be collapsed from the DOM.
    private const string CollapseSideMenuScript =
        "const pane = document.querySelector('ion-split-pane.split-pane-visible');" +
        "if (pane) pane.when = false;" +
        "const menu = document.querySelector('ion-menu.show-menu');" +
        "if (menu) menu.close(false);" +
        "return !!(pane || menu);";

    public void CloseSideMenuIfOpen()
    {
        if (Driver is AndroidDriver { Context: "NATIVE_APP" }
            && !Driver.FindElements(By.XPath("//*[@text='Log Out']")).Any(e => e.Displayed)) return;

        if (ExecuteInWebView(CollapseSideMenuScript) is not true) return;
        Console.WriteLine("📱 Side menu was pinned open. Collapsed it.");
        Thread.Sleep(600); // Layout/animation settle
    }

    // Runs a script in the app's DOM. Native page objects stay in NATIVE_APP, so on mobile this hops into the
    // WebView just for the script and back
    protected object? ExecuteInWebView(string script)
    {
        if (Driver is AndroidDriver { Context: "NATIVE_APP" } androidDriver)
        {
            DriverFactory.SwitchToWebViewContext(androidDriver);
            try
            {
                return androidDriver.ExecuteScript(script);
            }
            finally
            {
                androidDriver.Context = "NATIVE_APP";
            }
        }
        return ((IJavaScriptExecutor)Driver).ExecuteScript(script);
    }

    // Ionic hash route of the page on screen, e.g. "#/termsOfService"
    protected string CurrentRoute() => ExecuteInWebView("return location.hash;") as string ?? string.Empty;

    protected void EnterText(By locator, string text)
    {
        var element = Find(locator);

       // 💡 Check if we are running in Web mode (not an Android Appium Session) before attempting to read browser tag properties
        if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver && 
            element.TagName.StartsWith("ion-", StringComparison.OrdinalIgnoreCase))
        {
            // Pierce into the native element child context to perform safe interactions inside Web mode
            var nativeInput = element.FindElement(By.CssSelector("input, textarea"));
            nativeInput.Clear();
            nativeInput.SendKeys(text);
        }
        else
        {
            // Standard action loop fallback execution path for pure HTML fields and mobile emulators
            element.Clear();
            element.SendKeys(text);
        }
    }
}