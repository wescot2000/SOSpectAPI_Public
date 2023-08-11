using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class AprobacionProtegido
    {
        public string p_user_id_thirdparty_protegido { get; set; }
        public string p_user_id_thirdparty_protector { get; set; }
        public string idioma { get; set; }
    }
}