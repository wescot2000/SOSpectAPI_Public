using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class Personas
    {
        public int persona_id { get; set; }

        [Required]
        [MaxLength(150)]
        public string? login { get; set; }

        [Required]
        public int radio_alarmas_id { get; set; }

        [Required]
        [MaxLength(150)]
        public string? user_id_thirdparty { get; set; }

        public DateTime? fechacreacion { get; set; }

        public int marca_bloqueo { get; set; }
        public string RegistrationId { get; set; }
        public string Plataforma { get; set; }
        public string Idioma { get; set; }
    }
}