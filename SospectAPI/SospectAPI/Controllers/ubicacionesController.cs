using Amazon.S3.Model;
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
    public class UbicacionesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        INotificationService _NotificationHubService;

        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public UbicacionesController(IConfiguration configuration, INotificationService NotificationHubService, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _NotificationHubService = NotificationHubService;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("InsertaUbicacion")]
        public async Task<IActionResult> InsertarUbicacion([FromBody] Ubicaciones model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.UpsertUbicacion(:p_user_id_thirdparty,:p_latitud,:p_longitud);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_latitud", NpgsqlDbType.Numeric, model.latitud);
                    command.Parameters.AddWithValue(":p_longitud", NpgsqlDbType.Numeric, model.longitud);

                    result = await command.ExecuteNonQueryAsync();

                    if (result < 0)
                    {
                        var v_idioma_origen = "es";
                        var v_idioma_destino = model.Idioma;
                        string sql5 = $"select coalesce(cantidad,0) as cantidad from public.vw_cantidad_alarmas_zona\r\nWHERE user_id_thirdparty = \'{model.p_user_id_thirdparty}\';";
                        using (NpgsqlCommand command5 = new NpgsqlCommand(sql5, connection))
                        {
                            //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                            var cantidad = 0;
                            NpgsqlDataReader reader5 = command5.ExecuteReader();
                            if (reader5.HasRows)
                            {
                                if (reader5.Read())
                                {
                                    cantidad = reader5.GetInt32(0);
                                }
                            }


                            if (cantidad>0)
                            {
                                var txt_notif = $"Ten cuidado! Acabas de entrar a una zona con alertas activas o tus subscripciones notificaron alertas. Cantidad de alertas a revisar: {cantidad}";

                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, txt_notif);
                                    }
                                    catch (Exception)
                                    {
                                        txt_notif = txt_notif;
                                    }

                                }

                                NotificationRequest notificationRequest = new NotificationRequest()
                                {
                                    Action = "A",
                                    Silent = false,
                                    Tags = new string[1] { model.p_user_id_thirdparty },
                                    Text = txt_notif
                                };

                                await _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);

                            }
                            

                        }
                        connection.Close(); //close the current connection


                        await connection.OpenAsync();

                        var command6 = new NpgsqlCommand("CALL public.updateNotificacion(:p_user_id_thirdparty);", connection);

                        command6.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);

                        var result2 = await command6.ExecuteNonQueryAsync();

                        List<AlarmaCercanasResponse> LstAlarmasCercanas = new List<AlarmaCercanasResponse>();
                        NpgsqlDataReader reader;
                        string sql2 = $"SELECT  user_id_thirdparty\r\n ,persona_id \r\n     ,user_id_creador_alarma\r\n       ,login_usuario_notificar\r\n       ,latitud_alarma\r\n       ,longitud_alarma\r\n       ,latitud_entrada\r\n       ,longitud_entrada\r\n       ,coalesce(tipo_subscr_activa_usuario,'Ninguna')\r\n       ,coalesce(fecha_activacion_subscr,'2000-01-01 00:00:00')\r\n       ,coalesce(fecha_finalizacion_subscr,'2000-01-01 00:00:00')\r\n       ,distancia_en_metros\r\n\t   ,alarma_id\r\n\t   ,fecha_alarma\r\n\t   ,descripciontipoalarma\r\n\t   ,tipoalarma_id\r\n\t   ,TiempoRefrescoUbicacion\r\n\t   ,flag_propietario_alarma\r\n\t   ,calificacion_actual_alarma\r\n\t   ,UsuarioCalificoAlarma\r\n\t   ,CalificacionAlarmaUsuario\r\n\t,cast(TRUE AS BOOLEAN) AS EsAlarmaActiva\r\n\t ,alarma_id_padre\r\n\t ,calificacion_alarma\r\n\t  FROM vw_busca_alarmas_por_zona WHERE user_id_thirdparty = \'{model.p_user_id_thirdparty}\' order by fecha_alarma DESC;\r\n";
                        using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                        {
                            //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                            reader = command3.ExecuteReader();
                            
                            while (reader.Read())
                            {
                                
                                AlarmaCercanasResponse RegistroDeAlarma = new AlarmaCercanasResponse();

                                RegistroDeAlarma.user_id_thirdparty = reader[0].ToString();
                                RegistroDeAlarma.persona_id = reader.GetInt64(1);
                                RegistroDeAlarma.user_id_creador_alarma = reader[2].ToString();
                                RegistroDeAlarma.login_usuario_notificar = reader[3].ToString();
                                RegistroDeAlarma.latitud_alarma = reader.GetDecimal(4);
                                RegistroDeAlarma.longitud_alarma = reader.GetDecimal(5);
                                RegistroDeAlarma.latitud_entrada = reader.GetDecimal(6);
                                RegistroDeAlarma.longitud_entrada = reader.GetDecimal(7);
                                RegistroDeAlarma.tipo_subscr_activa_usuario = reader[8] == null ? null : reader[8].ToString();
                                RegistroDeAlarma.fecha_activacion_subscr = reader.GetDateTime(9);
                                RegistroDeAlarma.fecha_finalizacion_subscr = reader.GetDateTime(10);
                                RegistroDeAlarma.distancia_en_metros = reader.GetDecimal(11);
                                RegistroDeAlarma.alarma_id = reader.GetInt64(12);
                                RegistroDeAlarma.fecha_alarma = reader.GetDateTime(13);
                                RegistroDeAlarma.descripciontipoalarma = reader[14].ToString();
                                RegistroDeAlarma.tipoalarma_id = reader.GetInt16(15);
                                RegistroDeAlarma.TiempoRefrescoUbicacion = reader.GetInt16(16);
                                RegistroDeAlarma.flag_propietario_alarma = reader.GetBoolean(17);
                                RegistroDeAlarma.calificacion_actual_alarma = reader.GetDecimal(18);
                                RegistroDeAlarma.usuariocalificoalarma = reader.GetBoolean(19);
                                RegistroDeAlarma.calificacionalarmausuario = reader[20].ToString();
                                RegistroDeAlarma.EsAlarmaActiva = reader.GetBoolean(21);
                                RegistroDeAlarma.alarma_id_padre = reader.IsDBNull(22) ? (long?)null : reader.GetInt64(22);
                                RegistroDeAlarma.calificacion_alarma = reader.IsDBNull(23) ? (long?)null : reader.GetDecimal(23);

                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        RegistroDeAlarma.descripciontipoalarma = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeAlarma.descripciontipoalarma);
                                    }
                                    catch (Exception)
                                    {
                                        RegistroDeAlarma.descripciontipoalarma = reader[14].ToString();
                                     }

                                }

                                LstAlarmasCercanas.Add(RegistroDeAlarma);
                            }

                        }
                        reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                        try
                        {
                            foreach (AlarmaCercanasResponse mensaje in LstAlarmasCercanas)
                            {
                                string updateSql = $"INSERT INTO mensajes_a_usuarios (persona_id,texto,fecha_mensaje,estado,asunto,idioma_origen,alarma_id)\r\nselect \r\nderiv.persona_id\r\n,'Llegaste a zona con una alerta cerca de ti, puedes verla aquí: ' as texto\r\n,now() as fecha_mensaje\r\n,cast(True as boolean) as estado\r\n,'Notificación Alerta en zona' as asunto\r\n,'es' as idioma_origen\r\n,deriv.alarma_id\r\nfrom (\r\nselect v.persona_id\r\n,v.alarma_id\r\nFROM vw_busca_alarmas_por_zona v \r\nWHERE v.user_id_thirdparty = \'{model.p_user_id_thirdparty}\'\r\nand v.alarma_id not in (select coalesce(m.alarma_id,-1) from mensajes_a_usuarios m where v.persona_id=m.persona_id group by m.alarma_id  )\r\ngroup by v.persona_id, v.alarma_id) as deriv;\r\n";
                                using (NpgsqlCommand updateCommand = new NpgsqlCommand(updateSql, connection))
                                {
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            var logData = new
                            {
                                timestamp = DateTime.UtcNow,
                                endpoint = "ubicaciones/InsertaUbicacion",
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
                            responseMessage.Message = "Ocurrió un error en el proceso de envio de mensaje a mensajes_a_usuarios ";
                            return BadRequest(responseMessage);
                        }
                        // Actualizar los mensajes en la base de datos en un segundo bucle

                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = LstAlarmasCercanas;
                        responseMessage.Message = "Insercion realizada. Alarmas consultadas";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "Fallo en el upsert de ubicacion del usuario.";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "ubicaciones/InsertaUbicacion",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de actualizacion de ubicacion de usuario. Codigo: 2 ";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible registrar la ubicación, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("ActualizaUbicacionMapa")]
        public async Task<IActionResult> ActualizaUbicacionMapa([FromBody] UbicacionesMapa model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<AlarmasEnMapaResponse> LstAlarmasCercanas = new List<AlarmasEnMapaResponse>();
                    string sql2 = $"SELECT  latitud_alarma\r\n       ,longitud_alarma\r\n       ,tipoalarma_id\r\n       ,alarma_id\r\n       ,fecha_alarma\r\n       ,descripciontipoalarma\r\n\t   ,radio_double\r\nFROM vw_busca_alarmas_sin_ubicacion_por_zona\r\nWHERE {model.latitud} between latitud_alarma-radio_double and latitud_alarma+radio_double\r\nand {model.longitud} between longitud_alarma-radio_double\tand longitud_alarma+radio_double\r\n;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            var v_idioma_origen = "es";
                            var v_idioma_destino = model.Idioma;

                            AlarmasEnMapaResponse RegistroDeAlarma = new AlarmasEnMapaResponse();

                            RegistroDeAlarma.latitud_alarma = reader.GetDecimal(0);
                            RegistroDeAlarma.longitud_alarma = reader.GetDecimal(1);
                            RegistroDeAlarma.tipoalarma_id = reader.GetInt16(2);
                            RegistroDeAlarma.alarma_id = reader.GetInt64(3);
                            RegistroDeAlarma.fecha_alarma = reader.GetDateTime(4);
                            RegistroDeAlarma.descripciontipoalarma = reader[5].ToString();

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    RegistroDeAlarma.descripciontipoalarma = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeAlarma.descripciontipoalarma);
                                }
                                catch (Exception)
                                {
                                    RegistroDeAlarma.descripciontipoalarma = reader[5].ToString(); 
                                }

                            }

                            LstAlarmasCercanas.Add(RegistroDeAlarma);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = LstAlarmasCercanas;
                    responseMessage.Message = "Alarmas consultadas en la zona solicitada";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "ubicaciones/ActualizaUbicacionMapa",
                        method = "POST",
                        request_body = model,
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de alarmas en esta zona del mapa. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar alarmas en esta zona, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}
