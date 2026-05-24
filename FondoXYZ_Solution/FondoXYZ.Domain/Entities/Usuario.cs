using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FondoXYZ.Domain.Entities
{
    [Table("Usuarios")]
    public class Usuario : IdentityUser<int>
    {

        [Required]
        [MaxLength(50)]
        public string NroDocumento { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        public string NombreCompleto { get; set; } = null!;

        public DateTime FechaRegistro { get; set; }

        // Propiedad de navegación
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}