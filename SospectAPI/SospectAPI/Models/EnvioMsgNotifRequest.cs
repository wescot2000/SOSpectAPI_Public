using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class EnvioMsgNotifRequest
    {
        public string p_asunto { get; set; }
        public string p_mensaje { get; set; }
        public string p_textoNotificacion { get; set; }
    }

}
