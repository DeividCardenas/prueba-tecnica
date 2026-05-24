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
    [Route("api/tarifas")]
    [ApiController]
    [Authorize]
    public class TarifasApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TarifasApiController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet("calcular")]
        public async Task<IActionResult> CalcularTarifas([FromQuery] int sedeId, [FromQuery] int alojamientoId, [FromQuery] int cantidadPersonas, [FromQuery] int cantidadVisitantes, [FromQuery] DateTime fechaLlegada, [FromQuery] DateTime fechaSalida, [FromQuery] bool incluyeLavanderia = false)
        {
            if (cantidadPersonas <= 0 || fechaLlegada >= fechaSalida)
            {
                return BadRequest(new { mensaje = "Configure un rango de fechas válido y al menos 1 persona." });
            }

            var liquidacion = new CostoEstadiaDto();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_CalcularCostoReserva";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@AlojamientoId", alojamientoId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada));
                command.Parameters.Add(CrearParametro(command, "@FechaSalida", fechaSalida));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas));
                command.Parameters.Add(CrearParametro(command, "@CantidadVisitantes", cantidadVisitantes));
                command.Parameters.Add(CrearParametro(command, "@IncluyeLavanderia", incluyeLavanderia ? 1 : 0));

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

            return Ok(liquidacion);
        }

        [HttpGet("preview")]
        public async Task<IActionResult> PreviewTarifa([FromQuery] int sedeId, [FromQuery] int alojamientoId, [FromQuery] int cantidadPersonas, [FromQuery] int cantidadVisitantes, [FromQuery] DateTime fechaLlegada)
        {
            if (sedeId <= 0 || alojamientoId <= 0 || cantidadPersonas <= 0 || fechaLlegada == default)
            {
                return BadRequest(new { mensaje = "Parámetros inválidos para el preview de tarifa." });
            }

            var preview = new PreviewTarifaDto();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "sp_PreviewTarifa";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(CrearParametro(command, "@SedeId", sedeId));
                command.Parameters.Add(CrearParametro(command, "@AlojamientoId", alojamientoId));
                command.Parameters.Add(CrearParametro(command, "@FechaLlegada", fechaLlegada.Date));
                command.Parameters.Add(CrearParametro(command, "@CantidadPersonas", cantidadPersonas));
                command.Parameters.Add(CrearParametro(command, "@CantidadVisitantes", cantidadVisitantes));

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        preview.TipoTarifa               = reader.GetString(reader.GetOrdinal("TipoTarifa"));
                        preview.ValorBaseNoche            = reader.GetDecimal(reader.GetOrdinal("ValorBaseNoche"));
                        preview.CapacidadIncluida         = reader.GetInt32(reader.GetOrdinal("CapacidadIncluida"));
                        preview.ValorPersonaAdicional     = reader.GetDecimal(reader.GetOrdinal("ValorPersonaAdicional"));
                        preview.CostoPersonasAdicionales  = reader.GetDecimal(reader.GetOrdinal("CostoPersonasAdicionales"));
                        preview.ValorVisitanteDia         = reader.GetDecimal(reader.GetOrdinal("ValorVisitanteDia"));
                        preview.EsAltaTemporada           = reader.GetBoolean(reader.GetOrdinal("EsAltaTemporada"));
                        preview.EsFestivo                 = reader.GetBoolean(reader.GetOrdinal("EsFestivo"));
                        preview.DescripcionTarifa         = reader.GetString(reader.GetOrdinal("DescripcionTarifa"));
                    }
                }
                await _context.Database.CloseConnectionAsync();
            }

            return Ok(preview);
        }

        private DbParameter CrearParametro(DbCommand command, string nombre, object valor)
        {
            var parametro = command.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value;
            return parametro;
        }
    }

    // DTO para el preview de tarifa
    public class PreviewTarifaDto
    {
        public string TipoTarifa              { get; set; } = string.Empty;
        public decimal ValorBaseNoche         { get; set; }
        public int CapacidadIncluida          { get; set; }
        public decimal ValorPersonaAdicional  { get; set; }
        public decimal CostoPersonasAdicionales { get; set; }
        public decimal ValorVisitanteDia      { get; set; }
        public bool EsAltaTemporada           { get; set; }
        public bool EsFestivo                 { get; set; }
        public string DescripcionTarifa       { get; set; } = string.Empty;
    }
}

