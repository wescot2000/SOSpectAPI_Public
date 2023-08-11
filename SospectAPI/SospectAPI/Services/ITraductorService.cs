using System;
using SospectAPI.Models;

namespace SospectAPI.Services
{
    public interface ITraductorService
    {
        Task<string> Traducir(string idiomaEntrada, string idiomaDestino, string texto);
    }
}

