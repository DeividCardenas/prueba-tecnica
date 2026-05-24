using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FondoXYZ.Domain.Entities
{
    [Table("Festivos")]
    public class Festivo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Fecha { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = null!;

        public bool EsAltaTemporada { get; set; }
    }
}