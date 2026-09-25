using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTracTests
{
    [TestFixture]
    public class CrossScenariosTests
    {
        // 1. Declare the ThreadLocal variables simply without initializing their values here
        private static ThreadLocal<AndroidDriver> _driverFlex = new ThreadLocal<AndroidDriver>();
        private static ThreadLocal<AndroidDriver> _driverDriver = new ThreadLocal<AndroidDriver>();

        // 2. Public properties to expose the drivers to the tests
        public static AndroidDriver DriverFlex => _driverFlex.Value!;
        public static AndroidDriver DriverDriver => _driverDriver.Value!;

        [SetUp]
        public void InicializarEmuladores()
        {
            // Appium Server 2.x
            Uri appiumServerUri = new Uri("http://127.0.0.1:4723/");

            // --- Configuration for RECOVERY TRAC FLEX ---
            var optionsFlex = new AppiumOptions();
            optionsFlex.PlatformName = "Android";
            optionsFlex.AutomationName = "UiAutomator2";
            
            optionsFlex.AddAdditionalAppiumOption("appium:deviceName", "Emulador_Flex_5554");
            optionsFlex.AddAdditionalAppiumOption("appium:udid", "emulator-5554"); // ID from 'adb devices'
            optionsFlex.AddAdditionalAppiumOption("appium:app", @"C:\apks\recovery_trac_flex.apk");
            optionsFlex.AddAdditionalAppiumOption("appium:systemPort", 8201);

            // Correct Assignation: Here set the real value to the first ThreadLocal
            _driverFlex.Value = new AndroidDriver(appiumServerUri, optionsFlex);

            // --- Configuration for RECOVERY TRAC DRIVER ---
            var optionsDriver = new AppiumOptions();
            optionsDriver.PlatformName = "Android";
            optionsDriver.AutomationName = "UiAutomator2";

            optionsDriver.AddAdditionalAppiumOption("appium:deviceName", "Emulador_Flex_5556");
            optionsDriver.AddAdditionalAppiumOption("appium:udid", "emulator-5556"); // ID from 'adb devices'
            optionsDriver.AddAdditionalAppiumOption("appium:app", @"C:\apks\recovery_trac_driver.apk");
            optionsDriver.AddAdditionalAppiumOption("appium:systemPort", 8202); 
            
            // Correct Assignation: Here set the real value to the second ThreadLocal
            _driverDriver.Value = new AndroidDriver(appiumServerUri, optionsDriver);
        }

        [TearDown]
        public void EndSessions()
        {
            // Release secure way the memory for each emulator at the end of each test
            if (_driverFlex.IsValueCreated && _driverFlex.Value != null)
            {
                _driverFlex.Value.Quit();
                _driverFlex.Value.Dispose();
            }

            if (_driverDriver.IsValueCreated && _driverDriver.Value != null)
            {
                _driverDriver.Value.Quit();
                _driverDriver.Value.Dispose();
            }
        }

        [OneTimeTearDown]
        public static void ReleaseStaticContainers()
        {
            // 2.This destroy the local containers 
            // at the end
            _driverFlex?.Dispose();
            _driverDriver?.Dispose();
        }
    }
}
