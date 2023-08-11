using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class CalificarDescAlarmaRequest 
        {
        public string p_user_id_thirdparty { get; set; }
        public long iddescripcion { get; set; }
        public string CalificacionDescripcion { get; set; }

    }

}
