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
                // Web Browser path fallback
                return By.XPath("//ion-label[contains(text(), 'Work State')] | //*[normalize-space()='Work State']");
            }
            
            // Mobile Android: Targets the projectTask30 container or spinner layout field
            return By.XPath("//android.view.View[@resource-id='projectTask30'] | //android.widget.Spinner[contains(@text, 'Work State')] | //*[contains(@text, 'Work State')]");
        }
    }

    public By WorkStateOption
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                return By.XPath("//*[contains(text(), 'FL')]");
            }
            
            // Mobile Android: Dynamically clicks the first available entry state layout item string row
            return By.XPath("//android.widget.CheckedTextView | //android.view.View[contains(@clickable, 'true')] | //android.widget.ListView/*");
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
        // =========================================================================
        // GUARD CLAUSE: Check if a project is already assigned to skip form steps!
        // =========================================================================
        bool isProjectAlreadyAssigned = false;
        try
        {
            // Check if the dashboard banner is actively displaying your selected text elements
            var bannerElements = DriverFactory.DriverFlex.FindElements(DemoTestingBanner);
            if (bannerElements.Count > 0 && bannerElements[0].Displayed)
            {
                string bannerText = bannerElements[0].Text;
                // If it already says DEMO-TESTING, we don't need to re-configure it!
                if (bannerText.Contains("DEMO-TESTING") || bannerText.Contains("Demo Labor Category"))
                {
                    isProjectAlreadyAssigned = true;
                }
            }
        }
        catch (Exception)
        {
            // Element not found or profile is unassigned; proceed to select details below
            isProjectAlreadyAssigned = false;
        }

        if (isProjectAlreadyAssigned)
        {
            Console.WriteLine("⏩ Project configuration is already active on the dashboard. Validating and skipping assignment steps.");
            return; // Exits the method instantly, protecting your workflow loop!
        }

        // =========================================================================
        // ORIGINAL ASSIGNMENT WORKFLOW (Runs only if form initialization is missing)
        // =========================================================================
        Console.WriteLine("🛠️ Project unassigned. Starting step-by-step form setup workflow...");

        // 1. Click the edit assignment pencil icon to bring up the modal
        var pencilBtn = wf.Until(d => d.FindElement(EditAssignmentPencil));
        pencilBtn.Click();

        // Wait safely for the modal container to animate into view
        System.Threading.Thread.Sleep(800);
        
        // Explicitly identify if we are targeting mobile based on your framework wrapper setup
        bool isMobileMode = DriverFactory.DriverFlex?.GetType().Name.Contains("AndroidDriver") == true
                    || DriverFactory.DriverFlex?.ToString()?.Contains("Android") == true;
        
        wf.Until(d => 
        {
            try 
            {
                if (!isMobileMode)
                {
                    var webHeader = d.FindElement(By.XPath("//ion-header//*[contains(translate(text(), 'ASSIGNMENT', 'assignment'), 'assignment')] | //div[contains(@class, 'modal')] | //*[text()='Change Assignment']"));
                    return webHeader != null && webHeader.Displayed;
                }
                else
                {
                    var mobileHeader = d.FindElement(By.XPath("//android.widget.TextView[@text='Change Assignment'] | //*[contains(@text, 'Assignment')]"));
                    return mobileHeader != null && mobileHeader.Displayed;
                }
            }
            catch (NoSuchElementException) 
            {
                return false; 
            }
        });
        
        System.Threading.Thread.Sleep(500);

        // =========================================================================
        // MOBILE ONLY STEP CONTAINER
        // =========================================================================
        if (isMobileMode)
        {
            Console.WriteLine("📱 Mobile mode detected. Selecting the Project option field...");
            
            // A. Click the empty 'Project' input container slot to open selection overlay
            var projDropdown = wf.Until(d => d.FindElement(ProjectDropdown));
            projDropdown.Click();
            System.Threading.Thread.Sleep(500);
            
            // B. Wait and interactively target the choice selection list string
            wf.Until(d => 
            {
                try
                {
                    var option = d.FindElement(By.XPath("//*[contains(@text, 'DEMO-TESTING')] | //android.widget.CheckedTextView[contains(@text, 'DEMO-TESTING')]"));
                    if (option != null && option.Displayed)
                    {
                        option.Click();
                        return option;
                    }
                    return null;
                }
                catch (Exception) { return null; }
            });
            
            System.Threading.Thread.Sleep(800);

            // =====================================================================
            // MOVED INSIDE: Select the Task field since it is now visible on mobile
            // =====================================================================
            Console.WriteLine("📱 Selecting the mandatory Task field on mobile view...");
            var taskField = wf.Until(d => d.FindElement(TaskDropdown));
            taskField.Click();
            System.Threading.Thread.Sleep(500);

            wf.Until(d =>
            {
                try
                {
                    var taskSel = d.FindElement(TaskOption);
                    if (taskSel != null && taskSel.Displayed)
                    {
                        taskSel.Click();
                        return taskSel;
                    }
                    return null;
                }
                catch (Exception) { return null; }
            });

            System.Threading.Thread.Sleep(800);
            
        } // <--- THIS closing bracket must capture everything mobile-only!

        // =========================================================================
        // SHARED CONTINUATION WORKFLOW (Both Web and Mobile run from here onward)
        // =========================================================================

        // 2. Click the context input field (Labor Category on Web, Position on Mobile)
        var laborDropdown = wf.Until(d => d.FindElement(LaborCategoryDropdown));
        laborDropdown.Click();

        // Small animation buffer pause for the list overlay transition
        System.Threading.Thread.Sleep(800);

         // 3. Select the option item from the modal list overlay
        var pmOption = wf.Until(d => 
        {
            try
            {
                // REMOVED: HideKeyboard() loop call to prevent Appium driver timeout locks
                
                var element = d.FindElement(ProjectManagerOption);
                if (element != null && element.Displayed)
                {
                    element.Click();
                    return element;
                }
                return null;
            }
            catch (ElementNotInteractableException)
            {
                return null; // Menu transition animation running, keep polling
            }
            catch (NoSuchElementException)
            {
                return null; // Element hasn't rendered in layout list tree yet, keep waiting
            }
        });

        // Small pause to let the choice register and populate back into the form field box
        System.Threading.Thread.Sleep(800);
        
        // =========================================================================
        // MOBILE ONLY SUB-STEP: Select the Work State field to clear validation
        // =========================================================================
        if (DriverFactory.DriverFlex?.GetType().Name.Contains("AndroidDriver") == true 
            || DriverFactory.DriverFlex?.ToString()?.Contains("Android") == true)
        {
            Console.WriteLine("📱 Selecting the mandatory Work State dropdown field on mobile layout view...");
            
            var stateField = wf.Until(d => d.FindElement(WorkStateDropdown));
            stateField.Click();
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
                catch (Exception) 
                { 
                    return null; // Keep polling item tray if animation is lagging frame rates
                }
            });

            System.Threading.Thread.Sleep(800);
        }

        // 4. Click the 'CHANGE' button to confirm and close the assignment overlay
        var changeBtn = wf.Until(d => 
        {
            try
            {
                var element = d.FindElement(ChangeSubmitButton);
                if (element != null && element.Displayed && element.Enabled)
                {
                    element.Click();
                    return element;
                }
                return null;
            }
            catch (NoSuchElementException) { return null; }
            catch (ElementNotInteractableException) { return null; }
        });

        System.Threading.Thread.Sleep(1500);
    }

}
