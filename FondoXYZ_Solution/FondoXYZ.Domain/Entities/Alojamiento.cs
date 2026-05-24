using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Alojamientos")]
    public class Alojamiento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SedeId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        public string? Descripcion { get; set; }

        public int NumeroHabitaciones { get; set; }
        public int CapacidadMaxima { get; set; }
        public bool EstadoDisponible { get; set; }

        // Propiedades de navegación
        [ForeignKey("SedeId")]
        public virtual Sede Sede { get; set; } = null!;
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}