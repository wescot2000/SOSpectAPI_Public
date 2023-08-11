using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SospectAPI.Data.Entities
{
    public class SubscripcionesVigilancia
    {
        public string p_user_id_thirdparty_protector { get; set; }
        public double Latitud_zona { get; set; }
        public double Longitud_zona { get; set; }
        public string? idioma { get; set; }
    }
}