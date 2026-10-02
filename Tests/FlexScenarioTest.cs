using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Pages.FlexApp;

namespace MobileAutomation.Tests.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Self)]
    public class FlexScenarioTest : BaseTest
    {
        [SetUp]
        public void InicializarEscenarioDual()
        {
            // Send two environments configured in your appsettings.json 
            // (Example: One pointing to emulator 5554 and the other to 5556)
            DriverFactory.InitializeDualApps();
        }

        [TearDown]
        public void FinalizarEscenarioDual()
        {
            Console.WriteLine("[Cleanup] Terminating dual drivers and releasing the leased emulator pair back to the pool...");
            // Crucial: Frees up the leased emulators so the next parallel class can immediately use them
            DriverFactory.Quit();
        }

        
        [Test]
        public void TestAsignacionDeServicioE2E()
        {
            // 1. Initialize the pages using the drivers inherited from BaseTest
            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

            // 2. Operation in the Flex App
            flexMainPage.CreateNewService("Calle Falsa 123");

            // 3. Verification and Operation in the Driver App
            Assert.That(driverMapPage.ValidateAlertVisibleTrip(), Is.True, "The Alarm Trip alert did not appear in the Driver app.");
            driverMapPage.AcceptTrip();
        }
    }
}
