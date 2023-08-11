using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ListaReporteEfectividadAlarmasResponse
    {
        public string metrica { get; set; }
        public long total_alarmas { get; set; }
        public long alarmas_ciertas { get; set; }
        public long alarmas_falsas { get; set; }
        public decimal porcentaje_ciertas { get; set; }

    }
}
