using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class CerrarAlarmaRequest
    {
        public long p_alarma_id { get; set; }
        public string p_user_id_thirdparty { get; set; }
        public string p_descripcion_cierre { get; set; }
        public Boolean p_flag_es_falsaalarma { get; set; }
        public Boolean p_flag_hubo_captura { get; set; }
        public string p_idioma { get; set; }
        
        }

}
