// using NUnit.Framework;
// using OpenQA.Selenium;
// using OpenQA.Selenium.Appium;
// using OpenQA.Selenium.Appium.Android;
// using System;

// namespace RecoveryTrac.Tests
// {
//     [TestFixture]
//     public class TruckValidationTests
//     {
//         private AndroidDriver<AndroidElement> _driver;

//         [SetUp]
//         public void SetUp()
//         {
//             var appiumOptions = new AppiumOptions();
//             appiumOptions.AddAdditionalCapability("platformName", "Android");
//             appiumOptions.AddAdditionalCapability("automationName", "UiAutomator2");
//             appiumOptions.AddAdditionalCapability("appPackage", "com.tetratech.recoverytracflex");
//             appiumOptions.AddAdditionalCapability("appActivity", "com.tetratech.recoverytracflex.MainActivity");
//             appiumOptions.AddAdditionalCapability("noReset", true);

//             _driver = new AndroidDriver<AndroidElement>(new Uri("http://127.0.0.1:4723/wd/hub"), appiumOptions);
//             _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
//         }

//         [TearDown]
//         public void TearDown()
//         {
//             _driver?.Quit();
//         }

//         [Test]
//         public void AssertTruckInformationFields()
//         {
//             // 1. Verify success toast message is displayed with the correct text
//             var successToast = _driver.FindElement(By.XPath("//android.widget.Toast | //android.widget.TextView[contains(@text, 'scanned successfully')]"));
//             Assert.AreEqual("The QR code was scanned successfully, and 4 controls were populated.", successToast.Text, "Toast message is incorrect.");

//             // 2. Verify 'Driver Name (Scan)' field
//             var driverNameScanLabel = _driver.FindElement(By.XPath("//android.widget.TextView[@text='Driver Name (Scan) *']"));
//             var driverNameScanValue = _driver.FindElement(By.XPath("//android.widget.EditText[@text='PAUL JONES']"));
//             Assert.IsTrue(driverNameScanLabel.Displayed, "Driver Name (Scan) label is missing.");
//             Assert.AreEqual("PAUL JONES", driverNameScanValue.Text, "Driver Name (Scan) value is incorrect.");

//             // 3. Verify 'Truck Number' field
//             var truckNumberLabel = _driver.FindElement(By.XPath("//android.widget.TextView[@text='Truck Number *']"));
//             var truckNumberValue = _driver.FindElement(By.XPath("//android.widget.EditText[@text='6L82-TV-1000']"));
//             Assert.IsTrue(truckNumberLabel.Displayed, "Truck Number label is missing.");
//             Assert.AreEqual("6L82-TV-1000", truckNumberValue.Text, "Truck Number value is incorrect.");

//             // 4. Verify 'Truck Capacity' field
//             var truckCapacityLabel = _driver.FindElement(By.XPath("//android.widget.TextView[@text='Truck Capacity *']"));
//             var truckCapacityValue = _driver.FindElement(By.XPath("//android.widget.EditText[@text='55']"));
//             Assert.IsTrue(truckCapacityLabel.Displayed, "Truck Capacity label is missing.");
//             Assert.AreEqual("55", truckCapacityValue.Text, "Truck Capacity value is incorrect.");

//             // 5. Verify 'Truck Driver Name' field
//             var truckDriverNameLabel = _driver.FindElement(By.XPath("//android.widget.TextView[@text='Truck Driver Name *']"));
//             var truckDriverNameValue = _driver.FindElement(By.XPath("//android.widget.EditText[@text='PAUL JONES']"));
//             Assert.IsTrue(truckDriverNameLabel.Displayed, "Truck Driver Name label is missing.");
//             Assert.AreEqual("PAUL JONES", truckDriverNameValue.Text, "Truck Driver Name value is incorrect.");

//             // 6. Verify 'Truck License Plate' field
//             var truckLicensePlateLabel = _driver.FindElement(By.XPath("//android.widget.TextView[@text='Truck License Plate *']"));
//             var truckLicensePlateValue = _driver.FindElement(By.XPath("//android.widget.EditText[@text='AB1 2CD']"));
//             Assert.IsTrue(truckLicensePlateLabel.Displayed, "Truck License Plate label is missing.");
//             Assert.AreEqual("AB1 2CD", truckLicensePlateValue.Text, "Truck License Plate value is incorrect.");
//         }
//     }
// }