using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Sedes")]
    public class Sede
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        public string? Descripcion { get; set; }

        [MaxLength(200)]
        public string? Ubicacion { get; set; }

        [Required]
        [MaxLength(100)]
        public string Tipo { get; set; } = null!;

        public int CapacidadMaxima { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Alojamiento> Alojamientos { get; set; } = new List<Alojamiento>();
        public virtual ICollection<Tarifa> Tarifas { get; set; } = new List<Tarifa>();
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}