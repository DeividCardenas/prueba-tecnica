using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Temporadas")]
    public class Temporada
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = null!;

        [Required]
        [Column(TypeName = "date")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime FechaFin { get; set; }

        public string? Descripcion { get; set; }
        public bool EsAltaTemporada { get; set; }
        public bool Activa { get; set; }
    }
}