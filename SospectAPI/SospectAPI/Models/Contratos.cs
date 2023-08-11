using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class Contratos
    {
        [Required]
        public string p_user_id_thirdparty { get; set; }

        [Required]
        public string p_ip_aceptacion { get; set; }

    }
}