using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class UbicacionesMapa
    {
        public long ubicacion_id { get; set; }
        
        [Required]
        public double? latitud { get; set; }

        [Required]
        public double? longitud { get; set; }
        [Required]
        public string Idioma { get; set; }

    }
}