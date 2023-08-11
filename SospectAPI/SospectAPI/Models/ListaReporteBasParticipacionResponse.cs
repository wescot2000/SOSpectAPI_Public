using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ListaReporteBasParticipacionResponse
    {
        public int tipoalarma_id { get; set; }
        public string descripciontipoalarma { get; set; }
        public long CantidadAlarmas { get; set; }
        public decimal Participacion { get; set; }
        public DateTime fecha_inicio_reporte { get; set; }
        public DateTime fecha_fin_reporte { get; set; }
    }
}
