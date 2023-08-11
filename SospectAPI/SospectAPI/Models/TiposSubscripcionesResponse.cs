using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class TiposSubscripcionesResponse
    {
        public int tipo_subscr_id { get; set; }
        public string descripcion_tipo { get; set; }
        public short? cantidad_poderes_requeridos { get; set; }
        public short? cantidad_subscripcion { get; set; }
        public short? tiempo_subscripcion_dias { get; set; }
        public string? texto { get; set; }

    }
}
