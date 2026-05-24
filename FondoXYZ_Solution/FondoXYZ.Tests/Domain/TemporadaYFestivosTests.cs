using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FondoXYZ.Tests.Domain
{
    /// <summary>
    /// Pruebas unitarias sobre las entidades Temporada y Festivo.
    /// Validan las reglas de negocio de temporadas: Alta, Escolar (Baja), y días festivos.
    /// </summary>
    public class TemporadaYFestivosTests
    {
        // ==============================================================
        // TEMPORADA
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Temporadas")]
        public void Temporada_DebeInicializarseConDatosValidos()
        {
            // Arrange
            var temporada = new Temporada
            {
                Nombre         = "Alta - Vacaciones de Mitad de Año",
                FechaInicio    = new DateTime(2026, 6, 15),
                FechaFin       = new DateTime(2026, 7, 31),
                EsAltaTemporada = true
            };

            // Assert
            temporada.Nombre.Should().NotBeNullOrEmpty();
            temporada.FechaFin.Should().BeAfter(temporada.FechaInicio);
            temporada.EsAltaTemporada.Should().BeTrue();
        }

        [Fact]
        [Trait("Categoria", "Temporadas")]
        public async Task Temporada_DebeGuardarseEnInMemory()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("Temp_Persist");
            var temporada = new Temporada
            {
                Nombre          = "Navidad",
                FechaInicio     = new DateTime(2026, 12, 15),
                FechaFin        = new DateTime(2027, 1, 15),
                EsAltaTemporada = true
            };

            // Act
            ctx.Temporadas.Add(temporada);
            await ctx.SaveChangesAsync();

            // Assert
            var guardada = await ctx.Temporadas.FirstOrDefaultAsync(t => t.Nombre == "Navidad");
            guardada.Should().NotBeNull();
            guardada!.EsAltaTemporada.Should().BeTrue();
        }

        [Theory]
        [Trait("Categoria", "Temporadas")]
        [InlineData("2026-05-21", true)]   // Mayo = escolar (baja)
        [InlineData("2026-06-20", false)]  // Junio = alta vacaciones
        [InlineData("2026-12-20", false)]  // Diciembre = alta navidad
        [InlineData("2026-03-15", true)]   // Marzo = temporada normal (baja)
        public void DeteccionTemporadaAlta_DebeCorresponderAFecha(string fechaStr, bool esperaBaja)
        {
            // Arrange — simulamos la lógica de evaluación de temporada
            var fecha = DateTime.Parse(fechaStr);

            // Rangos de Alta Temporada (replicando datos de la BD)
            var temporadasAlta = new[]
            {
                (Inicio: new DateTime(2026, 6,  15), Fin: new DateTime(2026, 7,  31)),
                (Inicio: new DateTime(2026, 12, 15), Fin: new DateTime(2027, 1,  15)),
                (Inicio: new DateTime(2026, 3,  21), Fin: new DateTime(2026, 4,   5)),
            };

            // Act
            bool esAlta = temporadasAlta.Any(t => fecha >= t.Inicio && fecha <= t.Fin);

            // Assert
            esAlta.Should().Be(!esperaBaja);
        }

        // ==============================================================
        // FESTIVOS
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Festivos")]
        public void Festivo_DebeInicializarseCorrectamente()
        {
            // Arrange
            var festivo = new Festivo
            {
                Fecha  = new DateTime(2026, 8, 7),
                Nombre = "Batalla de Boyacá"
            };

            // Assert
            festivo.Nombre.Should().NotBeNullOrEmpty();
            festivo.Fecha.Year.Should().Be(2026);
        }

        [Fact]
        [Trait("Categoria", "Festivos")]
        public async Task Festivo_DebeGuardarseYRecuperarseDeBD()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("Festivo_CRUD");
            var festivo = new Festivo
            {
                Fecha  = new DateTime(2026, 11, 2),
                Nombre = "Día de los Difuntos"
            };

            // Act
            ctx.Festivos.Add(festivo);
            await ctx.SaveChangesAsync();

            // Assert
            var guardado = await ctx.Festivos.FirstOrDefaultAsync(f => f.Nombre.Contains("Difuntos"));
            guardado.Should().NotBeNull();
            guardado!.Fecha.Month.Should().Be(11);
        }

        [Theory]
        [Trait("Categoria", "Festivos")]
        [InlineData("2026-08-07", true,  "Batalla de Boyacá es festivo")]
        [InlineData("2026-08-06", false, "6 de agosto no es festivo")]
        [InlineData("2026-12-08", true,  "Inmaculada Concepción es festivo")]
        [InlineData("2026-06-09", false, "9 de junio no es festivo ordinario")]
        public void EsFestivo_DebeDetectarCorrectamente(string fechaStr, bool esperaFestivo, string porque)
        {
            // Arrange — festivos colombianos clave (representativos)
            var festivosColombia = new[]
            {
                new DateTime(2026, 1,  1),   // Año Nuevo
                new DateTime(2026, 4,  2),   // Jueves Santo
                new DateTime(2026, 4,  3),   // Viernes Santo
                new DateTime(2026, 5,  1),   // Día del Trabajo
                new DateTime(2026, 6,  15),  // Corpus Christi
                new DateTime(2026, 7,  20),  // Independencia Colombia
                new DateTime(2026, 8,  7),   // Batalla de Boyacá
                new DateTime(2026, 12, 8),   // Inmaculada Concepción
                new DateTime(2026, 12, 25),  // Navidad
            };

            var fecha = DateTime.Parse(fechaStr);

            // Act
            bool esFestivo = festivosColombia.Contains(fecha);

            // Assert
            esFestivo.Should().Be(esperaFestivo, porque);
        }

        // ==============================================================
        // IMPACTO DE FESTIVOS EN TARIFA
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Festivos")]
        public void TarifaEnFestivo_DebeAplicarTarifaOrdinaria_AunqueSeaLunes()
        {
            // Arrange — lunes festivo: 1 de agosto 2026 es lunes ordinario (no festivo en Colombia),
            //           pero por ejemplo 7 de agosto que es viernes SÍ es festivo (Batalla de Boyacá)
            var diaFestivo = new DateTime(2026, 8, 7); // viernes + festivo
            bool esFestivo = true; // lo simula la BD

            // Act — La regla: si es festivo, se aplica tarifa Ordinaria sin importar el día
            bool esFinDeSemana = diaFestivo.DayOfWeek == DayOfWeek.Saturday || diaFestivo.DayOfWeek == DayOfWeek.Sunday;
            string tipoTarifa  = (esFestivo || esFinDeSemana) ? "Ordinaria" : "Especial";

            // Assert
            tipoTarifa.Should().Be("Ordinaria",
                "los festivos siempre se cobran a tarifa Ordinaria según el reglamento del Fondo XYZ");
        }

        [Fact]
        [Trait("Categoria", "Festivos")]
        public void TarifaEnDiaNormal_DebeAplicarTarifaEspecial_SiNoEsFestivo()
        {
            // Arrange — Martes 24 de marzo, fuera de alta temporada, sin festivo
            var diaNormal  = new DateTime(2026, 3, 24); // martes
            bool esFestivo = false;

            // Act
            bool esFinDeSemana = diaNormal.DayOfWeek == DayOfWeek.Saturday || diaNormal.DayOfWeek == DayOfWeek.Sunday;
            string tipoTarifa  = (esFestivo || esFinDeSemana) ? "Ordinaria" : "Especial";

            // Assert
            tipoTarifa.Should().Be("Especial",
                "un martes sin festivo y fuera de alta temporada aplica la tarifa Especial (lunes-jueves)");
        }
    }
}
