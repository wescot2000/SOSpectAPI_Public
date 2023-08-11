using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class TransaccionesRequest
    {
        public string p_user_id_thirdparty { get; set; }
        public short? cantidad { get; set; }
        public decimal? valor { get; set; }
        public string? ip_transaccion { get; set; }
        public string? p_tipo_transaccion { get; set; }
        public string p_purchase_token  { get; set; }

    }
}
