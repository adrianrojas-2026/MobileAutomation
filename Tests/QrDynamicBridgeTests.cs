using OpenQA.Selenium;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Pages.FlexApp;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class QrDynamicBridgeTests : BaseTest
    {
        [SetUp]
        public void InitializeDualScenario()
        {
            // Send two environments configured in your appsettings.json 
            // (Example: One pointing to emulator 5554 and the other to 5556)
            DriverFactory.InitializeDualApps();
            Console.WriteLine("Clearing native OS permission blocks on Driver App...");
            PermissionHelper.HandleSystemPermissions(DriverFactory.DriverDriver);

            Console.WriteLine("Clearing native OS permission blocks on Flex App...");
            PermissionHelper.HandleSystemPermissions(DriverFactory.DriverFlex);
        }

        [Test]
        public void Test_QR_Dynamic_Synchronization()
        {

            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

            // --- STEP 1: Generate QR en RecoveryTrac Driver (Emulador 1) ---
            driverMapPage.GenerateTicket();
            // Assert that the QR rendered properly before attempting to bridge the image
            Assert.That(driverMapPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True, 
                "The ticket QR code failed to render on the Driver App UI within the timeout.");

            // --- STEP 2: Dynamic Capture in memory (Emulador 1) ---
            // Screenshot screenshotEmulador1 =  DriverFactory.DriverDriver.GetScreenshot();
            // string qrImagenBase64 = screenshotEmulador1.AsBase64EncodedString;
            string qrImagenBase64 = driverMapPage.GetOnlyQrCodeAsBase64();

            // --- STEP 3: Prepare scanner in RecoveryTrac Flex (Emulador 2) ---
            flexMainPage.StartScan();

            // --- STEP 4: Real-time Injection in the Virtual Camera ---
            var scriptArgs = new Dictionary<string, object> { { "payload", qrImagenBase64 } };
            DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", scriptArgs);

            // --- STEP 5: Assertions / Verifications ---
            Assert.That(flexMainPage.GetTicketStatus(), Is.EqualTo("Ticket Synchronized Successfully"), "The ticket was not successfully synced in the Flex app.");
        }

        [Test]
        public void Test_QrSynchronization_Success_WhenDevicesAreClose()
        {
            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

            // --- STEP 1: Simulate Proximity (Devices are at the exact same drop-off zone) ---
            // Setting coordinates close to each other (e.g., San José, Costa Rica)
            // DriverFactory.DriverDriver.Location.Latitude = 9.9281;
            // DriverFactory.DriverDriver.Location.Longitude = -84.0907;

            var locationDriver = new OpenQA.Selenium.Appium.Location { Latitude = 9.9281, Longitude = -84.0907, Altitude = 0 };
            DriverFactory.DriverDriver.Location = locationDriver;

            // DriverFactory.DriverFlex.Location.Latitude = 9.9282; // Just a few meters away
            // DriverFactory.DriverFlex.Location.Longitude = -84.0908;

            var locationFlex = new OpenQA.Selenium.Appium.Location { Latitude = 9.9282, Longitude = -84.0908, Altitude = 0 };
            DriverFactory.DriverFlex.Location = locationFlex;

            // --- STEP 2: Generate QR Code on RecoveryTrac Driver (Emulator 1) ---
            driverMapPage.GenerateTicket(); // Triggers QR rendering on screen
            // Assert that the QR rendered properly before attempting to bridge the image
            Assert.That(driverMapPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True, 
                "The ticket QR code failed to render on the Driver App UI within the timeout.");

            // --- STEP 3: Dynamic Memory Capture (Emulator 1) ---
            // Screenshot driverScreen = DriverFactory.DriverDriver.GetScreenshot();
            // string qrImageBase64 = driverScreen.AsBase64EncodedString;
            string qrImageBase64 = driverMapPage.GetOnlyQrCodeAsBase64();

            // --- STEP 4: Prepare Scanner on RecoveryTrac Flex (Emulator 2) ---
            flexMainPage.StartScan();
            
            // --- STEP 5: Real-Time Camera Injection ---
            var scriptArgs = new Dictionary<string, object> { { "payload", qrImageBase64 } };
            DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", scriptArgs);
            Thread.Sleep(2000);

            // --- STEP 6: Assertions ---
            Assert.That(flexMainPage.GetTicketStatus(), Is.EqualTo("Ticket Synchronized Successfully"), "The ticket was not successfully synced in the Flex app.");
        }

        [Test]
        public void Test_QrSynchronization_Fails_WhenDevicesAreTooFar()
        {
            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

             // --- STEP 1: Simulate High Distance (Driver is far away from the Flex Supervisor) ---
            // Safely assign Driver coordinates using the explicit Appium Location model
            var locationDriver = new OpenQA.Selenium.Appium.Location { Latitude = 9.9281, Longitude = -84.0907, Altitude = 0 };
            DriverFactory.DriverDriver.Location = locationDriver;

            // Flex App Supervisor is located 100+ kilometers away at Point B
            var locationFlex = new OpenQA.Selenium.Appium.Location { Latitude = 10.0024, Longitude = -83.0271, Altitude = 0 };
            DriverFactory.DriverFlex.Location = locationFlex;

            // --- STEP 2: Generate QR Code on RecoveryTrac Driver (Emulator 1) ---
            driverMapPage.GenerateTicket();
            // Assert that the QR rendered properly before attempting to bridge the image
            Assert.That(driverMapPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True, 
                "The ticket QR code failed to render on the Driver App UI within the timeout.");

             // --- STEP 3: Optimized Dynamic Element Capture (Only the clean QR bounding box) ---
            string qrImageBase64 = driverMapPage.GetOnlyQrCodeAsBase64();

            // --- STEP 4: Prepare Scanner on RecoveryTrac Flex (Emulator 2) ---
            flexMainPage.StartScan();
            
            // --- STEP 5: Real-Time Camera Injection ---
            var scriptArgs = new Dictionary<string, object> { { "payload", qrImageBase64 } };
            DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", scriptArgs);

            // --- STEP 6: Assert Failure due to Distance Fraud Check ---
            // Clean and highly readable business validations:
            Assert.That(flexMainPage.IsDistanceErrorDisplayed(), Is.True, "Distance error warning was not displayed.");
            Assert.That(flexMainPage.GetDistanceErrorMessage(), Is.EqualTo("Cannot process ticket: Driver is out of the permitted range."));
        }
    }
}
