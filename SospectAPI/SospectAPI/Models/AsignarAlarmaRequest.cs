using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class AsignarAlarmaRequest
    {
        public long p_alarma_id { get; set; }
        public string p_user_id_thirdparty { get; set; }
        public string p_idioma { get; set; }
        
        }

}
