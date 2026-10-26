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
                // Web Browser path remains exactly the same
                return By.XPath("//ion-item[contains(., 'Work State')] | //ion-row[contains(., 'Work State')]");
            }
            
            // Mobile Android: Explicitly find a text view containing 'Work State' and get its adjacent interactable field,
            return By.XPath("//*[contains(@text, 'Work State')]/following-sibling::android.widget.Spinner " +
                        "| //*[contains(@text, 'Work State')]/following-sibling::android.view.View " +
                        "| //*[contains(@text, 'Work State')]/..//*[@clickable='true']");
        }
    }

    public By WorkStateOption
    {
        get
        {
            if (!DriverFactory.DriverFlex.GetType().Name.Contains("AndroidDriver"))
            {
                // Web Browser path
                return By.XPath("//ion-select-option | //ion-alert//button | //*[contains(text(), 'FL')]");
            }
            
            // Mobile Android: Look universally for any selectable view option text items in the dropdown popup list
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
            
            // Alternative fallback version if the ID numbers shift dynamically:
            return By.XPath("//android.view.View[@resource-id='projectTask28'] | //*[@resource-id='projectTask28']");
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
            return By.XPath("//*[contains(@text, 'Demo Task')] | //android.widget.CheckedTextView[contains(@text, 'Demo Task')] | (//android.widget.ListView/*)[3]");
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
            
            return By.XPath("//*[contains(@text, 'Labor Category')]/following-sibling::android.view.View " +
                        "| //*[contains(@text, 'Labor Category')]/..//android.widget.Spinner " +
                        "| //*[contains(@text, 'Labor Category')]/..//*[@clickable='true']");
        }
    }

    public By ProjectManagerOption
    {
        get
        {
            if (DriverFactory.DriverFlex is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                // Web Browser locator: option inside the searchable picker. 'PROJECT MANAGER ASSIGNED' is only offered for
                // pre-assigned tasks; 'Demo Labor Category' is offered for 'Demo Task' when the assignment is picked manually.
                return By.XPath(
                    "//app-ion-select-searchable//ion-item[.//ion-label[contains(normalize-space(.), 'PROJECT MANAGER ASSIGNED')]] " +
                    "| //app-ion-select-searchable//ion-item[.//ion-label[contains(normalize-space(.), 'Demo Labor Category')]]");
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
        // The initial sync overlay blocks the dashboard (and the pencil) until it finishes
        WaitForSyncToComplete();
        CloseSideMenuIfOpen();

        // 1. Guard Clause: Skip if already configured
        if (IsProjectAlreadyConfigured())
        {
            Console.WriteLine("⏩ Project configuration is already active. Skipping assignment steps.");
            return;
        }

        Console.WriteLine("🛠️ Project unassigned. Starting form setup workflow...");

        // 2. Open the Assignment Modal
        OpenAssignmentModal(wf);
        
        // 3. Run Platform-Specific Preamble: Project and Task must be chosen before Labor Category is enabled
        bool isMobileMode = IsMobileSession();
        if (isMobileMode)
        {
            HandleMobileFormPreamble(wf);
        }
        else
        {
            HandleWebFormPreamble(wf);
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
        // 1. Click the edit assignment pencil icon to bring up the modal
        var pencilBtn = wf.Until(d => d.FindElements(EditAssignmentPencil).FirstOrDefault());
        pencilBtn.Click();
        System.Threading.Thread.Sleep(800); // Animation buffer

        // 2. Wait out the modal rendering sequence safely across both platforms
        wf.Until(d => 
        {
            try 
            {
                if (!IsMobileSession())
                {
                    // Web Browser: Target the header web DOM elements cleanly
                    var webHeader = d.FindElement(By.XPath("//ion-header//*[contains(translate(text(), 'ASSIGNMENT', 'assignment'), 'assignment')] | //div[contains(@class, 'modal')] | //*[text()='Change Assignment']"));
                    return webHeader != null && webHeader.Displayed;
                }
                else
                {
                    // Mobile Android Emulator: Search directly for the visible text view layer string
                    var mobileHeader = d.FindElement(By.XPath("//android.widget.TextView[@text='Change Assignment'] | //*[contains(@text, 'Change Assignment')]"));
                    return mobileHeader != null && mobileHeader.Displayed;
                }
            }
            catch (NoSuchElementException) 
            { 
                return false; // Keep polling until the overlay layout completely animation stabilizes
            }
        });
        
        System.Threading.Thread.Sleep(500);
    }

    // Mobile: value label of the 'Position' field shown instead of Task + Labor Category for accounts with a
    // pre-assigned task (the WebView exposes the HTML id as resource-id)
    private static readonly By MobilePositionValue = By.XPath("//*[@resource-id='projectTask9']");

    // The field itself has no text; the chosen position is in its child TextView
    private static string GetMobilePositionText() =>
        DriverFactory.DriverFlex.FindElements(By.XPath("//*[@resource-id='projectTask9']//android.widget.TextView"))
            .Select(e => e.Text?.Trim() ?? string.Empty).FirstOrDefault(t => t.Length > 0) ?? string.Empty;

    private void HandleMobileFormPreamble(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        string positionText = GetMobilePositionText();
        if (positionText.Length > 0)
        {
            Console.WriteLine($"⏩ Assignment pre-filled (Position '{positionText}'). Skipping Project/Task selection.");
            return;
        }

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

        // Projects with positions (field activities, e.g. 474) show Position instead of Task. An account that was
        // never clocked in (e.g. the Node3 user) gets here with Position empty, so wait for whichever field appears
        string nextField = wf.Until(d =>
            d.FindElements(MobilePositionValue).Any(e => e.Displayed) ? "position"
            : d.FindElements(TaskDropdown).Any(e => e.Displayed) ? "task"
            : null);
        if (nextField == "position")
        {
            Console.WriteLine("⏩ Project uses positions. Skipping Task selection.");
            return;
        }

        // Select Task
        var taskField = wf.Until(d => d.FindElement(TaskDropdown));
        taskField.Click();
        System.Threading.Thread.Sleep(500);

        wf.Until(d => 
        {
            try
            {
                // Find the specific task element in the open list
                var elements = d.FindElements(By.XPath("//*[contains(@text, 'FRANK-TEST')]"));
                
                if (elements.Count > 0 && elements[0].Displayed)
                {
                    elements[0].Click(); // Perform the selection
                    return true;        // Exit the wait loop successfully
                }
                return false; // Keep waiting if the element isn't visible yet
            }
            catch (Exception)
            {
                return false; 
            }
        });

        System.Threading.Thread.Sleep(500); // Micro stabilization pause
    }

    // Web Browser: a fresh browser profile has no cached assignment while the user is clocked out,
    // so Project starts empty and Task / Labor Category stay hidden until a Project is picked.
    private void HandleWebFormPreamble(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        string projectText = wf.Until(d => d.FindElement(WebAssignmentFieldValue("Project"))).Text.Trim();
        if (projectText.Contains("DEMO-TESTING ONLY"))
        {
            Console.WriteLine($"⏩ Project already selected ('{projectText}'). Skipping Project/Task selection.");
            return;
        }

        Console.WriteLine("🌐 Web mode: Project is empty. Selecting Project and Task fields...");
        SelectWebAssignmentOption(wf, "Project", "DEMO-TESTING ONLY");
        SelectWebAssignmentOption(wf, "Task", "Demo Task");
    }

    // Value label of a Change Assignment field (Project, Task, Labor Category, Work State)
    private static By WebAssignmentFieldValue(string fieldLabel) => By.XPath(
        $"//app-project-task-selection//ion-col[contains(@class,'change-project')][.//ion-label[normalize-space()='{fieldLabel}']]" +
        "//ion-label[contains(@class,'projectLabel')]");

    // optionText null picks the first option in the list
    private void SelectWebAssignmentOption(OpenQA.Selenium.Support.UI.WebDriverWait wf, string fieldLabel, string? optionText)
    {
        wf.Until(d =>
        {
            try
            {
                var field = d.FindElement(WebAssignmentFieldValue(fieldLabel));
                if (field.Displayed) { field.Click(); return true; }
                return false;
            }
            catch (Exception) { return false; }
        });

        wf.Until(d =>
        {
            try
            {
                // Options are role=listitem; the picker's first ion-item is its "Search:" input
                var option = d.FindElements(By.XPath(optionText == null
                        ? "//app-ion-select-searchable//ion-item[@role='listitem']"
                        : $"//app-ion-select-searchable//ion-item[@role='listitem'][.//ion-label[contains(normalize-space(.),'{optionText}')]]"))
                    .FirstOrDefault(o => o.Displayed);
                if (option == null) return false;
                option.Click();
                return true;
            }
            catch (Exception) { return false; }
        });

        // Wait for the picker to close and the field to show the chosen value
        wf.Until(d =>
        {
            string value = d.FindElement(WebAssignmentFieldValue(fieldLabel)).Text.Trim();
            return optionText == null ? value.Length > 0 : value.Contains(optionText);
        });
    }

    private void SelectLaborCategory(OpenQA.Selenium.Support.UI.WebDriverWait wf)
    {
        // Web: accounts with a pre-assigned task (e.g. the Node2 user) open the modal already filled in, showing a
        // 'Position' field instead of 'Labor Category'. CHANGE only submits once a pick re-validates the form, so
        // re-pick the current Position
        if (!IsMobileSession())
        {
            string positionText = DriverFactory.DriverFlex.FindElements(WebAssignmentFieldValue("Position"))
                .FirstOrDefault(e => e.Displayed)?.Text.Trim() ?? string.Empty;
            if (positionText.Length > 0)
            {
                Console.WriteLine($"🌐 Position already set ('{positionText}'). Re-selecting it so the form validates.");
                SelectWebAssignmentOption(wf, "Position", positionText);
                return;
            }
        }
        else if (DriverFactory.DriverFlex.FindElements(MobilePositionValue).Any(e => e.Displayed))
        {
            string positionText = GetMobilePositionText();
            if (positionText.Length > 0)
            {
                Console.WriteLine($"📱 Position already set ('{positionText}'). Re-selecting it so the form validates.");
            }
            else
            {
                // Same pick as the Labor Category path (ProjectManagerOption)
                positionText = "PROJECT MANAGER";
                Console.WriteLine($"📱 Position is empty. Selecting '{positionText}'...");
            }
            wf.Until(d => d.FindElements(MobilePositionValue).FirstOrDefault(e => e.Displayed)).Click();
            System.Threading.Thread.Sleep(800);

            // The picker lists the options over the modal; skip the field label it was opened from
            wf.Until(d =>
            {
                try
                {
                    var option = d.FindElements(By.XPath($"//*[contains(@text, '{positionText}')][not(ancestor-or-self::*[@resource-id='projectTask9'])]"))
                        .LastOrDefault(o => o.Displayed);
                    if (option == null) return false;
                    option.Click();
                    return true;
                }
                catch (Exception) { return false; }
            });
            System.Threading.Thread.Sleep(800);
            return;
        }

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

        // Web: Work State is the same picker as Project/Task. The generic WorkStateDropdown locator matches the
        // container wrapping the whole form, so reading/clicking it neither sees the value nor opens the picker
        if (!IsMobileSession())
        {
            string webStateText = wf.Until(d => d.FindElement(WebAssignmentFieldValue("Work State"))).Text.Trim();
            if (webStateText.Length > 0)
            {
                Console.WriteLine($"⏩ Work State already selected ('{webStateText}'). Skipping.");
                return;
            }
            Console.WriteLine("🌐 Web mode: Work State is empty. Selecting 'Florida'...");
            SelectWebAssignmentOption(wf, "Work State", "Florida");
            return;
        }

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

        // Web: an invalid form keeps the modal open behind an 'All fields are required.' alert; fail here with that
        // message instead of timing out later on whatever the open modal blocks
        if (!IsMobileSession())
        {
            var driver = DriverFactory.DriverFlex;
            var alert = driver.FindElements(By.XPath("//ion-alert[contains(., 'required')]")).FirstOrDefault(a => a.Displayed);
            if (alert != null)
            {
                throw new InvalidOperationException($"Change Assignment was rejected: '{alert.Text.Trim()}'.");
            }
            wf.Until(d => !d.FindElements(By.TagName("app-change-project")).Any(m => m.Displayed));
        }
    }

}
