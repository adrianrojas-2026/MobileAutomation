using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;

// RecoveryTrac Driver app entry screens: Welcome (#/login) > Tutorial (#/tutorial) > Home
public class DriverOnboardingPage : BasePage
{
    public DriverOnboardingPage(IWebDriver driver)
        : base(driver)
    {
    }

    private bool IsMobile => Driver is AndroidDriver;

    // Button captions are uppercased by CSS, so the DOM/accessibility text may be "Get Started" or "GET STARTED"
    private static string TextEquals(string attribute, string text) =>
        $"normalize-space(translate({attribute}, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'))='{text.ToUpperInvariant()}'";

    private By ButtonByText(string text) => IsMobile
        ? By.XPath($"//android.widget.Button[{TextEquals("@text", text)}] | //android.view.View[@clickable='true' and {TextEquals("@text", text)}]")
        : By.XPath($"//ion-button[{TextEquals(".", text)}] | //button[{TextEquals(".", text)}]");

    private By GetStartedButton => ButtonByText("Get Started");
    private By SkipButton => ButtonByText("Skip");

    // Where the app is, for failure messages: the URL on the web, the current activity on a device (the native
    // context has no URL)
    public string DescribeLocation()
    {
        try
        {
            return Driver is AndroidDriver android ? $"activity: {android.CurrentActivity}" : $"URL: {Driver.Url}";
        }
        catch (WebDriverException)
        {
            return "location unknown";
        }
    }

    // On the web, Ionic leaves the Welcome page (app-login) in the DOM un-hidden behind Home, so its GET STARTED
    // still reports Displayed; the route (/login > /tutorial > /home) is what tells the screens apart
    private bool IsOnRoute(string route) => new Uri(Driver.Url).AbsolutePath.StartsWith(route, StringComparison.OrdinalIgnoreCase);

    private bool WaitForScreen(string route, By marker, int timeoutSeconds)
    {
        if (IsMobile) return FindVisible(marker, timeoutSeconds) != null;
        try
        {
            // Presence, not Displayed: on a wide window the Welcome card sits below the fold, and Ionic reports content
            // outside its visible scroll area as not displayed (the click scrolls it into view)
            return new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds))
                .Until(d => IsOnRoute(route) && d.FindElements(marker).Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    public bool IsWelcomeScreenDisplayed(int timeoutSeconds = 10) => WaitForScreen("/login", GetStartedButton, timeoutSeconds);

    // Newer Android images (13+) draw SKIP under the status bar and leave it out of the native hierarchy, so on a
    // device the tutorial is also recognised by its slides
    private By TutorialMarker => IsMobile
        ? By.XPath($"{SkipButton.Criteria} | //android.widget.Image[starts-with(@text, 'tutorial-step')]")
        : SkipButton;

    public bool IsTutorialDisplayed(int timeoutSeconds = 10) => WaitForScreen("/tutorial", TutorialMarker, timeoutSeconds);

    // Home's main button: "Scan QR Code" (Driver 1.2+) or "Scan Load Code" (1.1.x) with no load, "Scan Receipt Code"
    // with an active load
    private By HomeScanButton => By.XPath(string.Join(" | ",
        new[] { "Scan QR Code", "Scan Load Code", "Scan Receipt Code" }.Select(caption => ButtonByText(caption).Criteria)));

    public bool IsHomeDisplayed(int timeoutSeconds = 15) => WaitForScreen("/home", HomeScanButton, timeoutSeconds);

    public void ClickGetStarted() => ClickWhenVisible(GetStartedButton, "GET STARTED");

    public void SkipTutorial()
    {
        if (IsMobile && FindVisible(SkipButton, 3) == null)
        {
            SkipTutorialInWebView();
        }
        else
        {
            ClickWhenVisible(SkipButton, "SKIP");
        }

        // The tutorial slides animate out; wait for them to disappear so the next step lands on Home
        var closedWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        closedWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        closedWait.Until(d => (!IsMobile && !IsOnRoute("/tutorial")) || !d.FindElements(TutorialMarker).Any(e => e.Displayed));
    }

    // SKIP is missing from the native hierarchy (Android 13+), but always exists in the app's DOM: tap it there and
    // return to the native context the rest of onboarding uses
    private void SkipTutorialInWebView()
    {
        var android = (AndroidDriver)Driver;
        Console.WriteLine("🚚 Driver app: SKIP is not in the native view. Tapping it in the WebView...");
        MobileAutomation.Tests.Drivers.DriverFactory.SwitchToWebViewContext(android);
        try
        {
            var skip = new WebDriverWait(android, TimeSpan.FromSeconds(10)).Until(d =>
                d.FindElements(By.XPath($"//ion-button[{TextEquals(".", "Skip")}] | //button[{TextEquals(".", "Skip")}]")).FirstOrDefault());
            ((IJavaScriptExecutor)android).ExecuteScript("arguments[0].click();", skip);
        }
        finally
        {
            android.Context = "NATIVE_APP";
        }
    }

    // Get Started > Skip. With noReset the emulator can already be past onboarding, so each screen is optional
    public void CompleteOnboardingIfPresent(int appStartTimeoutSeconds = 60)
    {
        // A freshly launched browser/emulator can take well over 10s to render the first screen, so wait for
        // whichever of Welcome / Tutorial / Home shows up before deciding what is left to do
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(appStartTimeoutSeconds)).Until(_ =>
                IsWelcomeScreenDisplayed(1) || IsTutorialDisplayed(1) || IsHomeDisplayed(1));
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine($"🚚 Driver app: no Welcome, Tutorial or Home screen within {appStartTimeoutSeconds}s ({DescribeLocation()}).");
        }

        if (IsWelcomeScreenDisplayed(2))
        {
            Console.WriteLine("🚚 Driver app: Welcome screen detected. Tapping GET STARTED...");
            ClickGetStarted();
        }
        else
        {
            Console.WriteLine("🚚 Driver app: Welcome screen not shown. Assuming onboarding was already completed.");
        }

        if (IsTutorialDisplayed())
        {
            Console.WriteLine("🚚 Driver app: Tutorial detected. Tapping SKIP...");
            SkipTutorial();
        }
    }

    private void ClickWhenVisible(By locator, string caption)
    {
        // ScrollAndClick relies on JS, which the native Android context can't run
        if (!IsMobile)
        {
            ScrollAndClick(locator, jsClickWhenIntercepted: true);
            return;
        }

        var button = FindVisible(locator, 15)
            ?? throw new NoSuchElementException($"The Driver app '{caption}' button was not displayed.");
        button.Click();
    }
}
