using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Utilities;

namespace MobileAutomation.Tests.Pages;

public class LoginPage : BasePage
{
    public LoginPage(IWebDriver driver)
        : base(driver)
    {
    }

    // 💡 Prioritizes standard web input types and placeholders for high-speed desktop runs
    private By EmailField => By.XPath(
        "//input[@type='email'] | //input[@placeholder='Email'] | //ion-input[@placeholder='Email'] | (//android.widget.EditText)");

    private By PasswordField => By.XPath(
        "//input[@type='password'] | //input[@placeholder='Password'] | //ion-input[@type='password'] | (//android.widget.EditText)[2]");
    
    private By LogInButton => By.XPath(
        "//ion-button[@type='submit'] | //button[@type='submit'] | //ion-button[contains(.,'LOG IN')] | //button[contains(.,'LOG IN')] | //android.widget.Button[@text='LOG IN']");

    // Screens that only exist once a session is authenticated: agreements, initial sync, side menu, dashboard
    private By LoggedInMarker => Driver is OpenQA.Selenium.Appium.Android.AndroidDriver
        ? By.XPath(
            "//*[contains(@text, 'AGREE')] | //*[contains(@text, 'Performing initial sync')] | //*[contains(@text, 'Syncing latest data')]" +
            " | //*[@text='Log Out'] | //android.widget.Button[contains(@text, 'Clock')] | //*[contains(@text, 'DEMO-TESTING ONLY')]" +
            " | //*[contains(@text, 'Activity Log')] | //*[contains(@text, 'SELECT A PROJECT')]")
        : By.CssSelector("ion-menu-button, ion-split-pane.split-pane-visible, ion-loading");

    // The login form is the one screen with both a "LOG IN" button and the email box
    private bool IsLoginFormShown(int timeoutSeconds)
    {
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            return wait.Until(d => d.FindElements(LogInButton).Any(e => e.Displayed) && d.FindElements(EmailField).Any(e => e.Displayed));
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    private static string Describe(IWebElement element)
    {
        try
        {
            var text = element.Text.Trim();
            return text.Length > 60 ? text[..60] + "..." : text.Length > 0 ? text : element.TagName;
        }
        catch (WebDriverException)
        {
            return "?";
        }
    }

    public void LoginTrac(string username, string password)
    {
        EnterText(EmailField, username);
        EnterText(PasswordField, password);
        Click(LogInButton);
    }

    // 💡 Encapsulated method containing your condition logic and spinner sync handling
    public void LoginIfNeeded(string username, string password, int appStartTimeoutSeconds = 45)
    {
        Console.WriteLine("Checking if Login screen is present...");
        bool loginRequired;
        try
        {
            // The app can take well over a few seconds to boot on an emulator, so wait for whichever screen shows up
            // first: the login form or a logged-in one. Logged-in markers are checked first because the mobile
            // Email locator matches any text box
            var entryWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(appStartTimeoutSeconds));
            entryWait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            string screen = entryWait.Until(d =>
            {
                // Android permission dialogs (location, notifications) pop up while the app boots and hide the whole
                // app from the hierarchy, so clear them on every poll
                if (d is OpenQA.Selenium.Appium.Android.AndroidDriver android)
                {
                    PermissionHelper.HandleSystemPermissions(android);
                }

                var marker = d.FindElements(LoggedInMarker).FirstOrDefault(e => e.Displayed);
                if (marker != null) return $"logged-in ('{Describe(marker)}')";
                if (d.FindElements(EmailField).Any(e => e.Displayed)) return "login";
                return null;
            });
            loginRequired = screen == "login";

            // A saved (noReset) but expired session briefly restores a logged-in screen, then bounces to Log In
            if (!loginRequired)
            {
                Console.WriteLine($"Logged-in screen detected: {screen}. Confirming the session is not redirected to Log In...");
                loginRequired = IsLoginFormShown(timeoutSeconds: 5);
            }
            Console.WriteLine(loginRequired ? "Login form is displayed." : "Session is already authenticated.");
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine($"Neither the login form nor a logged-in screen appeared within {appStartTimeoutSeconds}s. Assuming session is already authenticated.");
            loginRequired = false;
        }

        if (loginRequired)
        {
            Console.WriteLine("Executing complete login credentials workflow...");
            LoginTrac(username, password);

            // Synchronize on Initial Sync Spinner immediately following submission
            WaitForSyncToComplete();
        }
    }
}
