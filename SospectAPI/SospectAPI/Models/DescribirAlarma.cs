using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class DescribirAlarmaRequest 
        {
        public string p_user_id_thirdparty { get; set; }
        public long alarma_id { get; set; }
        public string? DescripcionAlarma { get; set; }
        public string? DescripcionSospechoso { get; set; }
        public string? DescripcionVehiculo { get; set; }
        public string? DescripcionArmas { get; set; }
        public int? p_tipoalarma_id { get; set; }
        public double? latitud_escape { get; set; }
        public double? longitud_escape { get; set; }
        public string? ip_usuario { get; set; }
        public string? idioma_descripcion { get; set; }
    }

}
