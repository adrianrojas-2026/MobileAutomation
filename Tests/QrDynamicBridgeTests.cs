using OpenQA.Selenium;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Pages.FlexApp;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class QrDynamicBridgeTests
    {
        [SetUp]
        public void InicializarEscenarioDual()
        {
            // Send two environments configured in your appsettings.json 
            // (Example: One pointing to emulator 5554 and the other to 5556)
            DriverFactory.InitializeDualApps("Android_Flex", "Android_Driver");
        }

        [Test]
        public void Test_Sincronizacion_Dinamica_QR()
        {

            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

            // --- STEP 1: Generate QR en RecoveryTrac Driver (Emulador 1) ---
            driverMapPage.GenerarTicket();
            
            Thread.Sleep(1500); 

            // --- STEP 2: Dynamic Capture in memory (Emulador 1) ---
            Screenshot screenshotEmulador1 =  DriverFactory.DriverDriver.GetScreenshot();
            string qrImagenBase64 = screenshotEmulador1.AsBase64EncodedString;

            // --- STEP 3: Prepare scanner in RecoveryTrac Flex (Emulador 2) ---
            flexMainPage.StartScan();
            Thread.Sleep(1000); 

            // --- STEP 4: Real-time Injection in the Virtual Camera ---
            var scriptArgs = new Dictionary<string, object> { { "payload", qrImagenBase64 } };
            DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", scriptArgs);

            // --- STEP 5: Assertions / Verifications ---
            Assert.That(flexMainPage.GetTicketStatus(), Is.EqualTo("Successfully Synced Ticket"), "The ticket was not successfully synced in the Flex app.");
        }
    }
}
