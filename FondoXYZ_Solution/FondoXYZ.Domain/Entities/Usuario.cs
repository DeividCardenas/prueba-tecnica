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

        [Column(TypeName = "date")]
        public DateTime? FechaNacimiento { get; set; }



        [MaxLength(100)]
        public string? Departamento { get; set; }

        [MaxLength(100)]
        public string? Municipio { get; set; }

        [MaxLength(100)]
        public string? Barrio { get; set; }

        [MaxLength(250)]
        public string? DireccionResidencia { get; set; }

        [MaxLength(50)]
        public string? TelefonoResidencia { get; set; }

        [MaxLength(150)]
        public string? PreguntaSecreta { get; set; }

        [MaxLength(150)]
        public string? RespuestaSecreta { get; set; }

        public bool AutorizaCorreo { get; set; }
        public bool AutorizaCelular { get; set; }



        public DateTime FechaRegistro { get; set; }

        // Propiedad de navegación
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}