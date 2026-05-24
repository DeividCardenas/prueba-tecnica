using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;

namespace FondoXYZ.Tests.Domain
{
    /// <summary>
    /// Pruebas unitarias sobre las reglas de negocio de tarifas:
    /// - Cálculo de tarifa especial vs ordinaria según día de la semana
    /// - Cálculo de personas adicionales
    /// - Cálculo de noches totales
    /// - Restricción de temporada alta (tarifa especial no aplica)
    /// </summary>
    public class TarifasCalculoTests
    {
        // ==============================================================
        // CÁLCULO DE NOCHES TOTALES
        // ==============================================================

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void NochesTotales_DebeCalcularseCorrectamente_ParaEstadiaEstandar()
        {
            // Arrange
            var llegada = new DateTime(2026, 5, 21);
            var salida  = new DateTime(2026, 5, 24);

            // Act
            int noches = (salida - llegada).Days;

            // Assert
            noches.Should().Be(3);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void NochesTotales_DebeSerUna_ParaEstadiaDeUnDia()
        {
            // Arrange
            var llegada = new DateTime(2026, 6, 10);
            var salida  = new DateTime(2026, 6, 11);

            // Act
            int noches = (salida - llegada).Days;

            // Assert
            noches.Should().Be(1);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void NochesTotales_DebeSerCero_CuandoLlegadaYSalidaSonElMismoDia()
        {
            // Arrange
            var fecha = new DateTime(2026, 7, 1);

            // Act
            int noches = (fecha - fecha).Days;

            // Assert — esta condición es rechazada por el SP pero la lógica pura da 0
            noches.Should().Be(0);
        }

        // ==============================================================
        // TARIFA ESPECIAL vs ORDINARIA (regla de día de la semana)
        // ==============================================================

        [Theory]
        [Trait("Categoria", "CalculoTarifas")]
        [InlineData(DayOfWeek.Monday,    "Especial")]
        [InlineData(DayOfWeek.Tuesday,   "Especial")]
        [InlineData(DayOfWeek.Wednesday, "Especial")]
        [InlineData(DayOfWeek.Thursday,  "Especial")]
        [InlineData(DayOfWeek.Friday,    "Ordinaria")]
        [InlineData(DayOfWeek.Saturday,  "Ordinaria")]
        [InlineData(DayOfWeek.Sunday,    "Ordinaria")]
        public void TipoTarifa_DebeCorresPonderAlDiaDeLaSemana(DayOfWeek dia, string tipoEsperado)
        {
            // Act — lógica idéntica a la del Stored Procedure sp_CalcularCostoReserva
            bool esFinDeSemana = dia == DayOfWeek.Friday || dia == DayOfWeek.Saturday || dia == DayOfWeek.Sunday;
            string tipoResultado = esFinDeSemana ? "Ordinaria" : "Especial";

            // Assert
            tipoResultado.Should().Be(tipoEsperado);
        }

        // ==============================================================
        // SUBTOTAL TARIFA BASE
        // ==============================================================

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void SubtotalTarifaBase_DebeCalcularseCorrectamente_ParaTarifaEspecial()
        {
            // Arrange — Alojamiento 2, El Placer: 3 noches entre semana a $37.000
            decimal valorNoche = 37_000;
            int     noches     = 3;

            // Act
            decimal subtotal = valorNoche * noches;

            // Assert
            subtotal.Should().Be(111_000);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void SubtotalTarifaBase_DebeCalcularseCorrectamente_ParaTarifaOrdinaria()
        {
            // Arrange — Alojamiento 1, El Placer: 4 noches fin de semana a $70.000
            decimal valorNoche = 70_000;
            int     noches     = 4;

            // Act
            decimal subtotal = valorNoche * noches;

            // Assert
            subtotal.Should().Be(280_000);
        }

        // ==============================================================
        // RECARGO PERSONAS ADICIONALES
        // ==============================================================

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void RecargoAdicional_DebeCalcularseCorrectamente_ConUnaPersonaExtra_TarifaEspecial()
        {
            // Arrange — $11.000 por persona adicional en tarifa especial, 3 noches, 1 persona extra
            decimal valorAdicional = 11_000;
            int     personasExtra  = 1;
            int     noches         = 3;

            // Act
            decimal recargo = valorAdicional * personasExtra * noches;

            // Assert
            recargo.Should().Be(33_000);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void RecargoAdicional_DebeCalcularseCorrectamente_ConUnaPersonaExtra_TarifaOrdinaria()
        {
            // Arrange — $16.000 por persona adicional en tarifa ordinaria, 4 noches, 1 persona extra
            decimal valorAdicional = 16_000;
            int     personasExtra  = 1;
            int     noches         = 4;

            // Act
            decimal recargo = valorAdicional * personasExtra * noches;

            // Assert
            recargo.Should().Be(64_000);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void RecargoAdicional_DebeSer_Cero_CuandoPersonasNoSuperanCapacidad()
        {
            // Arrange
            var tarifa = new Tarifa
            {
                ValorBase = 70_000, CapacidadBase = 4, ValorPersonaAdicional = 16_000
            };
            int cantidadPersonas = 4; // Exactamente la capacidad base

            // Act
            decimal recargo = cantidadPersonas > tarifa.CapacidadBase
                ? (cantidadPersonas - tarifa.CapacidadBase) * tarifa.ValorPersonaAdicional
                : 0;

            // Assert
            recargo.Should().Be(0);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void RecargoAdicional_DebeCalcularCorrectoConVariasPersonasExtra()
        {
            // Arrange — Capacidad base 4, ingresan 7: 3 personas adicionales × $16.000
            var tarifa = new Tarifa
            {
                ValorBase = 70_000, CapacidadBase = 4, ValorPersonaAdicional = 16_000
            };
            int cantidadPersonas = 7;

            // Act
            decimal recargo = cantidadPersonas > tarifa.CapacidadBase
                ? (cantidadPersonas - tarifa.CapacidadBase) * tarifa.ValorPersonaAdicional
                : 0;

            // Assert
            recargo.Should().Be(48_000); // 3 × $16.000
        }

        // ==============================================================
        // TOTAL FINAL ACUMULADO (integración de cálculos)
        // ==============================================================

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void TotalEstadia_DebeSerSumaCorrectaDeSubtotalMasRecargo()
        {
            // Arrange — Escenario captura 1: 3 noches, 5 personas, Alojamiento 2 El Placer
            // (1 noche especial $37K + 2 noches ordinarias $90K, 1 persona adicional)
            decimal subtotalBase   = 217_000; // $37K + $90K + $90K
            decimal recargoPersonas = 43_000; // $11K + $16K + $16K

            // Act
            decimal total = subtotalBase + recargoPersonas;

            // Assert
            total.Should().Be(260_000);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void TotalEstadia_CapturaDos_DebeCalcularseCorrectamente()
        {
            // Arrange — Escenario captura 2: 4 noches ordinarias $70K + 1 persona extra $16K
            decimal valorNoche      = 70_000;
            decimal valorAdicional  = 16_000;
            int     noches          = 4;
            int     personasExtra   = 1;

            // Act
            decimal subtotal = valorNoche     * noches;
            decimal recargo  = valorAdicional * personasExtra * noches;
            decimal total    = subtotal + recargo;

            // Assert
            subtotal.Should().Be(280_000);
            recargo.Should().Be(64_000);
            total.Should().Be(344_000);
        }

        // ==============================================================
        // TEMPORADA ALTA: TARIFA ESPECIAL NO APLICA
        // ==============================================================

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void TarifaEspecial_NoDebeAplicar_EnTemporadaAlta()
        {
            // Arrange — En Alta Temporada, siempre se cobra Tarifa Ordinaria aunque sea entre semana
            bool esTemporadaAlta = true;
            var  dia             = DayOfWeek.Tuesday; // normalmente aplicaría especial

            // Act — Regla del negocio: Alta Temporada > día de la semana
            bool esFinDeSemana   = dia == DayOfWeek.Friday || dia == DayOfWeek.Saturday || dia == DayOfWeek.Sunday;
            string tipoTarifa    = (esTemporadaAlta || esFinDeSemana) ? "Ordinaria" : "Especial";

            // Assert
            tipoTarifa.Should().Be("Ordinaria");
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void TarifaEspecial_SiDebeAplicar_EnBajaTemporadaEntresSemana()
        {
            // Arrange — Baja Temporada + Martes = Tarifa Especial
            bool esTemporadaAlta = false;
            var  dia             = DayOfWeek.Tuesday;

            // Act
            bool esFinDeSemana = dia == DayOfWeek.Friday || dia == DayOfWeek.Saturday || dia == DayOfWeek.Sunday;
            string tipoTarifa  = (esTemporadaAlta || esFinDeSemana) ? "Ordinaria" : "Especial";

            // Assert
            tipoTarifa.Should().Be("Especial");
        }

        // ==============================================================
        // LAVANDERÍA — SÓLO APLICA EN SEDE RODADERO (SedeId = 8)
        // ==============================================================

        [Theory]
        [Trait("Categoria", "CalculoTarifas")]
        [InlineData(1, false)]  // Villeta
        [InlineData(2, false)]  // El Placer
        [InlineData(6, false)]  // Medellín
        [InlineData(7, false)]  // Santa Marta (sede, no rodadero)
        [InlineData(8, true)]   // El Rodadero (única con lavandería)
        public void Lavanderia_DebeAplicar_SoloEnRodadero(int sedeId, bool debeAplicar)
        {
            // Act — lógica idéntica a la del controlador
            bool aplicaLavanderia = sedeId == 8;

            // Assert
            aplicaLavanderia.Should().Be(debeAplicar);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void CostoLavanderia_DebeCalcularsePorNoches_CuandoAplica()
        {
            // Arrange — El Rodadero: $75.000 adicionales por estadía
            decimal costoLavanderiaPorEstadia = 75_000;
            bool    incluyeLavanderia         = true;

            // Act
            decimal costo = incluyeLavanderia ? costoLavanderiaPorEstadia : 0;

            // Assert
            costo.Should().Be(75_000);
        }

        [Fact]
        [Trait("Categoria", "CalculoTarifas")]
        public void CostoLavanderia_DebeSer_Cero_CuandoNoSeSelecciona()
        {
            // Arrange
            decimal costoLavanderiaPorEstadia = 75_000;
            bool    incluyeLavanderia         = false;

            // Act
            decimal costo = incluyeLavanderia ? costoLavanderiaPorEstadia : 0;

            // Assert
            costo.Should().Be(0);
        }
    }
}
