using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace FondoXYZ.Web.Controllers
{
    [Authorize]
    public class SedesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SedesController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ===================================================================================
        // 1. LISTADO GENERAL DE SEDES
        // ===================================================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var sedes = await _context.Sedes
                .AsNoTracking()
                .OrderBy(s => s.Nombre)
                .ToListAsync();

            return View(sedes);
        }

        // ===================================================================================
        // 2. DETALLE DE LA SEDE Y SUS ALOJAMIENTOS
        // ===================================================================================
        [HttpGet]
        public async Task<IActionResult> Detalles(int id)
        {
            if (id <= 0)
            {
                return BadRequest("El identificador de la sede no es válido.");
            }

            var sede = await _context.Sedes
                .Include(s => s.Alojamientos)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sede == null)
            {
                return NotFound($"No se encontró ninguna sede con el ID {id}.");
            }

            return View(sede);
        }

        // ===================================================================================
        // 3. ACCIÓN QUE LA PANTALLA NECESITA: LIQUIDACIÓN DINÁMICA DE ESTADÍA
        // ===================================================================================
        [HttpGet]
        public async Task<IActionResult> ConsultarTarifas(int sedeId, int alojamientoId, int? cantidadPersonas, DateTime? fechaLlegada, DateTime? fechaSalida, bool? incluyeLavanderia)
        {
            ViewBag.SedeId = sedeId;
            ViewBag.AlojamientoId = alojamientoId;
            ViewBag.Sede = await _context.Sedes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sedeId);
            ViewBag.Alojamiento = await _context.Alojamientos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == alojamientoId);

            // Mantenemos el estado de la selección del interruptor en la UI
            ViewBag.IncluyeLavanderia = incluyeLavanderia ?? false;

            // Si es la primera carga y no se han ingresado filtros, se muestra el formulario limpio
            if (!cantidadPersonas.HasValue || !fechaLlegada.HasValue || !fechaSalida.HasValue)
            {
                return View(null);
            }

            // Validación previa al consumo de la base de datos
            if (cantidadPersonas <= 0 || fechaLlegada >= fechaSalida)
            {
                ViewBag.Error = "Configure un rango de fechas válido y al menos 1 persona.";
                return View(null);
            }

            var liquidacion = new CostoEstadiaDto();

            // Invocación segura al procedimiento almacenado sp_CalcularCostoReserva usando ADO.NET
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_CalcularCostoReserva";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@AlojamientoId", alojamientoId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada.Value));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", fechaSalida.Value));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas.Value));

                // Mapeo de la lavandería opcional elegida por el usuario (1 si es true, 0 si es false)
                command.Parameters.Add(CrearParametro(command, "@IncluyeLavanderia", (incluyeLavanderia == true) ? 1 : 0));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        liquidacion.NochesTotales = reader.GetInt32(reader.GetOrdinal("NochesTotales"));
                        liquidacion.SubtotalTarifaBase = reader.GetDecimal(reader.GetOrdinal("SubtotalTarifaBase"));
                        liquidacion.TotalExcedentePersonas = reader.GetDecimal(reader.GetOrdinal("TotalExcedentePersonas"));
                        liquidacion.CostoLavanderia = reader.GetDecimal(reader.GetOrdinal("CostoLavanderia"));
                        liquidacion.CostoTotalEstadia = reader.GetDecimal(reader.GetOrdinal("CostoTotalEstadia"));
                    }
                }
                await _context.Database.CloseConnectionAsync();
            }

            ViewBag.CantidadPersonas = cantidadPersonas.Value;
            ViewBag.FechaLlegada = fechaLlegada.Value.ToString("yyyy-MM-dd");
            ViewBag.FechaSalida = fechaSalida.Value.ToString("yyyy-MM-dd");

            return View(liquidacion);
        }

        // Helper auxiliar para inyectar parámetros con soporte contra valores nulos
        private DbParameter CrearParametro(DbCommand command, string nombre, object valor)
        {
            var parametro = command.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            return parametro;
        }
    }

    // DTO obligatorio para mapear el modelo esperado por la vista
    public class CostoEstadiaDto
    {
        public int NochesTotales { get; set; }
        public decimal SubtotalTarifaBase { get; set; }
        public decimal TotalExcedentePersonas { get; set; }
        public decimal CostoLavanderia { get; set; }
        public decimal CostoTotalEstadia { get; set; }
    }
}