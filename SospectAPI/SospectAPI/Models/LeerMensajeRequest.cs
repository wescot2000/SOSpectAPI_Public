using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class LeerMensajeRequest
    {
        public string p_user_id_thirdparty { get; set; }
        public  string idioma_dispositivo { get; set; }
        public long p_mensaje_id { get; set; }

    }

}
