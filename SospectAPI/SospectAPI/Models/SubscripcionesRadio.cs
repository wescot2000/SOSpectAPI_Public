using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class SubscripcionesRadio
    {
        public string p_user_id_thirdparty_protector { get; set; }
        public int cantidad_subscripcion { get; set; }
        public string? idioma { get; set; }
    }
}