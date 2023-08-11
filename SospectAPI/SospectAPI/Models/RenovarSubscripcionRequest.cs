using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class RenovarSubscripcionRequest
    {
        public long subscripcion_id { get; set; }
        public string user_id_thirdparty { get; set; }
        public int cantidad_poderes { get; set; }
        public string idioma { get; set; }
    }
}