using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class CalificarAlarmaRequest 
        {
        public string p_user_id_thirdparty { get; set; }
        public long alarma_id { get; set; }
        public Boolean VeracidadAlarma { get; set; }
        }

}
