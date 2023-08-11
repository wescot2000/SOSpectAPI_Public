using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class SuspensionProtegido
    {
        public string p_user_id_thirdparty_protegido { get; set; }
        public int p_tiempo_suspension { get; set; }
        public string idioma { get; set; }
    }
}