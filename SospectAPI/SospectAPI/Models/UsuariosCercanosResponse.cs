using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class UsuariosCercanosResponse 
        {
        public string? user_id_thirdparty { get; set; }
        public long? persona_id { get; set; }
        public long alarma_id { get; set; }
        public decimal latitud_alarma { get; set; }
        public decimal longitud_alarma { get; set; }
        public string? txt_notif { get; set; }
    }
}
