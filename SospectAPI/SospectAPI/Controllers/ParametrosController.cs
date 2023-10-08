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
    public class ParametrosController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public ParametrosController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("ConsultaParametros")]
        public async Task<IActionResult> ConsultaParametros([FromBody] Parametros model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.ActualizaParametros(:p_user_id_thirdparty,:p_registrationid,:p_idioma);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_registrationid", NpgsqlDbType.Varchar, model.RegistrationId);
                    command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.Idioma);


                   
                    result = await command.ExecuteNonQueryAsync();


                    ParametrosResponse RegistroDeParametros = new ParametrosResponse();
                   
                    
                    string sql2 = $"select \r\nuser_id_thirdParty\r\n,tiempo_refresco_mapa\r\n,marca_bloqueo\r\n,radio_mts\r\n,MensajesParaUsuario\r\n,flag_bloqueo_usuario\r\n,flag_usuario_debe_firmar_cto\r\n,saldo_poderes\r\n,idioma_destino\r\n,registrationid\r\n,latitud\r\n,longitud\r\n,fechafin_bloqueo_usuario\r\n,radio_alarmas_mts_actual\r\n,credibilidad_persona \r\n from public.vw_consulta_parametros_usuario\r\nwhere user_id_thirdParty=\'{model.p_user_id_thirdparty}\'\r\nand registrationid=\'{model.RegistrationId}\';";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
 

                            RegistroDeParametros.p_user_id_thirdparty = reader[0].ToString();
                            RegistroDeParametros.TiempoRefrescoUbicacion = reader.GetInt16(1);
                            RegistroDeParametros.marca_bloqueo = reader.GetInt16(2);
                            RegistroDeParametros.radio_mts = reader.GetInt16(3);
                            RegistroDeParametros.MensajesParaUsuario = reader.GetInt16(4);
                            RegistroDeParametros.flag_bloqueo_usuario = reader.GetBoolean(5);
                            RegistroDeParametros.flag_usuario_debe_firmar_cto = reader.GetBoolean(6);
                            RegistroDeParametros.saldo_poderes = reader[7] == null ? 0 : reader.GetInt32(7);
                            RegistroDeParametros.latitud=reader.GetDecimal(10);
                            RegistroDeParametros.longitud= reader.GetDecimal(11);
                            RegistroDeParametros.fechafin_bloqueo_usuario=reader.GetDateTime(12);
                            RegistroDeParametros.radio_alarmas_mts_actual = reader.GetInt32(13);
                            RegistroDeParametros.credibilidad_persona = reader.GetDecimal(14);


                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = RegistroDeParametros;
                    responseMessage.Message = "Parametros consultados exitosamente";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Parametros/ConsultaParametros",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de parametros. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar los parametros, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}
