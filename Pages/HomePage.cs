using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Pages;

public class HomePage : BasePage
{
    public HomePage(IWebDriver driver) 
        : base(driver) 
    { 
    }

    public By WorkStateDropdown
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                // Web Browser: Target the overarching ion-item block container that wraps BOTH the label and the input value field
                return By.XPath("//ion-item[contains(., 'Work State')] | //ion-row[contains(., 'Work State')]");
            }
            // Mobile Android path stays exactly the same
            return By.XPath("//android.view.View[@resource-id='projectTask30'] | //android.widget.Spinner[contains(@text, 'Work State')]");
        }
    }

    public By WorkStateOption
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                // Web Browser path: Dynamically targets any available choice option in the web dropdown list popup overlay
                return By.XPath("//ion-select-option | //ion-alert//button | //*[contains(text(), 'FL')] | //ion-item[contains(@id, 'ion-sel')]");
            }
            // Mobile Android path
            return By.XPath("//android.widget.CheckedTextView | //android.view.View[contains(@clickable, 'true')]");
        }
    }

    public By TaskDropdown
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                // Web Browser path
                return By.XPath("//ion-label[contains(text(), 'Task')] | //*[normalize-space()='Task']");
            }
            
            // Mobile Android: Target the specific view resource ID found via Appium Inspector
             return By.XPath("//android.view.View[@resource-id='projectTask28'] | (//android.view.View[@resource-id='projectTask28'])[1] | //android.view.View[@resource-id='projectTask28']");
        }
    }

    public By TaskOption
    {
        get
        {
            // On your platform, selecting any valid task clears the layout. 
            // We will target the first option context item.
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                return By.XPath("//*[contains(text(), '01 -')]");
            }
            return By.XPath("//*[contains(@text, 'Demo Task')] | //android.view.View[contains(@text, 'Demo Task')] | //android.widget.ListView/*[contains(@clickable, 'true')]");
        }
    }

    public By EditAssignmentPencil
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // Web Browser locator
                return By.Id("edit-project");
            }
            // Mobile Android Locator (Targets the resource ID or element text name context)
            return By.XPath("//android.widget.Image[contains(@resource-id, 'edit-project')] | //*[@text='edit-project']");
        }
    }

    // 1. The Project Dropdown Field (Needs to be clicked first on Mobile)
    public By ProjectDropdown
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // Web doesn't need to touch this since it defaults to DEMO-TESTING, 
                // but we provide a dummy fallback just in case.
                return By.XPath("//div[contains(text(), 'Project')]");
            }
            // Mobile Android: Target the Project spinner/field container
            return By.XPath("//android.widget.Spinner[contains(@text, 'Project')] | //*[contains(@text, 'Project')] | //android.view.View[contains(@resource-id, 'project')]");
        }
    }

    // 2. The Second Dropdown Field (Labor Category on Web, Position on Mobile)
    public By LaborCategoryDropdown
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                // Web Browser path
                return By.XPath("//ion-label[contains(text(), 'Labor Category')] | //*[normalize-space()='Labor Category']");
            }
            
            // Mobile Android: Look for a projectTask view element following the task selection field
            return By.XPath("//android.view.View[@resource-id='projectTask29']");
        }
    }

    public By ProjectManagerOption
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // Web Browser locator: Targets the item option text inside the popover overlay
                return By.XPath("//*[contains(text(), 'PROJECT MANAGER ASSIGNED')]");
            }
            // Mobile Android: Target visible items from your screenshot list 
            // (e.g., 'DEVELOPER' or 'Demo Labor Category') or grab any valid option row
            return By.XPath("//*[contains(@text, 'DEVELOPER')] | //*[contains(@text, 'Demo Labor Category')] | //android.view.View[contains(@text, 'DEVELOPER')]");
        }
    }

    public By ChangeSubmitButton
    {
        get
        {
        if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // Web Browser locator: Target the ion-button inside the footer structure using flexible text containing
                return By.XPath("//app-footer-buttons//ion-button[contains(translate(., 'change', 'CHANGE'), 'CHANGE')] | //ion-footer//ion-button[contains(., 'CHANGE')]");
            }
        // Mobile Android Locator
        return By.XPath("//android.widget.Button[@text='CHANGE'] | //*[@text='CHANGE']");
        }
    }

    public By SelectProjectSection => MobileBy.XPath("//android.widget.TextView[contains(@text, 'SELECT A PROJECT')] | //*[contains(@text, 'SELECT A PROJECT')]");
    public By ActivityLogReminder => MobileBy.XPath("//android.widget.TextView[contains(@text, 'Activity Log')] | //*[contains(@text, 'Activity Log')]");
    public By ClockInButton => MobileBy.XPath("//android.widget.Button[@text='Clock In'] | //android.widget.Button[contains(@text, 'Clock In')]");    
    public By DashboardHeaderTitle => By.XPath(
        "//ion-header//*[contains(text(), 'Home')] " +
        "| //*[contains(@class, 'toolbar')]//*[contains(text(), 'Home')] " +
        "| //ion-title " +
        "| //android.widget.TextView[@text='Home']");
   
   // 💡 Dynamically delivers safe browser CSS/XPath selectors or pure mobile @text attributes
    public By DemoTestingBanner
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // 🌐 Web browser target: matches elements containing your text
                return By.XPath("//span[contains(., 'DEMO-TESTING ONLY')]");
            }
            // 📱 Mobile native emulator target: strictly uses @text attributes to prevent server-side crashes
            return By.XPath("//*[@text='DEMO-TESTING ONLY'] | //*[contains(@text, 'DEMO-TESTING ONLY')]");
        }
    }
    
    public By ProjectManagerProfileText
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                return By.XPath("//*[contains(text(), 'clocked')]");
            }
            // Mobile Android: Look universally for any text content tracking the active clock layer
            return By.XPath("//android.widget.TextView[contains(@text, 'clocked')] | //*[contains(@text, 'You are clocked')]");
        }
    }

    public By ClockActionButton
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                return By.XPath("//ion-button[contains(., 'Clock')] | //button[contains(., 'Clock')]");
            }
            return By.XPath("//android.widget.Button[contains(@text, 'Clock')] | //android.widget.Button[@text='Clock Out'] | //android.widget.Button[@text='Clock In']");
        }
    }
    
    public void CompleteProjectAssignment(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        // 1. Guard Clause: Skip if already configured
        if (IsProjectAlreadyConfigured())
        {
            Console.WriteLine("⏩ Project configuration is already active. Skipping assignment steps.");
            return;
        }

        Console.WriteLine("🛠️ Project unassigned. Starting form setup workflow...");

        // 2. Open the Assignment Modal
        OpenAssignmentModal(wf);
        
        // 3. Run Platform-Specific Preamble (Mobile only handles)
        bool isMobileMode = IsMobileSession();
        if (isMobileMode)
        {
            HandleMobileFormPreamble(wf);
        }

        // 4. Shared Step: Select Labor Category / Position
        SelectLaborCategory(wf);

        // 5. Shared Step: Select Work State
        SelectWorkState(wf);

        // 6. Submit Changes
        SubmitAssignmentChange(wf);
    }

    private bool IsMobileSession()
    {
        return DriverFactory.DriverFlex?.GetType().Name.Contains("AndroidDriver") == true
            || DriverFactory.DriverFlex?.ToString()?.Contains("Android") == true;
    }

    private bool IsProjectAlreadyConfigured()
    {
        try
        {
            var bannerElements = DriverFactory.DriverFlex.FindElements(DemoTestingBanner);
            if (bannerElements.Count > 0 && bannerElements[0].Displayed)
            {
                string bannerText = bannerElements[0].Text;
                return bannerText.Contains("DEMO-TESTING") || bannerText.Contains("Demo Labor Category");
            }
        }
        catch (Exception) { /* Handle missing element as unconfigured state */ }
        return false;
    }

    private void OpenAssignmentModal(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        var pencilBtn = wf.Until(d => d.FindElement(EditAssignmentPencil));
        pencilBtn.Click();
        System.Threading.Thread.Sleep(800); // Animation buffer

        wf.Until(d => 
        {
            try 
            {
                var locator = !IsMobileSession() 
                    ? By.XPath("//ion-header//*[contains(translate(text(), 'ASSIGNMENT', 'assignment'), 'assignment')] | //div[contains(@class, 'modal')] | //*[text()='Change Assignment']")
                    : By.XPath("//android.widget.TextView[@text='Change Assignment'] | //*[contains(@text, 'Assignment')]");
                    
                var header = d.FindElement(locator);
                return header != null && header.Displayed;
            }
            catch (NoSuchElementException) { return false; }
        });
        System.Threading.Thread.Sleep(500);
    }

    private void HandleMobileFormPreamble(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        Console.WriteLine("📱 Mobile mode detected. Selecting Project and Task fields...");
        
        // Select Project
        var projDropdown = wf.Until(d => d.FindElement(ProjectDropdown));
        projDropdown.Click();
        System.Threading.Thread.Sleep(500);
        
        wf.Until(d => 
        {
            try
            {
                var option = d.FindElement(By.XPath("//*[contains(@text, 'DEMO-TESTING')] | //android.widget.CheckedTextView[contains(@text, 'DEMO-TESTING')]"));
                if (option != null && option.Displayed) { option.Click(); return option; }
                return null;
            }
            catch (Exception) { return null; }
        });
        System.Threading.Thread.Sleep(800);

        // Select Task
        var taskField = wf.Until(d => d.FindElement(TaskDropdown));
        taskField.Click();
        System.Threading.Thread.Sleep(500);

        wf.Until(d =>
        {
            try
            {
                var taskSel = d.FindElement(TaskOption);
                if (taskSel != null && taskSel.Displayed) { taskSel.Click(); return taskSel; }
                return null;
            }
            catch (Exception) { return null; }
        });
        System.Threading.Thread.Sleep(800);
    }

    private void SelectLaborCategory(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        var laborDropdown = wf.Until(d => d.FindElement(LaborCategoryDropdown));
        laborDropdown.Click();
        System.Threading.Thread.Sleep(800);

        wf.Until(d => 
        {
            try
            {
                var element = d.FindElement(ProjectManagerOption);
                if (element != null && element.Displayed) { element.Click(); return element; }
                return null;
            }
            catch (Exception) { return null; }
        });
        System.Threading.Thread.Sleep(800);
    }

    private void SelectWorkState(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        Console.WriteLine("🌐📱 Validating mandatory Work State field status...");
        
        // 1. Safely extract the outer wrapper text layer first
        var stateField = wf.Until(d => d.FindElement(WorkStateDropdown));
        string currentStateText = stateField.Text?.ToLower().Trim() ?? string.Empty;
        
        Console.WriteLine($"🔍 Extracted Work State text envelope: '{currentStateText}'");

        // =========================================================================
        // GUARD CLAUSE: If 'virginia', 'fl', or any chosen option is present, skip!
        // =========================================================================
        if (currentStateText.Contains("virginia") || 
            currentStateText.Contains("fl") || 
            (currentStateText.Length > 12 && !currentStateText.EndsWith("work state") && !currentStateText.Contains("select")))
        {
            Console.WriteLine($"⏩ Work State evaluates as pre-populated ('{stateField.Text?.Replace("\n", " ").Trim() ?? string.Empty}'). Skipping dropdown interactions.");
            return; // Exits the method instantly so lines 350-381 never run!
        }

        // =========================================================================
        // FALLBACK WORKFLOW: Runs only if the dropdown is genuinely blank/empty
        // =========================================================================
        Console.WriteLine("🌐📱 Work State is blank. Tapping open list overlay...");
        
        wf.Until(d => 
        {
            try
            {
                var element = d.FindElement(WorkStateDropdown);
                if (element != null && element.Displayed)
                {
                    element.Click();
                    return true;
                }
                return false;
            }
            catch (Exception) { return false; }
        });
        
        System.Threading.Thread.Sleep(600);

        wf.Until(d =>
        {
            try
            {
                var stateSel = d.FindElement(WorkStateOption);
                if (stateSel != null && stateSel.Displayed) 
                { 
                    stateSel.Click(); 
                    return stateSel; 
                }
                return null;
            }
            catch (Exception) { return null; }
        });
        
        System.Threading.Thread.Sleep(800);
    }

    private void SubmitAssignmentChange(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        var changeBtn = wf.Until(d => 
        {
            try
            {
                var element = d.FindElement(ChangeSubmitButton);
                if (element != null && element.Displayed && element.Enabled) { element.Click(); return element; }
                return null;
            }
            catch (Exception) { return null; }
        });
        System.Threading.Thread.Sleep(1500);
    }

}
