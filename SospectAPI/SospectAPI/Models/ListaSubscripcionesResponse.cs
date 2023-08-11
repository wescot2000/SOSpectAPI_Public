using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ListaSubscripcionesResponse
    {
        public long subscripcion_id { get; set; }
        public string user_id_thirdparty { get; set; }
        public string? descripcion_tipo { get; set; }
        public string? descripcion { get; set; }
        public DateTime? fecha_finalizacion { get; set; }
        public int? poderes_renovacion { get; set; }
        public bool? flag_subscr_vencida { get; set; }
        public string? observ_subscripcion { get; set; }
        public bool flag_renovable { get; set; }
        public string? texto_renovable { get; set; }

    }
}
