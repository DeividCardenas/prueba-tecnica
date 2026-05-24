using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Reservas")]
    public class Reserva
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public int SedeId { get; set; }

        [Required]
        public int AlojamientoId { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime FechaLlegada { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime FechaSalida { get; set; }

        public int CantidadPersonas { get; set; }
        public bool IncluyeLavanderia { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoTotal { get; set; }

        [MaxLength(50)]
        public string EstadoReserva { get; set; } = "Pendiente";

        public DateTime FechaReserva { get; set; }
        public DateTime? FechaPago { get; set; }

        // Propiedades de navegación
        [ForeignKey("UsuarioId")]
        public virtual Usuario Usuario { get; set; } = null!;

        [ForeignKey("SedeId")]
        public virtual Sede Sede { get; set; } = null!;

        [ForeignKey("AlojamientoId")]
        public virtual Alojamiento Alojamiento { get; set; } = null!;
    }
}