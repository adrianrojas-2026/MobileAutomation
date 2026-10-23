using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Pages;

public class PrivacyPolicyPage : BasePage
{
    public PrivacyPolicyPage(IWebDriver driver) 
        : base(driver) 
    { 
    }
    public By AgreeButton
    {
        get
        {
            if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                return By.CssSelector("ion-button[slot='end'], ion-button.right-button");
            }
            
            // Case-insensitive and not tied to android.widget.Button: newer builds expose the caption as "I Agree" (the
            // capitals are CSS) and the button as a clickable View. Exact caption, so the "END USER LICENSE AGREEMENT"
            // title doesn't match
            const string caption = "translate(normalize-space(@text), 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')";
            return By.XPath(
                $"//android.widget.Button[{caption}='I AGREE' or {caption}='AGREE'] " +
                $"| //*[@clickable='true'][{caption}='I AGREE' or {caption}='AGREE']");
        }
    }

    // Clicks the agreement's I AGREE inside the app's DOM: the floating chat bubble overlaps it, so a native tap at
    // the button's centre can land on the bubble instead
    private const string HasAgreeButtonInDomScript =
        "return [...document.querySelectorAll('ion-button, button')].some(b => {" +
        "  const t = (b.innerText || b.textContent || '').trim().toUpperCase();" +
        "  const r = b.getBoundingClientRect();" +
        "  return (t === 'I AGREE' || t === 'AGREE') && r.width > 0 && r.height > 0;" +
        "});";

    private const string ClickAgreeInDomScript =
        "const btn = [...document.querySelectorAll('ion-button, button')].reverse().find(b => {" +
        "  const t = (b.innerText || b.textContent || '').trim().toUpperCase();" +
        "  const r = b.getBoundingClientRect();" +
        "  return (t === 'I AGREE' || t === 'AGREE') && r.width > 0 && r.height > 0;" +
        "});" +
        "if (!btn) return false;" +
        "btn.click();" +
        "return true;";

    public void ClickAgree()
    {
        if (Driver is OpenQA.Selenium.Appium.Android.AndroidDriver && ExecuteInWebView(ClickAgreeInDomScript) is true)
        {
            Console.WriteLine("📱 Mobile Mode: Clicked 'I AGREE' inside the WebView.");
            return;
        }

        // 🌐 WEB ONLY WORKFLOW
        if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver && Driver is IJavaScriptExecutor js)
        {
            var scriptQuery = "return document.querySelector('ion-buttons[slot=\"end\"] ion-button') || document.querySelector('ion-button.right-button') || document.querySelector('ion-button');";
            var element = js.ExecuteScript(scriptQuery) as IWebElement;
            if (element != null)
            {
                element.Click();
                return;
            }
        }

        // 📱 MOBILE WORKFLOW
        Console.WriteLine("📱 Mobile Mode: Triggering explicit click sequence on the native button widget...");
        var buttonElement = Driver.FindElement(AgreeButton);
        
        // A standard click works perfectly once locked onto the true android.widget.Button node
        buttonElement.Click();
    }
    private static bool IsPostLoginRoute(string route) =>
        route.Contains("privacyPolicy", StringComparison.OrdinalIgnoreCase)
        || route.Contains("termsOfService", StringComparison.OrdinalIgnoreCase)
        || route.StartsWith("#/app/", StringComparison.OrdinalIgnoreCase);

    // Returns the route once the app leaves login/sync, or whatever route it is on when the timeout elapses
    private string WaitForPostLoginRoute(int timeoutSeconds)
    {
        string route = CurrentRoute();
        var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
        while (!IsPostLoginRoute(route) && DateTime.Now < deadline)
        {
            Thread.Sleep(1000);
            try
            {
                route = CurrentRoute();
            }
            catch (WebDriverException)
            {
                // WebView context briefly unavailable while the app navigates - poll again
            }
        }
        if (!IsPostLoginRoute(route))
        {
            Console.WriteLine($"App is still on '{route}' after {timeoutSeconds}s; checking for agreements anyway.");
        }
        return route;
    }

    public void ClearAllPendingAgreements()
    {
        Console.WriteLine("Scanning for active agreement screens (Privacy Policy / Terms of Service)...");
        bool agreementsPending = true;
        int agreementCounter = 0;
        int attempts = 0;

        while (agreementsPending && agreementCounter < 3 && attempts++ < 6)
        {
            // The initial sync overlay can sit on top of the agreement screen (even when no login happened this run);
            // AGREE taps are swallowed until it clears
            // On a fresh install the sync starts several seconds after login, so the first pass watches longer for it
            WaitForSyncToComplete(appearSeconds: agreementCounter == 0 ? 10 : 3);
            CloseSideMenuIfOpen();

            try
            {
                // After a fresh-install login the app sits on the login/sync route for a while before it routes to the
                // agreements, so wait until it lands on an agreement or an in-app route before deciding
                string route = WaitForPostLoginRoute(timeoutSeconds: agreementCounter == 0 ? 60 : 15);

                // Still on an agreement route: the long EULA (Terms of Service) can take well over 5s to render its
                // I AGREE button on an emulator, so give it longer before concluding there is nothing to accept
                bool onAgreementRoute = route.Contains("privacyPolicy", StringComparison.OrdinalIgnoreCase)
                                        || route.Contains("termsOfService", StringComparison.OrdinalIgnoreCase);
                var agreementWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(onAgreementRoute ? 30 : 5));

                // "native", "dom" or null; WebDriverWait needs a reference type
                var agreeBtn = agreementWait.Until(d =>
                {
                    // 💡 FIX: Use FindElements to safely return an empty collection instead of crashing the runner thread
                    var elements = d.FindElements(AgreeButton);
                    if (elements.Count > 0)
                    {
                        var el = elements.First();
                        if (el != null && el.Displayed && el.Enabled) return "native";
                    }

                    // Some builds don't expose the WebView's I AGREE in the native tree at all
                    if (d is OpenQA.Selenium.Appium.Android.AndroidDriver && onAgreementRoute
                        && ExecuteInWebView(HasAgreeButtonInDomScript) is true)
                    {
                        return "dom";
                    }
                    return null;
                });

                if (agreeBtn != null)
                {
                    Console.WriteLine($"[Agreement #{agreementCounter + 1}] Found active screen. Clicking 'I AGREE'...");
                    string routeBefore = CurrentRoute();
                    ClickAgree();

                    // Only count the agreement once the app navigates away from it: a tap on a half-visible
                    // I AGREE (menu re-pinned by the next page) is silently lost
                    var acceptedWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
                    acceptedWait.IgnoreExceptionTypes(typeof(WebDriverException));
                    try
                    {
                        acceptedWait.Until(_ => CurrentRoute() != routeBefore);
                        agreementCounter++;
                        Console.WriteLine($"[Agreement #{agreementCounter}] Accepted ({routeBefore} -> {CurrentRoute()}).");
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Console.WriteLine($"[Agreement #{agreementCounter + 1}] Still on {routeBefore} after clicking 'I AGREE'. Retrying...");
                    }
                }
                else
                {
                    agreementsPending = false;
                }
            }
            catch (WebDriverTimeoutException)
            {
                var matches = Driver.FindElements(AgreeButton);
                Console.WriteLine("No active agreement screens detected. Moving forward to Dashboard verification. " +
                                  $"(route {CurrentRoute()}, I AGREE matches: [{string.Join(", ", matches.Select(m => $"displayed={m.Displayed} enabled={m.Enabled}"))}])");
                agreementsPending = false;
            }
        }
    }
}
