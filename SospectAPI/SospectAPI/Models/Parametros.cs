using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class Parametros
    {
        [Required]
        public string p_user_id_thirdparty { get; set; }

        [Required]
        public string RegistrationId { get; set; }

        [Required]
        public string Idioma { get; set; }


    }
}