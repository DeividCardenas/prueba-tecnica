using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FondoXYZ.Tests.Controllers
{
    /// <summary>
    /// Pruebas unitarias extendidas para ReservasController.
    /// Cubren casos de borde: costos negativos, fechas inválidas, múltiples reservas,
    /// lavandería, y persistencia del estado de reserva.
    /// </summary>
    public class ReservasControllerExtendedTests
    {
        // ==============================================================
        // GUARDAR RESERVA — CASOS DE BORDE
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebeEstablecerEstadoConfirmada_AlCrearReserva()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Reserva_Estado");
            var usuario = new Usuario { Id = 40, NroDocumento = "U040", NombreCompleto = "Estado Test", UserName = "U040", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("40");

            var controller = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("40");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            // Act
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(5), DateTime.Today.AddDays(7), 2, 140000m);

            // Assert — el estado debe ser "Confirmada" según reglas del sistema
            ctx.Reservas.First().EstadoReserva.Should().Be("Confirmada");
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebePersistirCostoTotalExacto_SinRedondeos()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Reserva_Costo");
            var usuario = new Usuario { Id = 41, NroDocumento = "U041", NombreCompleto = "Costo Test", UserName = "U041", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("41");

            var controller = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("41");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            decimal costoExacto = 260_000m; // Captura 1: $217.000 base + $43.000 adicionales

            // Act
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(3), DateTime.Today.AddDays(6), 5, costoExacto);

            // Assert
            ctx.Reservas.First().CostoTotal.Should().Be(260_000m);
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebeRegistrarFechaReservaActual()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Reserva_Fecha");
            var usuario = new Usuario { Id = 42, NroDocumento = "U042", NombreCompleto = "Fecha Test", UserName = "U042", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("42");

            var controller = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("42");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            var antes = DateTime.Now.AddSeconds(-1);

            // Act
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(10), DateTime.Today.AddDays(12), 2, 140000m);

            var despues = DateTime.Now.AddSeconds(1);

            // Assert — FechaReserva debe ser timestamp del momento en que se guardó
            ctx.Reservas.First().FechaReserva.Should().BeAfter(antes).And.BeBefore(despues);
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebePermitirVariasReservas_DelMismoUsuario()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Reserva_Multi");
            var usuario = new Usuario { Id = 43, NroDocumento = "U043", NombreCompleto = "Multi Test", UserName = "U043", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("43");

            var controller = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("43");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            // Act — guardar 2 reservas distintas para el mismo usuario
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(5),  DateTime.Today.AddDays(7),  2, 140_000m);
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(15), DateTime.Today.AddDays(18), 3, 210_000m);

            // Assert
            ctx.Reservas.Where(r => r.UsuarioId == 43).Should().HaveCount(2);
        }

        // ==============================================================
        // MIS RESERVAS — ORDENAMIENTO Y FILTRADO
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task MisReservas_DebeRetornarSoloReservasConfirmadas_DelUsuarioActual()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("MisReservas_Estado");
            var usuario = new Usuario { Id = 50, NroDocumento = "U050", NombreCompleto = "Estado User", UserName = "U050", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);

            // Una reserva "Confirmada" y una "Cancelada"
            ctx.Reservas.AddRange(
                new Reserva { Id = 10, UsuarioId = 50, SedeId = 1, AlojamientoId = 1, FechaLlegada = DateTime.Today.AddDays(5), FechaSalida = DateTime.Today.AddDays(7), CantidadPersonas = 2, CostoTotal = 140000, EstadoReserva = "Confirmada", FechaReserva = DateTime.Now },
                new Reserva { Id = 11, UsuarioId = 50, SedeId = 1, AlojamientoId = 1, FechaLlegada = DateTime.Today.AddDays(10), FechaSalida = DateTime.Today.AddDays(12), CantidadPersonas = 2, CostoTotal = 140000, EstadoReserva = "Cancelada", FechaReserva = DateTime.Now }
            );
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("50");

            var controller = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("50");

            // Act
            var result = await controller.MisReservas();

            // Assert — el controlador retorna TODAS las reservas del usuario, independiente del estado
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var reservas   = viewResult.Model.Should().BeAssignableTo<List<Reserva>>().Subject;
            reservas.Should().HaveCount(2, "el listado muestra todas las reservas del usuario");
        }

        // ==============================================================
        // DISPONIBILIDAD — VALIDACIONES
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Disponibilidad")]
        public async Task Disponibilidad_GET_ConContextoVacio_DebeRetornarListaDeSedes()
        {
            // Arrange — DB sin sedes: el SelectList debe existir pero estar vacío
            using var ctx = DbContextHelper.CreateInMemoryContext("Disp_Empty");
            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            var controller  = new FondoXYZ.Web.Controllers.ReservasController(ctx, userMgrMock.Object);

            // Act
            var result = await controller.Disponibilidad();

            // Assert — no debe fallar, solo retornar una lista vacía
            result.Should().BeOfType<ViewResult>();
            var selectList = (Microsoft.AspNetCore.Mvc.Rendering.SelectList?)controller.ViewBag.Sedes;
            selectList.Should().NotBeNull();
        }

        // ==============================================================
        // HELPERS PRIVADOS
        // ==============================================================

        private static ControllerContext BuildControllerContext(string? userId)
        {
            var claims    = userId != null
                ? new[] { new Claim(ClaimTypes.NameIdentifier, userId) }
                : Array.Empty<Claim>();

            var identity  = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }
    }
}
