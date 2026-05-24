using FondoXYZ.Domain.Entities;
using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace FondoXYZ.Web.Controllers
{
    [Authorize] // Requisito estricto: Solo usuarios logueados pueden ver o hacer reservas
    public class ReservasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Usuario> _userManager;

        public ReservasController(ApplicationDbContext context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // 1. MIS RESERVAS (Corrige los errores CS0117 y CS8602)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> MisReservas()
        {
            // Extraer el Id del usuario logueado desde Identity
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Cuenta");

            int userId = int.Parse(userIdStr);

            // Traemos las reservas incluyendo los datos de la Sede y Alojamiento (Eager Loading)
            var reservas = await _context.Reservas
                .Include(r => r.Sede)
                .Include(r => r.Alojamiento)
                .Where(r => r.UsuarioId == userId)
                .OrderByDescending(r => r.FechaReserva)
                .ToListAsync();

            return View(reservas);
        }

        // ==========================================
        // 2. BUSCAR DISPONIBILIDAD (GET)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Disponibilidad()
        {
            var sedes = await _context.Sedes.ToListAsync();
            ViewBag.Sedes    = new SelectList(sedes, "Id", "Nombre");
            // Pasar datos completos como JSON para que el JS pueda mostrar descripciones
            ViewBag.SedesRaw = sedes.Select(s => new { s.Id, s.Nombre, s.Descripcion }).ToList();
            return View();
        }

        // ==========================================
        // 3. BUSCAR DISPONIBILIDAD (POST) - Ejecuta el SP
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Disponibilidad(int sedeId, DateTime fechaLlegada, DateTime fechaSalida, int cantidadPersonas)
        {
            ViewBag.Sedes = new SelectList(await _context.Sedes.ToListAsync(), "Id", "Nombre", sedeId);

            if (fechaLlegada < DateTime.Today || fechaSalida <= fechaLlegada || cantidadPersonas <= 0 || cantidadPersonas > 10)
            {
                ViewBag.Error = "Debe seleccionar un rango de fechas válido y la cantidad de personas debe ser entre 1 y 10.";
                return View();
            }

            var resultados = new List<DisponibilidadDto>();

            // SOLUCIÓN ROBUSTA: Usamos ADO.NET a través de la conexión de EF Core.
            // Esto garantiza que los tipos de datos del SP se mapeen exactamente sin fallar.
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_ConsultarDisponibilidadPorFechasYPersonas";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", fechaSalida));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        resultados.Add(new DisponibilidadDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Nombre = reader.GetString(reader.GetOrdinal("Nombre")),
                            NumeroHabitaciones = reader.GetInt32(reader.GetOrdinal("NumeroHabitaciones")),
                            CapacidadMaxima = reader.GetInt32(reader.GetOrdinal("CapacidadMaxima")),
                            NochesTotales = reader.GetInt32(reader.GetOrdinal("NochesTotales")),
                            EstadoDisponibilidad = reader.GetString(reader.GetOrdinal("EstadoDisponibilidad"))
                        });
                    }
                }
                await _context.Database.CloseConnectionAsync();
            }

            ViewBag.Resultados = resultados;
            ViewBag.FechaLlegada = fechaLlegada.ToString("yyyy-MM-dd");
            ViewBag.FechaSalida = fechaSalida.ToString("yyyy-MM-dd");
            ViewBag.CantidadPersonas = cantidadPersonas;
            ViewBag.SedeId = sedeId;

            return View();
        }

        // ==========================================
        // 4. CONFIRMAR RESERVA (Calcula el costo real usando el SP)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> ConfirmarReserva(int sedeId, int alojamientoId, DateTime fechaLlegada, DateTime fechaSalida, int cantidadPersonas)
        {
            var costoInfo = new CostoReservaDto();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_CalcularCostoReserva";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@AlojamientoId", alojamientoId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", fechaSalida));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas));

                // Aplicar lógica estricta de lavandería para Santa Marta (Id = 8)
                command.Parameters.Add(CrearParametro(command, "@IncluyeLavanderia", sedeId == 8 ? 1 : 0));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        costoInfo.NochesTotales = reader.GetInt32(reader.GetOrdinal("NochesTotales"));
                        costoInfo.SubtotalTarifaBase = reader.GetDecimal(reader.GetOrdinal("SubtotalTarifaBase"));
                        costoInfo.TotalExcedentePersonas = reader.GetDecimal(reader.GetOrdinal("TotalExcedentePersonas"));
                        costoInfo.CostoLavanderia = reader.GetDecimal(reader.GetOrdinal("CostoLavanderia"));
                        costoInfo.CostoTotalEstadia = reader.GetDecimal(reader.GetOrdinal("CostoTotalEstadia"));
                    }
                }
                await _context.Database.CloseConnectionAsync();
            }

            ViewBag.CostoDetalle = costoInfo;
            ViewBag.Sede = await _context.Sedes.FindAsync(sedeId);
            ViewBag.Alojamiento = await _context.Alojamientos.FindAsync(alojamientoId);
            ViewBag.FechaLlegada = fechaLlegada;
            ViewBag.FechaSalida = fechaSalida;
            ViewBag.CantidadPersonas = cantidadPersonas;

            return View(); // Deberás crear una vista 'ConfirmarReserva.cshtml' para mostrar este resumen
        }

        // ==========================================
        // 5. GUARDAR EN BD
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarReservaFirme(int sedeId, int alojamientoId, DateTime fechaLlegada, DateTime fechaSalida, int cantidadPersonas, decimal costoTotal)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Cuenta");

            var nuevaReserva = new Reserva
            {
                UsuarioId = int.Parse(userIdStr),
                SedeId = sedeId,
                AlojamientoId = alojamientoId,
                FechaLlegada = fechaLlegada,
                FechaSalida = fechaSalida,
                CantidadPersonas = cantidadPersonas,
                IncluyeLavanderia = sedeId == 8,
                CostoTotal = costoTotal,
                EstadoReserva = "Confirmada",
                FechaReserva = DateTime.Now
            };

            _context.Reservas.Add(nuevaReserva);
            await _context.SaveChangesAsync();

            TempData["MensajeExito"] = "¡Reserva generada con éxito!";
            return RedirectToAction("MisReservas");
        }

        // ==========================================
        // 6. EDITAR RESERVA (GET) — CRUD: Update
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> EditarReserva(int id)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Cuenta");
            int userId = int.Parse(userIdStr);

            var reserva = await _context.Reservas
                .Include(r => r.Sede)
                .Include(r => r.Alojamiento)
                .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == userId);

            if (reserva == null) return NotFound("Reserva no encontrada o no pertenece a su cuenta.");

            // Sólo se puede editar si la llegada aún no ocurrió
            if (reserva.FechaLlegada <= DateTime.Today)
            {
                TempData["MensajeError"] = "No se puede editar una reserva cuya fecha de llegada ya pasó o es hoy.";
                return RedirectToAction("MisReservas");
            }

            if (reserva.EstadoReserva == "Cancelada")
            {
                TempData["MensajeError"] = "No se puede editar una reserva cancelada.";
                return RedirectToAction("MisReservas");
            }

            return View(reserva);
        }

        // ==========================================
        // 7. EDITAR RESERVA (POST) — CRUD: Update
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarReserva(int id, DateTime nuevaFechaLlegada, DateTime nuevaFechaSalida, int nuevaCantidadPersonas)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Cuenta");
            int userId = int.Parse(userIdStr);

            var reserva = await _context.Reservas
                .Include(r => r.Sede)
                .Include(r => r.Alojamiento)
                .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == userId);

            if (reserva == null) return NotFound();

            // Validaciones de negocio
            if (nuevaFechaLlegada < DateTime.Today)
            {
                TempData["MensajeError"] = "La nueva fecha de llegada no puede ser anterior a hoy.";
                return RedirectToAction("EditarReserva", new { id });
            }
            if (nuevaFechaSalida <= nuevaFechaLlegada)
            {
                TempData["MensajeError"] = "La fecha de salida debe ser posterior a la fecha de llegada.";
                return RedirectToAction("EditarReserva", new { id });
            }
            if (nuevaCantidadPersonas <= 0)
            {
                TempData["MensajeError"] = "Debe indicar al menos 1 persona.";
                return RedirectToAction("EditarReserva", new { id });
            }

            // Recalcular costo con el SP usando los nuevos valores
            decimal nuevoCosto = 0;
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_CalcularCostoReserva";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(CrearParametro(command, "@SedeId", reserva.SedeId));
                command.Parameters.Add(CrearParametro(command, "@AlojamientoId", reserva.AlojamientoId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", nuevaFechaLlegada));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", nuevaFechaSalida));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", nuevaCantidadPersonas));
                command.Parameters.Add(CrearParametro(command, "@IncluyeLavanderia", reserva.IncluyeLavanderia ? 1 : 0));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        nuevoCosto = reader.GetDecimal(reader.GetOrdinal("CostoTotalEstadia"));
                }
                await _context.Database.CloseConnectionAsync();
            }

            // Actualizar la reserva
            reserva.FechaLlegada      = nuevaFechaLlegada;
            reserva.FechaSalida       = nuevaFechaSalida;
            reserva.CantidadPersonas  = nuevaCantidadPersonas;
            reserva.CostoTotal        = nuevoCosto;

            _context.Reservas.Update(reserva);
            await _context.SaveChangesAsync();

            TempData["MensajeExito"] = $"✅ Reserva #{id} actualizada exitosamente. Nuevo costo: {nuevoCosto:C0}";
            return RedirectToAction("MisReservas");
        }

        // ==========================================
        // 8. CANCELAR RESERVA (POST) — CRUD: Delete lógico
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarReserva(int id)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Cuenta");
            int userId = int.Parse(userIdStr);

            var reserva = await _context.Reservas
                .FirstOrDefaultAsync(r => r.Id == id && r.UsuarioId == userId);

            if (reserva == null) return NotFound("Reserva no encontrada.");

            if (reserva.EstadoReserva == "Cancelada")
            {
                TempData["MensajeError"] = "Esta reserva ya está cancelada.";
                return RedirectToAction("MisReservas");
            }

            if (reserva.FechaLlegada <= DateTime.Today)
            {
                TempData["MensajeError"] = "No se puede cancelar una reserva cuya fecha de llegada ya pasó o es hoy.";
                return RedirectToAction("MisReservas");
            }

            // Cancelación lógica (no eliminación física — conserva historial)
            reserva.EstadoReserva = "Cancelada";
            _context.Reservas.Update(reserva);
            await _context.SaveChangesAsync();

            TempData["MensajeExito"] = $"🚫 Reserva #{id} cancelada correctamente.";
            return RedirectToAction("MisReservas");
        }


        // ==========================================
        // 9. AJAX: Obtener fechas ya ocupadas de una sede (para el calendario Airbnb)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ObtenerFechasOcupadas(int sedeId)
        {
            if (sedeId <= 0) return Json(new List<object>());

            // Traer todas las reservas confirmadas de esta sede
            var reservasActivas = await _context.Reservas
                .Where(r => r.SedeId == sedeId && r.EstadoReserva != "Cancelada" && r.FechaSalida >= DateTime.Today)
                .Select(r => new { r.FechaLlegada, r.FechaSalida })
                .ToListAsync();

            // Convertir a rango de fechas que flatpickr entiende (formato {from, to})
            var bloqueados = reservasActivas.Select(r => new
            {
                from = r.FechaLlegada.ToString("yyyy-MM-dd"),
                to   = r.FechaSalida.ToString("yyyy-MM-dd")
            }).ToList();

            return Json(bloqueados);
        }

        // ==========================================
        // 10. AJAX: Buscar disponibilidad vía SP (retorna JSON para la tabla)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> BuscarDisponibilidadSp(int sedeId, DateTime fechaLlegada, DateTime fechaSalida, int cantidadPersonas)
        {
            if (sedeId <= 0 || fechaLlegada == default || fechaSalida <= fechaLlegada || cantidadPersonas <= 0 || cantidadPersonas > 10)
                return Json(new List<object>());

            // Cargar datos de EF ANTES de abrir el DataReader para evitar el conflicto de conexión
            var sede   = await _context.Sedes.FindAsync(sedeId);
            var tarifa = await _context.Tarifas
                .Where(t => t.SedeId == sedeId)
                .OrderBy(t => t.Id)
                .FirstOrDefaultAsync();
            var alojamientos = await _context.Alojamientos
                .Where(a => a.SedeId == sedeId)
                .ToListAsync();

            var resultados = new List<object>();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_ConsultarDisponibilidadPorFechasYPersonas";
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", fechaSalida));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int     alojId    = reader.GetInt32(reader.GetOrdinal("Id"));
                        int     noches    = reader.GetInt32(reader.GetOrdinal("NochesTotales"));
                        int     capMax    = reader.GetInt32(reader.GetOrdinal("CapacidadMaxima"));
                        int     numHab    = reader.GetInt32(reader.GetOrdinal("NumeroHabitaciones"));
                        string  nombre    = reader.GetString(reader.GetOrdinal("Nombre"));
                        string  estadoDisp = reader.GetString(reader.GetOrdinal("EstadoDisponibilidad"));

                        decimal costoEstimado = tarifa != null ? tarifa.ValorBase * noches : 0;
                        string desc = alojamientos.FirstOrDefault(a => a.Id == alojId)?.Descripcion ?? "Unidad vacacional totalmente dotada con lencería de cama, baño privado y conectividad de entretenimiento estándar.";

                        resultados.Add(new
                        {
                            alojamientoId   = alojId,
                            alojamiento     = nombre,
                            tipoAlojamiento = $"{numHab} hab.",
                            sedeNombre      = sede?.Nombre ?? "",
                            estadoDisp      = estadoDisp,
                            costoEstimado   = costoEstimado,
                            capacidadMaxima = capMax,
                            descripcion     = desc
                        });
                    }
                }
                await _context.Database.CloseConnectionAsync();
            }

            return Json(resultados);
        }

        // Helper para inyectar parámetros limpios en ADO.NET
        private DbParameter CrearParametro(DbCommand command, string nombre, object valor)
        {
            var parametro = command.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            return parametro;
        }
    }

    // ==========================================
    // CLASES DTO (Data Transfer Objects)
    // ==========================================
    // Estas clases sirven para recibir los datos de SQL Server sin contaminar tus entidades

    public class DisponibilidadDto
    {
        public int Id { get; set; }
        public required string Nombre { get; set; }
        public int NumeroHabitaciones { get; set; }
        public int CapacidadMaxima { get; set; }
        public int NochesTotales { get; set; }
        public string EstadoDisponibilidad { get; set; } = "";
    }

    public class CostoReservaDto
    {
        public int NochesTotales { get; set; }
        public decimal SubtotalTarifaBase { get; set; }
        public decimal TotalExcedentePersonas { get; set; }
        public decimal CostoLavanderia { get; set; }
        public decimal CostoTotalEstadia { get; set; }
    }
}