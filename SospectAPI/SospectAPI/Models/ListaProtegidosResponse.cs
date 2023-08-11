using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ListaProtegidosResponse
    {
        public string user_id_thirdParty_protector { get; set; }
        public string user_id_thirdParty_protegido { get; set; }
        public string login_protector { get; set; }
        public string login_protegido { get; set; }
        public DateTime fecha_activacion { get; set; }
        public DateTime fecha_finalizacion { get; set; }

    }
}
