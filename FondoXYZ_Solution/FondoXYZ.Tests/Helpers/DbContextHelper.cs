using FondoXYZ.Domain.Entities;
using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FondoXYZ.Tests.Helpers
{
    /// <summary>
    /// Construye un ApplicationDbContext respaldado por InMemory para tests de integración ligeros.
    /// Cada llamada recibe un nombre de base de datos único para aislar pruebas entre sí.
    /// </summary>
    public static class DbContextHelper
    {
        public static ApplicationDbContext CreateInMemoryContext(string dbName = "")
        {
            if (string.IsNullOrEmpty(dbName))
                dbName = Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Retorna un contexto con datos semilla básicos: una Sede y un Alojamiento.
        /// </summary>
        public static ApplicationDbContext CreateSeededContext(string dbName = "")
        {
            var ctx = CreateInMemoryContext(dbName);

            var sede = new Sede
            {
                Id = 1, Nombre = "Villeta", Tipo = "Sede Recreativa",
                CapacidadMaxima = 32, Descripcion = "Sede de prueba", Ubicacion = "Cundinamarca"
            };
            var alojamiento = new Alojamiento
            {
                Id = 1, SedeId = 1, Nombre = "Alojamiento 1",
                NumeroHabitaciones = 1, CapacidadMaxima = 4,
                Descripcion = "Habitación de prueba", EstadoDisponible = true
            };
            var tarifa = new Tarifa
            {
                Id = 1, SedeId = 1, Descripcion = "Alojamiento (1) - Tarifa Ordinaria (V-D)",
                ValorBase = 70000, CapacidadBase = 4, ValorPersonaAdicional = 16000,
                Temporada = "Ordinaria"
            };

            ctx.Sedes.Add(sede);
            ctx.Alojamientos.Add(alojamiento);
            ctx.Tarifas.Add(tarifa);
            ctx.SaveChanges();

            return ctx;
        }
    }
}
