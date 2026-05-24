using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace FondoXYZ.Web.Controllers
{
    // CUMPLE REQUERIMIENTO: Solo usuarios autenticados podrán usar el aplicativo
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        // Inyectamos tanto el contexto de BD como el Logger para monitoreo
        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // 1. Extraer información segura del usuario desde las Claims (Cookies)
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.FindFirstValue(ClaimTypes.Name);

                int reservasProximas = 0;
                int totalSedes = 0;

                // 2. Consultas rápidas asíncronas para armar el Dashboard
                if (int.TryParse(userIdStr, out int userId))
                {
                    // Contamos cuántas reservas tiene el usuario a partir de la fecha actual
                    reservasProximas = await _context.Reservas
                        .AsNoTracking()
                        .CountAsync(r => r.UsuarioId == userId
                                      && r.FechaLlegada >= DateTime.Today
                                      && r.EstadoReserva != "Cancelada");
                }

                // Contamos el total de destinos disponibles
                totalSedes = await _context.Sedes.AsNoTracking().CountAsync();

                // 3. Pasamos los indicadores a la vista usando ViewBag
                ViewBag.NombreUsuario = userName ?? "Asociado";
                ViewBag.ReservasProximas = reservasProximas;
                ViewBag.TotalSedes = totalSedes;

                return View();
            }
            catch (Exception ex)
            {
                // Registramos el error internamente sin romper la pantalla del usuario
                _logger.LogError(ex, "Error al cargar el Dashboard del Home.");

                // Valores por defecto seguros en caso de fallo de BD
                ViewBag.NombreUsuario = "Asociado";
                ViewBag.ReservasProximas = 0;
                ViewBag.TotalSedes = 0;

                return View();
            }
        }

        // ===================================================================================
        // MANEJO GLOBAL DE ERRORES
        // ===================================================================================
        [AllowAnonymous] // Permite ver la pantalla de error incluso si la sesión caducó
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Este método captura los errores 500 para mostrar una vista amigable
            return View();
        }
    }
}