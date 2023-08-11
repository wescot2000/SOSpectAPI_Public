using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using SospectAPI.Data;
using SospectAPI.Data.Entities;
using SospectAPI.Data.ObjetosConsulta;
using SospectAPI.Helpers;
using SospectAPI.Models;
using SospectAPI.Services;
using System;
using System.Data.Entity.Core.Mapping;
using System.Data.SqlClient;
using System.Globalization;
using System.Transactions;
using System.Windows.Input;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Amazon.S3;
using Amazon.S3.Model;


namespace SospectAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReportesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public ReportesController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarParticipacionTipoAlarma")]
        public async Task<IActionResult> ListarParticipacionTipoAlarma(string user_id_thirdParty, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                
                try
                {
                    var v_idioma_origen = "es";
                    var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ListaReporteBasParticipacionResponse> ListaParticipacionTipoAlarma = new List<ListaReporteBasParticipacionResponse>();
                    NpgsqlDataReader reader;

                    string sql2 = $"SELECT * FROM public.ConsultaParticipacionTiposAlarma(\'{user_id_thirdParty}\');";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        
                        reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaReporteBasParticipacionResponse ListaParticipacion = new ListaReporteBasParticipacionResponse();

                            ListaParticipacion.tipoalarma_id = reader.GetInt32(0);
                            ListaParticipacion.descripciontipoalarma = reader.GetString(1);
                            ListaParticipacion.CantidadAlarmas = reader.GetInt64(2);
                            ListaParticipacion.Participacion = reader.GetDecimal(3);
                            ListaParticipacion.fecha_inicio_reporte = reader.GetDateTime(4);
                            ListaParticipacion.fecha_fin_reporte = reader.GetDateTime(5);

                            ListaParticipacionTipoAlarma.Add(ListaParticipacion);
                        }

                    }
                    reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaParticipacionTipoAlarma;
                    responseMessage.Message = "Lista de participacion de tipos de alarma consultada correctamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Reportes/ListarParticipacionTipoAlarma",
                        method = "GET",
                        request_body = "",
                        response_body = responseMessage,
                        db_response = "",
                        error_message = ex.Message,
                        stack_trace = ex.StackTrace
                    };

                    // Convierte la información en una cadena JSON
                    var logString = JsonConvert.SerializeObject(logData);

                    // Crea un objeto PutObjectRequest
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = "bucketS3_AWS",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    var response = await _s3.PutObjectAsync(putRequest);

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de participacion de tipos de alarma";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la participacion de tipos de alarma, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListaMetricasBasicas")]
        public async Task<IActionResult> ListaMetricasBasicas(string user_id_thirdParty, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {

                try
                {
                    var v_idioma_origen = "es";
                    var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ListaReporteMetricasBasicasResponse> ListaMetricasBasicas = new List<ListaReporteMetricasBasicasResponse>();
                    NpgsqlDataReader reader;

                    string sql2 = $"SELECT * FROM public.MetricasSueltasBasicas(\'{user_id_thirdParty}\');";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        
                        reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaReporteMetricasBasicasResponse Metrica = new ListaReporteMetricasBasicasResponse();

                            Metrica.metrica = reader.GetString(0);
                            Metrica.cantidad = reader.GetInt64(1);

                            ListaMetricasBasicas.Add(Metrica);
                        }

                    }
                    reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaMetricasBasicas;
                    responseMessage.Message = "Lista de metricas basicas consultada correctamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Reportes/ListaMetricasBasicas",
                        method = "GET",
                        request_body = "",
                        response_body = responseMessage,
                        db_response = "",
                        error_message = ex.Message,
                        stack_trace = ex.StackTrace
                    };

                    // Convierte la información en una cadena JSON
                    var logString = JsonConvert.SerializeObject(logData);

                    // Crea un objeto PutObjectRequest
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = "bucketS3_AWS",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    var response = await _s3.PutObjectAsync(putRequest);

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de metricas basicas";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar metricas basicas, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ObtenerPromedioEfectivoAlarmas")]
        public async Task<IActionResult> ObtenerPromedioEfectivoAlarmas(string user_id_thirdParty, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var v_idioma_origen = "es";
                    var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    ListaReporteEfectividadAlarmasResponse EfectividadAlarmas = null; // Not a list now
                    NpgsqlDataReader reader;

                    string sql2 = $"SELECT * FROM public.MetricasAlarmasEnZona(\'{user_id_thirdParty}\');";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        reader = command3.ExecuteReader();

                        if (reader.Read()) 
                        {
                            EfectividadAlarmas = new ListaReporteEfectividadAlarmasResponse();

                            EfectividadAlarmas.metrica = reader.GetString(0);
                            EfectividadAlarmas.total_alarmas = reader.GetInt64(1);
                            EfectividadAlarmas.alarmas_ciertas = reader.GetInt64(2);
                            EfectividadAlarmas.alarmas_falsas = reader.GetInt64(3);
                            EfectividadAlarmas.porcentaje_ciertas = reader.GetDecimal(4);
                        }
                    }

                    reader.Close(); // Close the reader before executing another command on the connection
                    connection.Close(); // Close the current connection

                    if (EfectividadAlarmas != null) // We add this to ensure that we found a record
                    {
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = EfectividadAlarmas; // We are now returning a single object, not a list
                        responseMessage.Message = "Efectividad de alarmas consultada correctamente.";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "No se encontró ninguna efectividad de alarmas con los criterios proporcionados.";
                        return NotFound(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Reportes/ListaMetricasBasicas",
                        method = "GET",
                        request_body = "",
                        response_body = responseMessage,
                        db_response = "",
                        error_message = ex.Message,
                        stack_trace = ex.StackTrace
                    };

                    // Convierte la información en una cadena JSON
                    var logString = JsonConvert.SerializeObject(logData);

                    // Crea un objeto PutObjectRequest
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = "bucketS3_AWS",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    var response = await _s3.PutObjectAsync(putRequest);

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de efectividad de alarmas";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la efectividad de alarmas, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}
