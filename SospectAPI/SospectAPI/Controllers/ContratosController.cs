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
    public class ContratosController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public ContratosController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("AceptacionCondiciones")]
        public async Task<IActionResult> AceptacionCondiciones([FromBody] Contratos model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.AceptacionCondiciones(:p_user_id_thirdparty,:p_ip_aceptacion);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_ip_aceptacion", NpgsqlDbType.Varchar, model.p_ip_aceptacion);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = "";
                    responseMessage.Message = "Aceptacion de contrato registrada exitosamente";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Contratos/AceptacionCondiciones",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de Aceptacion de contrato. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible ejecutar la aceptacion de contrato, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ObtenerContrato")]
        public async Task<IActionResult> ObtenerContrato(string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var flag_traducido = false;
                    var texto_contrato_traducido = "";

                    //string sql2 = $"select * from public.traducciones_contrato;";
                    string sql2 = $"select \r\ncase when traduccion_id is not null then cast(true as boolean)\r\nelse cast(false as boolean) end flag_traducido\r\n,coalesce(tc.texto_traducido,'') as texto_traducido\r\nfrom public.condiciones_servicio cs\r\nleft outer join public.traducciones_contrato tc\r\non (tc.contrato_id=cs.contrato_id and tc.fecha_traduccion between cs.fecha_inicio_version and coalesce(cs.fecha_fin_version,cast(now() as timestamp with time zone)) and tc.idioma=\'{idioma_dispositivo}\');";
                    using (NpgsqlCommand command = new NpgsqlCommand(sql2, connection))
                    {
                        NpgsqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            flag_traducido = reader.GetBoolean(0);
                            texto_contrato_traducido = reader[1].ToString() == null ? null : reader[1].ToString();
                        }
                        connection.Close();
                    }

                    if (flag_traducido)
                    {
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = texto_contrato_traducido;
                        responseMessage.Message = "Texto del contrato de condiciones de servicio consultado exitosamente.";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        //Extraccion de datos de contrato y traduccion del mismo
                        await connection.OpenAsync();

                        string sql = $"select texto_contrato\r\nfrom condiciones_servicio cs\r\ninner join numerales_contrato nc\r\non (nc.contrato_id=cs.contrato_id)\r\nwhere cs.fecha_fin_version is null\r\norder by nc.numeral asc;";
                        using (NpgsqlCommand command2 = new NpgsqlCommand(sql, connection))
                        {
                            //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                            NpgsqlDataReader reader2 = command2.ExecuteReader();

                            while (reader2.Read())
                            {
                                var v_idioma_origen = "es";
                                var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;
                                var texto_numeral = reader2[0].ToString();


                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        texto_numeral = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, texto_numeral);

                                        if (texto_numeral.Contains("Error al traducir el texto"))
                                        {
                                            // Si contiene el mensaje de error, usa el contrato en inglés
                                            idioma_dispositivo = "en";
                                            texto_numeral = reader2[0].ToString();
                                        }

                                    }
                                    catch (Exception)
                                    {
                                        texto_numeral = reader2[0].ToString();
                                    }

                                }

                                texto_contrato_traducido += texto_numeral + "\n";
                            }
                            connection.Close();
                        }
                        await connection.OpenAsync();

                        try
                        {
                            var command = new NpgsqlCommand("CALL public.InsertaContratoTraducido(:p_texto_contrato_traducido,:p_idioma_dispositivo);", connection);

                            command.Parameters.AddWithValue(":p_texto_contrato_traducido", NpgsqlDbType.Varchar, texto_contrato_traducido ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue(":p_idioma_dispositivo", NpgsqlDbType.Varchar, idioma_dispositivo);
                            var result = await command.ExecuteNonQueryAsync();
                            connection.Close();
                        }
                        catch (Exception)
                        {
                            await connection.OpenAsync();
                            string sql3 = $"select \r\ncase when traduccion_id is not null then cast(true as boolean)\r\nelse cast(false as boolean) end flag_traducido\r\n,coalesce(tc.texto_traducido,'') as texto_traducido\r\nfrom public.condiciones_servicio cs\r\nleft outer join public.traducciones_contrato tc\r\non (tc.contrato_id=cs.contrato_id and tc.fecha_traduccion between cs.fecha_inicio_version and coalesce(cs.fecha_fin_version,cast(now() as timestamp with time zone)) and tc.idioma='en');";
                            using (NpgsqlCommand command3 = new NpgsqlCommand(sql3, connection))
                            {
                                NpgsqlDataReader reader3 = command3.ExecuteReader();
                                if (reader3.Read())
                                {
                                    flag_traducido = reader3.GetBoolean(0);
                                    texto_contrato_traducido = reader3[1].ToString() == null ? null : reader3[1].ToString();
                                }
                                connection.Close();
                            }

                        }
                        
                    }

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = texto_contrato_traducido;
                    responseMessage.Message = "Texto del contrato de condiciones de servicio consultado exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Contratos/ObtenerContrato",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de condiciones de servicio";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar el texto del contrato de condiciones de servicio, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}
