using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class ParametrosResponse
    {
        public string p_user_id_thirdparty { get; set; }
        public short? TiempoRefrescoUbicacion { get; set; }
        public short? marca_bloqueo { get; set; }
        public short radio_mts { get; set; }
        public int? MensajesParaUsuario { get; set; }
        public bool flag_bloqueo_usuario { get; set; }
        public bool flag_usuario_debe_firmar_cto { get; set; }
        public int? saldo_poderes { get; set; }
        public decimal latitud { get; set; }
        public decimal longitud { get; set; }
        public DateTime? fechafin_bloqueo_usuario { get; set; }
        public int? radio_alarmas_mts_actual { get; set; }
        public decimal credibilidad_persona { get; set; }

    }
}
