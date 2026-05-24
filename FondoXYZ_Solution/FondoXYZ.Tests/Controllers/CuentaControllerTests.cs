using FluentAssertions;
using FondoXYZ.Domain.Entities;
using FondoXYZ.Tests.Helpers;
using FondoXYZ.Web.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FondoXYZ.Tests.Controllers
{
    /// <summary>
    /// Pruebas unitarias para CuentaController.
    /// Cubren los flujos de Login, Registro y Recuperación de Contraseña.
    /// Se usan mocks de Identity para aislar la lógica del controlador
    /// sin necesidad de base de datos real.
    /// </summary>
    public class CuentaControllerTests
    {
        private readonly Mock<UserManager<Usuario>> _userManagerMock;
        private readonly Mock<SignInManager<Usuario>> _signInManagerMock;
        private readonly Mock<Microsoft.Extensions.Configuration.IConfiguration> _configMock;
        private readonly CuentaController _sut;

        public CuentaControllerTests()
        {
            _userManagerMock  = IdentityMockHelper.CreateUserManagerMock();
            _signInManagerMock = IdentityMockHelper.CreateSignInManagerMock(_userManagerMock);
            _configMock       = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

            _sut = new CuentaController(
                _userManagerMock.Object,
                _signInManagerMock.Object,
                _configMock.Object);
        }

        // ==============================================================
        // LOGIN — GET
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Login")]
        public void Login_GET_DebeRetornarView_CuandoUsuarioNoEstaAutenticado()
        {
            // Arrange: usuario sin contexto HTTP (no autenticado)
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

            // Act
            var result = _sut.Login();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        // ==============================================================
        // LOGIN — POST
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Login")]
        public async Task Login_POST_DebeRedirigirAIndex_CuandoCredencialesSonCorrectas()
        {
            // Arrange
            _signInManagerMock
                .Setup(s => s.PasswordSignInAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            // Act
            var result = await _sut.Login("12345678", "Admin1234!");

            // Assert
            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("Index");
        }

        [Fact]
        [Trait("Categoria", "Login")]
        public async Task Login_POST_DebeRetornarViewConError_CuandoCredencialesSonIncorrectas()
        {
            // Arrange
            _signInManagerMock
                .Setup(s => s.PasswordSignInAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            // Act
            var result = await _sut.Login("12345678", "ClaveWrong");

            // Assert
            result.Should().BeOfType<ViewResult>();
            string? error = _sut.ViewBag.Error;
            error.Should().NotBeNullOrEmpty();
        }

        [Fact]
        [Trait("Categoria", "Login")]
        public async Task Login_POST_DebeRetornarViewConError_CuandoCamposEstanVacios()
        {
            // Act
            var result = await _sut.Login("", "");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            string? error = _sut.ViewBag.Error;
            error.Should().Contain("Debe ingresar");
        }

        [Fact]
        [Trait("Categoria", "Login")]
        public async Task Login_POST_DebeRetornarViewConError_CuandoDocumentoEsNulo()
        {
            // Act
            var result = await _sut.Login(null!, "Admin1234!");

            // Assert
            result.Should().BeOfType<ViewResult>();
            string? error2 = _sut.ViewBag.Error;
            error2.Should().NotBeNullOrEmpty();
        }

        // ==============================================================
        // REGISTRO — POST
        // ==============================================================

        [Fact]
        [Trait("Categoria", "Registro")]
        public async Task Registro_POST_DebeRedirigirALogin_CuandoRegistroEsExitoso()
        {
            // Arrange
            _userManagerMock
                .Setup(u => u.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((Usuario?)null);
            _userManagerMock
                .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((Usuario?)null);
            _userManagerMock
                .Setup(u => u.CreateAsync(It.IsAny<Usuario>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            var nuevoUsuario = new Usuario
            {
                NroDocumento   = "99887766",
                NombreCompleto = "Juan Prueba",
                FechaNacimiento = new DateTime(1995, 6, 15)
            };

            // Act
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
            _sut.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                _sut.ControllerContext.HttpContext,
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

            var result = await _sut.Registro(nuevoUsuario, "Test1234!", "juan@test.com", "3001112233");


            // Assert
            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("Login");
        }

        [Fact]
        [Trait("Categoria", "Registro")]
        public async Task Registro_POST_DebeRetornarViewConError_CuandoDocumentoYaExiste()
        {
            // Arrange: FindByNameAsync devuelve un usuario ya existente
            _userManagerMock
                .Setup(u => u.FindByNameAsync("12345678"))
                .ReturnsAsync(new Usuario { NroDocumento = "12345678", NombreCompleto = "Existente" });

            var modelo = new Usuario { NroDocumento = "12345678", NombreCompleto = "Otro" };

            // Act
            var result = await _sut.Registro(modelo, "Clave123!", "otro@mail.com", "");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            string? error = _sut.ViewBag.Error;
            error.Should().Contain("documento");
        }

        [Fact]
        [Trait("Categoria", "Registro")]
        public async Task Registro_POST_DebeRetornarViewConError_CuandoEmailYaExiste()
        {
            // Arrange
            _userManagerMock
                .Setup(u => u.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((Usuario?)null);
            _userManagerMock
                .Setup(u => u.FindByEmailAsync("repetido@fondoxyz.com"))
                .ReturnsAsync(new Usuario { Email = "repetido@fondoxyz.com", NombreCompleto = "Otro" });

            var modelo = new Usuario { NroDocumento = "55554444", NombreCompleto = "Nuevo" };

            // Act
            var result = await _sut.Registro(modelo, "Clave123!", "repetido@fondoxyz.com", "");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            string? error = _sut.ViewBag.Error;
            error.Should().Contain("correo");
        }

        // ==============================================================
        // RECUPERAR CLAVE
        // ==============================================================

        [Fact]
        [Trait("Categoria", "RecuperarClave")]
        public async Task RecuperarClave_POST_DebeRetornarViewConError_CuandoEmailNoExiste()
        {
            // Arrange
            _userManagerMock
                .Setup(u => u.FindByEmailAsync("noexiste@mail.com"))
                .ReturnsAsync((Usuario?)null);

            // Act
            var result = await _sut.RecuperarClave("noexiste@mail.com");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            string? error = _sut.ViewBag.Error;
            error.Should().Contain("No se encontró");
        }
    }
}
