using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SospectAPI.Models;
using SospectAPI.Services;

namespace SospectAPI.Services
{
    public class TraductorService : ITraductorService
    {
        public async Task<string> Traducir(string idiomaEntrada, string idiomaDestino, string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return string.Empty;
            }
            using (var httpClient = new HttpClient())
            {
                var parameters = new Dictionary<string, string>
            {
                { "idioma_entrada", idiomaEntrada },
                { "idioma_destino", idiomaDestino },
                { "texto", texto }
            };

                var content = new FormUrlEncodedContent(parameters);

                var response = await httpClient.PostAsync("http://localhost:8889/traducir", content);

                var responseContent = await response.Content.ReadAsStringAsync();

                return responseContent;
            }
        }
    }
}

