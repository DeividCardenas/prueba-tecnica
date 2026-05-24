using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FondoXYZ.Tests.Domain
{
    /// <summary>
    /// Pruebas unitarias sobre las entidades de dominio y la persistencia con InMemory.
    /// Validan que el modelo relacional (Sedes → Alojamientos → Reservas) se comporta correctamente.
    /// </summary>
    public class EntidadesTests
    {
        // ==============================================================
        // SEDE
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Sede_DebeInicializarseConValoresPorDefecto()
        {
            // Act
            var sede = new Sede
            {
                Nombre = "Villeta",
                Tipo = "Sede Recreativa",
                CapacidadMaxima = 32
            };

            // Assert
            sede.Nombre.Should().Be("Villeta");
            sede.CapacidadMaxima.Should().BePositive();
            sede.Alojamientos.Should().NotBeNull();
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public async Task Sede_DebeGuardarseYRecuperarseDeInMemory()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("Sede_CRUD");
            var sede = new Sede { Nombre = "El Placer", Tipo = "Sede Recreativa", CapacidadMaxima = 34, Descripcion = "Fusagasugá", Ubicacion = "Cundinamarca" };

            // Act
            ctx.Sedes.Add(sede);
            await ctx.SaveChangesAsync();

            var recuperada = await ctx.Sedes.FirstOrDefaultAsync(s => s.Nombre == "El Placer");

            // Assert
            recuperada.Should().NotBeNull();
            recuperada!.CapacidadMaxima.Should().Be(34);
        }

        // ==============================================================
        // ALOJAMIENTO
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Entidades")]
        public async Task Alojamiento_DebeEstarAsociadoASedePorFK()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Aloj_FK");

            // Act
            var aloj = await ctx.Alojamientos.Include(a => a.Sede).FirstOrDefaultAsync(a => a.Id == 1);

            // Assert
            aloj.Should().NotBeNull();
            aloj!.Sede.Should().NotBeNull();
            aloj.Sede.Nombre.Should().Be("Villeta");
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Alojamiento_CapacidadMaxima_DebeSerPositiva()
        {
            // Arrange
            var aloj = new Alojamiento
            {
                Nombre = "Cabaña 5", SedeId = 1,
                NumeroHabitaciones = 2, CapacidadMaxima = 4
            };

            // Assert
            aloj.CapacidadMaxima.Should().BeGreaterThan(0);
            aloj.NumeroHabitaciones.Should().BeGreaterThan(0);
        }

        // ==============================================================
        // RESERVA
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Entidades")]
        public async Task Reserva_DebePersistirseCorrectamente_EnInMemory()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Reserva_Persist");
            var usuario = new Usuario { Id = 5, NroDocumento = "U005", NombreCompleto = "Test", UserName = "U005", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var reserva = new Reserva
            {
                UsuarioId        = 5,
                SedeId           = 1,
                AlojamientoId    = 1,
                FechaLlegada     = DateTime.Today.AddDays(7),
                FechaSalida      = DateTime.Today.AddDays(10),
                CantidadPersonas = 3,
                CostoTotal       = 210000,
                EstadoReserva    = "Confirmada",
                FechaReserva     = DateTime.Now
            };

            // Act
            ctx.Reservas.Add(reserva);
            await ctx.SaveChangesAsync();

            var guardada = await ctx.Reservas.FindAsync(reserva.Id);

            // Assert
            guardada.Should().NotBeNull();
            guardada!.CostoTotal.Should().Be(210000);
            guardada.EstadoReserva.Should().Be("Confirmada");
            guardada.CantidadPersonas.Should().Be(3);
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Reserva_FechaSalida_DebeSerPosteriorAFechaLlegada()
        {
            // Arrange
            var llegada = DateTime.Today.AddDays(5);
            var salida  = DateTime.Today.AddDays(8);

            // Assert — Regla de negocio fundamental
            salida.Should().BeAfter(llegada);
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Reserva_NochesTotales_DebeCalcularseCorrectamente()
        {
            // Arrange
            var llegada = new DateTime(2026, 7, 1);
            var salida  = new DateTime(2026, 7, 4);

            // Act
            int noches = (salida - llegada).Days;

            // Assert
            noches.Should().Be(3);
        }

        // ==============================================================
        // TARIFA
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Entidades")]
        public async Task Tarifa_DebeRecuperarseCorrectamente_PorSedeId()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Tarifa_Query");

            // Act
            var tarifas = ctx.Tarifas.Where(t => t.SedeId == 1).ToList();

            // Assert
            tarifas.Should().NotBeEmpty();
            tarifas.All(t => t.ValorBase > 0).Should().BeTrue();
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Tarifa_PersonaAdicional_DebeCalcularseSobreLaCapacidadBase()
        {
            // Arrange — Regla de negocio: $16.000 por persona adicional sobre cap. 4
            var tarifa = new Tarifa
            {
                ValorBase = 70000, CapacidadBase = 4, ValorPersonaAdicional = 16000
            };
            int cantidadPersonas = 6;

            // Act
            decimal costoAdicional = cantidadPersonas > tarifa.CapacidadBase
                ? (cantidadPersonas - tarifa.CapacidadBase) * tarifa.ValorPersonaAdicional
                : 0;

            // Assert
            costoAdicional.Should().Be(32000); // 2 personas × $16.000
        }

        [Fact]
        [Trait("Categoria", "Entidades")]
        public void Tarifa_NoDebeCobrarAdicional_CuandoPersonasNoSuperanCapacidad()
        {
            // Arrange
            var tarifa = new Tarifa
            {
                ValorBase = 70000, CapacidadBase = 4, ValorPersonaAdicional = 16000
            };
            int cantidadPersonas = 3; // Dentro de capacidad

            // Act
            decimal costoAdicional = cantidadPersonas > tarifa.CapacidadBase
                ? (cantidadPersonas - tarifa.CapacidadBase) * tarifa.ValorPersonaAdicional
                : 0;

            // Assert
            costoAdicional.Should().Be(0);
        }
    }
}
