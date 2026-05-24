using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;
using FondoXYZ.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FondoXYZ.Tests.Controllers
{
    /// <summary>
    /// Pruebas unitarias para ReservasController.
    /// Cubren la obtención de reservas del usuario y el guardado de una nueva reserva.
    /// Usa una base de datos InMemory para aislar la capa de datos.
    /// </summary>
    public class ReservasControllerTests
    {
        // ==============================================================
        // MIS RESERVAS
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task MisReservas_DebeRetornarViewConListaVacia_CuandoUsuarioNoTieneReservas()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("MisReservas_Vacia");
            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("1");

            var controller = new ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("1");

            // Act
            var result = await controller.MisReservas();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var reservas = viewResult.Model.Should().BeAssignableTo<List<Reserva>>().Subject;
            reservas.Should().BeEmpty();
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task MisReservas_DebeRetornarSoloReservasDelUsuarioActual()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("MisReservas_Filtro");

            // Insertamos un usuario y dos reservas: una para el usuario 1, otra para el 2
            var usuario1 = new Usuario { Id = 10, NroDocumento = "U001", NombreCompleto = "Usuario Uno", UserName = "U001", FechaRegistro = DateTime.Now };
            var usuario2 = new Usuario { Id = 11, NroDocumento = "U002", NombreCompleto = "Usuario Dos", UserName = "U002", FechaRegistro = DateTime.Now };
            ctx.Users.AddRange(usuario1, usuario2);

            ctx.Reservas.AddRange(
                new Reserva { Id = 1, UsuarioId = 10, SedeId = 1, AlojamientoId = 1, FechaLlegada = DateTime.Today, FechaSalida = DateTime.Today.AddDays(3), CantidadPersonas = 2, CostoTotal = 210000, EstadoReserva = "Confirmada", FechaReserva = DateTime.Now },
                new Reserva { Id = 2, UsuarioId = 11, SedeId = 1, AlojamientoId = 1, FechaLlegada = DateTime.Today, FechaSalida = DateTime.Today.AddDays(2), CantidadPersonas = 1, CostoTotal = 140000, EstadoReserva = "Confirmada", FechaReserva = DateTime.Now }
            );
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("10");

            var controller = new ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("10");

            // Act
            var result = await controller.MisReservas();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var reservas = viewResult.Model.Should().BeAssignableTo<List<Reserva>>().Subject;
            reservas.Should().HaveCount(1);
            reservas[0].UsuarioId.Should().Be(10);
        }

        // ==============================================================
        // GUARDAR RESERVA FIRME
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebeRedirigirAMisReservas_CuandoDatosSonValidos()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("GuardarReserva_OK");

            var usuario = new Usuario { Id = 20, NroDocumento = "U020", NombreCompleto = "Test User", UserName = "U020", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("20");

            var controller = new ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("20");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            // Act
            var result = await controller.GuardarReservaFirme(
                sedeId:           1,
                alojamientoId:    1,
                fechaLlegada:     DateTime.Today.AddDays(10),
                fechaSalida:      DateTime.Today.AddDays(12),
                cantidadPersonas: 2,
                costoTotal:       140000m);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("MisReservas");
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebeGuardarReservaEnBD_CuandoDatosSonValidos()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("GuardarReserva_BD");

            var usuario = new Usuario { Id = 30, NroDocumento = "U030", NombreCompleto = "Test Save", UserName = "U030", FechaRegistro = DateTime.Now };
            ctx.Users.Add(usuario);
            await ctx.SaveChangesAsync();

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("30");

            var controller = new ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext("30");
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                controller.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            // Act
            await controller.GuardarReservaFirme(1, 1, DateTime.Today.AddDays(5), DateTime.Today.AddDays(7), 3, 140000m);

            // Assert: la reserva fue efectivamente persistida en InMemory
            ctx.Reservas.Should().HaveCount(1);
            ctx.Reservas.First().UsuarioId.Should().Be(30);
            ctx.Reservas.First().CostoTotal.Should().Be(140000m);
        }

        [Fact]
        [Trait("Categoria", "Reservas")]
        public async Task GuardarReservaFirme_DebeRedirigirALogin_CuandoUsuarioNoEstaAutenticado()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("GuardarReserva_NoAuth");

            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            userMgrMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns((string?)null);

            var controller = new ReservasController(ctx, userMgrMock.Object);
            controller.ControllerContext = BuildControllerContext(null);

            // Act
            var result = await controller.GuardarReservaFirme(1, 1, DateTime.Today, DateTime.Today.AddDays(2), 2, 140000m);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("Login");
        }

        // ==============================================================
        // DISPONIBILIDAD — GET
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Disponibilidad")]
        public async Task Disponibilidad_GET_DebeRetornarViewConListaDeSedes()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Disponibilidad_GET");
            var userMgrMock = IdentityMockHelper.CreateUserManagerMock();
            var controller = new ReservasController(ctx, userMgrMock.Object);

            // Act
            var result = await controller.Disponibilidad();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var selectList = (Microsoft.AspNetCore.Mvc.Rendering.SelectList?)controller.ViewBag.Sedes;
            selectList.Should().NotBeNull();

        }

        // ==============================================================
        // HELPERS PRIVADOS
        // ==============================================================

        private static ControllerContext BuildControllerContext(string? userId)
        {
            var claims = userId != null
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
