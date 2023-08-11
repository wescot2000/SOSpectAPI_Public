using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class AlarmasEnMapaResponse
    {
        public decimal latitud_alarma { get; set; }
        public decimal longitud_alarma { get; set; }
        public long alarma_id { get; set; }
        public DateTime? fecha_alarma { get; set; }
        public string? descripciontipoalarma { get; set; }
        public short tipoalarma_id { get; set; }
    }
}
