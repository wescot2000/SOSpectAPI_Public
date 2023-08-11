using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class SolicitudesPendientesResponse
    {
        public string user_id_thirdparty { get; set; }
        public string user_id_thirdparty_protector { get; set; }
        public string login { get; set; }
        public DateTime fecha_solicitud { get; set; }


    }
}
