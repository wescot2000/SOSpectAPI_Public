using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class PermisoProtegido
    {
        public string p_user_id_thirdparty_protector { get; set; }
        public string p_user_id_thirdparty_protegido { get; set; }
        public int tiempo_subscripcion_dias { get; set; }
        public int TiporelacionId { get; set; }
        public string idioma { get; set; }
    }
}