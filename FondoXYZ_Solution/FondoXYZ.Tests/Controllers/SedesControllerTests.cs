using FluentAssertions;
using FondoXYZ.Tests.Helpers;
using FondoXYZ.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace FondoXYZ.Tests.Controllers
{
    /// <summary>
    /// Pruebas unitarias para SedesController.
    /// Verifican el listado de sedes, el detalle y las validaciones de parámetros
    /// usando una base de datos InMemory.
    /// </summary>
    public class SedesControllerTests
    {
        // ==============================================================
        // INDEX — LISTADO DE SEDES
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Index_DebeRetornarViewConSedes_CuandoExistenSedes()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Sedes_Index");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var sedes = viewResult.Model.Should().BeAssignableTo<List<FondoXYZ.Domain.Entities.Sede>>().Subject;
            sedes.Should().HaveCount(1);
            sedes[0].Nombre.Should().Be("Villeta");
        }

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Index_DebeRetornarListaVacia_CuandoNoHaySedes()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateInMemoryContext("Sedes_Empty");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var sedes = viewResult.Model.Should().BeAssignableTo<List<FondoXYZ.Domain.Entities.Sede>>().Subject;
            sedes.Should().BeEmpty();
        }

        // ==============================================================
        // DETALLES — SEDE INDIVIDUAL
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Detalles_DebeRetornarViewConSede_CuandoIdEsValido()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Sedes_Detalles");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Detalles(1);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var sede = viewResult.Model.Should().BeOfType<FondoXYZ.Domain.Entities.Sede>().Subject;
            sede.Nombre.Should().Be("Villeta");
        }

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Detalles_DebeRetornarNotFound_CuandoSedeNoExiste()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Sedes_NotFound");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Detalles(9999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Detalles_DebeRetornarBadRequest_CuandoIdEsCeroONegativo()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Sedes_BadRequest");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Detalles(0);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        [Trait("Categoria", "Sedes")]
        public async Task Detalles_DebeIncluirAlojamientos_CuandoSedeExiste()
        {
            // Arrange
            using var ctx = DbContextHelper.CreateSeededContext("Sedes_ConAlojamientos");
            var controller = new SedesController(ctx);

            // Act
            var result = await controller.Detalles(1);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var sede = viewResult.Model.Should().BeOfType<FondoXYZ.Domain.Entities.Sede>().Subject;
            sede.Alojamientos.Should().NotBeNull();
            sede.Alojamientos.Should().HaveCount(1, "Se seeded 1 alojamiento para SedeId=1");
        }
    }
}
