using System;
namespace SospectAPI.Models
{
    public class NotificationRequest
    {
        public string Text { get; set; }
        public string Action { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public bool Silent { get; set; }
        public string userThirdParty { get; set; }
        public long alarma_id { get; set; }
        public decimal latitud_alarma { get; set; }
        public decimal longitud_alarma { get; set; }

        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();

    }
}

