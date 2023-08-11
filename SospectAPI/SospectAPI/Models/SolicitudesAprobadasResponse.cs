using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class SolicitudesAprobadasResponse
    {
        public string user_id_thirdparty_protector { get; set; }
        public string user_id_thirdparty_protegido { get; set; }
        public string login { get; set; }
        public DateTime fecha_aprobado { get; set; }
        public int tiporelacion_id { get; set; }

    }
}
