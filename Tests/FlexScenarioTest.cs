using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Pages.FlexApp;

namespace MobileAutomation.Tests.Tests
{
    [TestFixture]
    public class FlexScenarioTest : BaseTest
    {
        [SetUp]
        public void InicializarEscenarioDual()
        {
            // Pasas los dos entornos configurados en tu appsettings.json 
            // (Ej: Uno apuntando al emulador 5554 y el otro al 5556)
            DriverFactory.InitializeDualApps("Android_Flex", "Android_Driver");
        }
        
        [Test]
        public void TestAsignacionDeServicioE2E()
        {
            // 1. Inicializar las páginas usando los drivers heredados de BaseTest
            var flexMainPage = new FlexAppPage(DriverFactory.DriverFlex);
            var driverMapPage = new DriverAppPage(DriverFactory.DriverDriver);

            // 2. Operación en la App de Flex
            flexMainPage.CrearNuevoServicio("Calle Falsa 123");

            // 3. Verificación y Operación en la App del Conductor
            Assert.That(driverMapPage.ValidarAlertaDeViajeVisible(), Is.True, "La alerta de viaje no apareció en la app del Driver.");
            driverMapPage.AceptarViaje();
        }
    }
}
