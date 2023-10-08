using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class AlarmaAsignadaResponse
    {
        public string? user_id_thirdparty { get; set; }
        public long alarma_id { get; set; }
        public string? txt_notif { get; set; }
        public string? idioma_destino { get; set; }
        public string? txt_notif_original { get; set; }
    }
}
