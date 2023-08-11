using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ListaMensajesResponse
    {
        public long mensaje_id { get; set; }
        public string asunto { get; set; }
        public bool estado { get; set; }
        public DateTime fecha_mensaje { get; set; }
        public string idioma_origen { get; set; }
        public string texto { get; set; }
    }
}
