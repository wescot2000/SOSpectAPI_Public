using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class DescripcionesAlarmasResponse
    {
        public long iddescripcion { get; set; }
        public long alarma_id { get; set; }
        public long persona_id { get; set; }
        public string? descripcionalarma { get; set; }
        public string? descripcionsospechoso { get; set; }
        public string? descripcionvehiculo { get; set; }
        public string? descripcionarmas { get; set; }
        public bool? FlagEditado { get; set; }
        public DateTime? fechadescripcion { get; set; }
        public bool propietario_descripcion { get; set; }
        public string? calificacion_otras_descripciones { get; set; }
        public short calificaciondescripcion { get; set; }
        public int? tipoalarma_id { get; set; }
        public string? descripciontipoalarma { get; set; }
        public bool EsAlarmaActiva { get; set; }
    }
}
