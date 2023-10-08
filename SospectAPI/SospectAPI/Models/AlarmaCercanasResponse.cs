using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class AlarmaCercanasResponse 
        {
        public string? user_id_thirdparty { get; set; }
        public long? persona_id { get; set; }
        public string? user_id_creador_alarma { get; set; }
        public string? login_usuario_notificar { get; set; }
        public decimal latitud_alarma { get; set; }
        public decimal longitud_alarma { get; set; }
        public decimal latitud_entrada { get; set; }
        public decimal longitud_entrada { get; set; }
        public string? tipo_subscr_activa_usuario { get; set; }
        public DateTime? fecha_activacion_subscr { get; set; }
        public DateTime? fecha_finalizacion_subscr { get; set; }
        public decimal distancia_en_metros { get; set; }
        public long alarma_id { get; set; }
        public DateTime? fecha_alarma { get; set; }
        public string? descripciontipoalarma { get; set; }
        public short tipoalarma_id { get; set; }
        public short TiempoRefrescoUbicacion { get; set; }
        public bool flag_propietario_alarma { get; set; }
        public decimal? calificacion_actual_alarma { get; set; }
        public bool usuariocalificoalarma { get; set; }
        public string? calificacionalarmausuario { get; set; }
        public bool EsAlarmaActiva { get; set; }
        public long? alarma_id_padre { get; set; }
        public decimal? calificacion_alarma { get; set; }
        public bool estado_alarma { get; set; }
        public bool Flag_hubo_captura { get; set; }
        public bool flag_alarma_siendo_atendida { get; set; }
        public int cantidad_agentes_atendiendo { get; set; }
        public int cantidad_interacciones { get; set; }
        public bool flag_es_policia { get; set; }
    }
}
