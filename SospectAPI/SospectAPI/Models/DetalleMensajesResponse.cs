using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class DetalleMensajesResponse
    {
        public long mensaje_id { get; set; }
        public string asunto { get; set; }
        public bool estado { get; set; }
        public string texto { get; set; }
        public string para { get; set; }
        public string remitente { get; set; }
        public DateTime fecha_mensaje { get; set; }
        public string idioma_origen { get; set; }
        public long? alarma_id { get; set; }

    }
}
