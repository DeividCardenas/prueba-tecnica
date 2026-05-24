using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Tarifas")]
    public class Tarifa
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SedeId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Descripcion { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorBase { get; set; }

        public int CapacidadBase { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValorPersonaAdicional { get; set; }

        public int? AplicaDia { get; set; }

        [MaxLength(100)]
        public string? Temporada { get; set; }

        // Propiedad de navegación
        [ForeignKey("SedeId")]
        public virtual Sede Sede { get; set; } = null!;
    }
}