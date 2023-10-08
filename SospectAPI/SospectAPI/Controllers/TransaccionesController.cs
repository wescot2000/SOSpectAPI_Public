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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Amazon.S3;
using Amazon.S3.Model;

namespace SospectAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TransaccionesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public TransaccionesController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("CompraPoderes")]
        public async Task<IActionResult> CompraPoderes([FromBody] TransaccionesRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.CompraPoderes(:p_user_id_thirdparty,:p_cantidad,:p_valor,:p_ip_transaccion,:p_purchase_token,:p_tipo_transaccion);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_cantidad", NpgsqlDbType.Smallint, model.cantidad);
                    command.Parameters.AddWithValue(":p_valor", NpgsqlDbType.Numeric, model.valor);
                    command.Parameters.AddWithValue(":p_ip_transaccion", NpgsqlDbType.Varchar, model.ip_transaccion);
                    command.Parameters.AddWithValue(":p_purchase_token", NpgsqlDbType.Varchar, model.p_purchase_token);
                    command.Parameters.AddWithValue(":p_tipo_transaccion", NpgsqlDbType.Varchar, model.p_tipo_transaccion);

                    result = await command.ExecuteNonQueryAsync();
                    if (result < 0)
                    {
                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = "Compra realizada exitosamente";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "Ocurrio un error al hacer la compra. Codigo: 1";
                        return BadRequest(responseMessage);
                    }
                }   
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Transacciones/CompraPoderes",
                        method = "POST",
                        request_body = model,
                        response_body = responseMessage,
                        db_response = result,
                        error_message = ex.Message,
                        stack_trace = ex.StackTrace
                    };

                    // Convierte la información en una cadena JSON
                    var logString = JsonConvert.SerializeObject(logData);

                    // Crea un objeto PutObjectRequest
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = "sospect-s3-data-bucket-prod",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    try
                    {
                        var response = await _s3.PutObjectAsync(putRequest);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error writing to AWS");
                    }

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error al hacer la compra. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible hacer la compra, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarTiposSubscripciones")]
        public async Task<IActionResult> ListarTiposSubscripciones(string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<TiposSubscripcionesResponse> ListaTiposSubscripciones = new List<TiposSubscripcionesResponse>();
                    string sql2 = $"select tipo_subscr_id,\r\ndescripcion_tipo\r\n,cantidad_poderes_requeridos\r\n,cantidad_subscripcion\r\n,tiempo_subscripcion_dias\r\n,texto\r\nfrom vw_consulta_valores_por_subscripciones;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        var v_idioma_origen = "es";
                        var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;

                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            TiposSubscripcionesResponse TipoSubscripcion = new TiposSubscripcionesResponse();

                            TipoSubscripcion.tipo_subscr_id = reader.GetInt32(0);
                            TipoSubscripcion.descripcion_tipo = reader[1].ToString();
                            TipoSubscripcion.cantidad_poderes_requeridos = reader.GetInt16(2);
                            TipoSubscripcion.cantidad_subscripcion = reader.GetInt16(3);
                            TipoSubscripcion.tiempo_subscripcion_dias = reader.GetInt16(4);
                            TipoSubscripcion.texto = reader[5].ToString();

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    TipoSubscripcion.descripcion_tipo = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, TipoSubscripcion.descripcion_tipo);
                                    TipoSubscripcion.texto = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, TipoSubscripcion.texto);
                                }
                                catch (Exception)
                                {
                                    TipoSubscripcion.descripcion_tipo = reader[0].ToString();
                                    TipoSubscripcion.texto = reader[4].ToString();
                                }

                            }

                            ListaTiposSubscripciones.Add(TipoSubscripcion);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaTiposSubscripciones;
                    responseMessage.Message = "Tipos de subscripciones consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Transacciones/ListarTiposSubscripciones",
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
                        BucketName = "sospect-s3-data-bucket-prod",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    try
                    {
                        var response = await _s3.PutObjectAsync(putRequest);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error writing to AWS");
                    }

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de tipos de subscripciones";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de tipos de subscripciones, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarValoresPoderes")]
        public async Task<IActionResult> ListarValoresPoderes()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ValoresPoderesResponse> ListaValoresPoderes = new List<ValoresPoderesResponse>();
                    string sql2 = $"select  cantidad as cantidad_poderes\n,valor_usd\n,cast(poder_id as varchar(50)) as \"ProductId\" \n,\"ProductId\" as ProductDesc\nfrom public.poderes order by cantidad asc;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            ValoresPoderesResponse ValoresPoderes = new ValoresPoderesResponse();

                            ValoresPoderes.cantidad_poderes = reader.GetInt16(0);
                            ValoresPoderes.valor_usd = reader.GetDecimal(1);
                            ValoresPoderes.ProductId = reader[2].ToString();
                            ValoresPoderes.ProductDesc = reader[3].ToString();

                            ListaValoresPoderes.Add(ValoresPoderes);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaValoresPoderes;
                    responseMessage.Message = "Valores de poderes consultados.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Transacciones/ListarValoresPoderes",
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
                        BucketName = "sospect-s3-data-bucket-prod",
                        Key = $"error_logs/{DateTime.UtcNow:yyyyMMdd_HHmmss}.json",
                        ContentType = "application/json",
                        ContentBody = logString
                    };

                    // Sube el registro de error a S3
                    try
                    {
                        var response = await _s3.PutObjectAsync(putRequest);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Error writing to AWS");
                    }

                    // Devuelve una respuesta de error al cliente
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de los valores de los poderes";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de poderes y sus valores, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}

