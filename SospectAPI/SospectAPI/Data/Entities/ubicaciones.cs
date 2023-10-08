using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class Ubicaciones
    {
        public int ubicacion_id { get; set; }
        
        [Required]
        public string p_user_id_thirdparty { get; set; }

        [Required]
        public double? latitud { get; set; }

        [Required]
        public double? longitud { get; set; }

        [Required]
        public string Idioma { get; set; }

        [Required]
        public string PantallaOrigen { get; set; }

    }
}