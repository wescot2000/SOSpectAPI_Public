using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class InsertaAlarmaRequest 
        {
        public string p_user_id_thirdparty { get; set; }
        public int p_tipoalarma_id { get; set; }
        public double p_latitud { get; set; }
        public double p_longitud { get; set; }
        public string? ip_usuario { get; set; }
        public long? p_alarma_id { get; set; }
        public string? idioma_dispositivo { get; set; }
    }

}
