using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Drivers;

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

    public void LoginTrac(string username, string password)
    {
        EnterText(EmailField, username);
        EnterText(PasswordField, password);
        Click(LogInButton);
    }

    // 💡 Encapsulated method containing your condition logic and spinner sync handling
    public void LoginIfNeeded(string username, string password)
    {
        Console.WriteLine("Checking if Login screen is present...");
        bool loginRequired = true;
        try
        {
            // Create a quick 4-second wait to poll for screen presence state safely
            var entryWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(4));
            
            // 💡 FIX: Ignore NoSuchElementException inside the wait loop so it drops to the catch block on failure instead of crashing
            entryWait.IgnoreExceptionTypes(typeof(NoSuchElementException));
            
            loginRequired = entryWait.Until(d => 
            {
                var elements = d.FindElements(EmailField);
                return elements.Count > 0 && elements.First().Displayed;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login field not found or timed out ({ex.Message}). Assuming session is already authenticated.");
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
