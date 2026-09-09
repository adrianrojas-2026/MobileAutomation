using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Pages;

public class LoginPage : BasePage
{
    public LoginPage(AndroidDriver driver)
        : base(driver)
    {
    }

    private By UserId => By.Id("ctlSignon_txtUserID");

    private By PasswordField => By.Id("ctlSignon_txtPassword");
    
    private By SignIn =>
        By.XPath("//button[contains(.,'Sign In')]");
    
    private By LogIn =>
        By.XPath("//android.widget.Button[@text='LOG IN']");
    
    private By EmailField => By.ClassName("android.widget.EditText");
    private By PasswordTracField =>
            By.XPath("(//android.widget.EditText)[2]");

    public void Login(
        string username,
        string password)
    {
        EnterText(UserId, username);

        EnterText(PasswordField, password);

        Click(SignIn);
    }

    public void LoginTrac(
        string username,
        string password)
    {
        
        EnterText(EmailField, username);

        EnterText(PasswordTracField, password);

        Click(LogIn);
    }
}