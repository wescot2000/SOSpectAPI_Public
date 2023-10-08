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
    public class MensajesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        INotificationService _NotificationHubService;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public MensajesController(IConfiguration configuration, INotificationService NotificationHubService,DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _NotificationHubService = NotificationHubService;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarMensajes")]
        public async Task<IActionResult> ListarMensajes(string user_id_thirdParty, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                
                try
                {
                    var v_idioma_origen = "";
                    var v_idioma_destino = string.IsNullOrEmpty(idioma_dispositivo) ? "en" : idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ListaMensajesResponse> ListaMensajes = new List<ListaMensajesResponse>();
                    List<ListaMensajesResponse> MensajesParaActualizar = new List<ListaMensajesResponse>();
                    NpgsqlDataReader reader;

                    string sql2 = $"select \r\nmensaje_id\r\n,asunto\r\n,estado\r\n,fecha_mensaje\r\n,idioma_origen\r\n,texto\r\n from vw_listar_mensajes\r\nwhere user_id_thirdparty=\'{user_id_thirdParty}\' order by fecha_mensaje desc;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaMensajesResponse Mensajes = new ListaMensajesResponse();

                            Mensajes.mensaje_id = reader.GetInt64(0);
                            Mensajes.asunto = reader[1].ToString();
                            Mensajes.estado = reader.GetBoolean(2);
                            Mensajes.fecha_mensaje = reader.GetDateTime(3);
                            Mensajes.idioma_origen = reader[4].ToString();
                            Mensajes.texto = reader[5].ToString();
                            v_idioma_origen = Mensajes.idioma_origen;

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    Mensajes.asunto = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Mensajes.asunto);
                                    Mensajes.texto = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Mensajes.texto);
                                    MensajesParaActualizar.Add(Mensajes);
                                }
                                catch (Exception)
                                {
                                    Mensajes.asunto = reader[1].ToString();
                                }

                            }

                            ListaMensajes.Add(Mensajes);
                        }

                    }
                    reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                    try
                    {
                        foreach (ListaMensajesResponse mensaje in MensajesParaActualizar)
                        {
                            string updateSql = "UPDATE public.mensajes_a_usuarios SET asunto_traducido = @asunto, texto_traducido = @texto, idioma_post_traduccion = @idioma_destino, fecha_traduccion=now() WHERE mensaje_id = @mensaje_id";
                            using (NpgsqlCommand updateCommand = new NpgsqlCommand(updateSql, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@asunto", mensaje.asunto);
                                updateCommand.Parameters.AddWithValue("@texto", mensaje.texto);
                                updateCommand.Parameters.AddWithValue("@idioma_destino", v_idioma_destino);
                                updateCommand.Parameters.AddWithValue("@mensaje_id", mensaje.mensaje_id);

                                await updateCommand.ExecuteNonQueryAsync();
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        var logData = new
                        {
                            timestamp = DateTime.UtcNow,
                            endpoint = "Mensajes/ListarMensajes",
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
                        responseMessage.Message = "Ocurrió un error en el proceso de actualización de idioma a los mensajes";
                        return BadRequest(responseMessage);
                    }
                    // Actualizar los mensajes en la base de datos en un segundo bucle
                    

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaMensajes;
                    responseMessage.Message = "Lista de mensajes consultada exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Mensajes/ListarMensajes",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de lista de mensajes";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de mensajes, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("LeerMensaje")]
        public async Task<IActionResult> LeerMensaje([FromBody] LeerMensajeRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    var v_idioma_origen = "";
                    var v_idioma_destino = string.IsNullOrEmpty(model.idioma_dispositivo) ? "en" : model.idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    
                    var command = new NpgsqlCommand("CALL public.MarcaMensajeLeido(:p_user_id_thirdparty,:p_mensaje_id);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_mensaje_id", NpgsqlDbType.Bigint, model.p_mensaje_id);

                    result = await command.ExecuteNonQueryAsync();

                    
                    DetalleMensajesResponse Mensajes = new DetalleMensajesResponse();
                    string sql2 = $"select \r\nmensaje_id\r\n,asunto\r\n,estado\r\n,texto\r\n,para\r\n,remitente\r\n,fecha_mensaje\r\n,idioma_origen\r\n,alarma_id\r\nfrom vw_leer_mensaje\r\nwhere user_id_thirdparty=\'{model.p_user_id_thirdparty}\'\r\nand mensaje_id={model.p_mensaje_id};";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            

                            Mensajes.mensaje_id = reader.GetInt64(0);
                            Mensajes.asunto = reader[1].ToString();
                            Mensajes.estado = reader.GetBoolean(2);
                            Mensajes.texto = reader[3].ToString();
                            Mensajes.para = reader[4].ToString();
                            Mensajes.remitente = reader[5].ToString();
                            Mensajes.fecha_mensaje = reader.GetDateTime(6);
                            Mensajes.idioma_origen = reader[7].ToString();
                            Mensajes.alarma_id = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8);
                            v_idioma_origen = Mensajes.idioma_origen;

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    Mensajes.asunto = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Mensajes.asunto);
                                    Mensajes.texto = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Mensajes.texto);
                                }
                                catch (Exception)
                                {
                                    Mensajes.asunto = reader[1].ToString();
                                    Mensajes.texto = reader[3].ToString();
                                }

                            }

                            
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Mensajes;
                    responseMessage.Message = "Mensajes consultados exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Mensajes/LeerMensaje",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de mensajes";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar los mensajes, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("EnviarMensajeUsuarios")]
        public async Task<IActionResult> EnviarMensajeUsuarios([FromBody] EnvioMsgNotifRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.EnviarMensaje(:p_asunto,:p_mensaje);", connection);

                    command.Parameters.AddWithValue(":p_asunto", NpgsqlDbType.Varchar, model.p_asunto);
                    command.Parameters.AddWithValue(":p_mensaje", NpgsqlDbType.Varchar, model.p_mensaje);

                    result = await command.ExecuteNonQueryAsync();

                    if (result < 0)
                    {
                        List<UsuariosTotalesResponse> LstUsuariosCercanos = new List<UsuariosTotalesResponse>();
                        string sql = $"select p.user_id_thirdparty, coalesce(d.idioma,'en') from personas p\ninner join dispositivos d \non d.persona_id=p.persona_id and d.fecha_fin is null and p.user_id_thirdparty is not null;";

                        using (NpgsqlCommand command2 = new NpgsqlCommand(sql, connection))
                        {
                            //string val;
                            NpgsqlDataReader reader = command2.ExecuteReader();

                            while (reader.Read())
                            {
                                var v_idioma_origen = "es";
                                var v_idioma_destino = "";

                                UsuariosTotalesResponse UsuarioNotificar = new UsuariosTotalesResponse();

                                UsuarioNotificar.user_id_thirdparty = reader[0].ToString();
                                v_idioma_destino = reader[1].ToString();
                                UsuarioNotificar.txt_notif = model.p_textoNotificacion;

                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        UsuarioNotificar.txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, UsuarioNotificar.txt_notif);
                                    }
                                    catch (Exception)
                                    {
                                        UsuarioNotificar.txt_notif = model.p_textoNotificacion;
                                    }

                                }

                                LstUsuariosCercanos.Add(UsuarioNotificar);

                                NotificationRequest notificationRequest = new NotificationRequest()
                                {
                                    Action = "A",
                                    Silent = false,
                                    Tags = new string[1] { UsuarioNotificar.user_id_thirdparty },
                                    Text = UsuarioNotificar.txt_notif
                                };

                                await _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);
                            }

                            reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                            connection.Close(); //close the current connection
                            responseMessage.IsSuccess = true;
                            responseMessage.Data = "";
                            responseMessage.Message = "Notifications sent";
                            return Ok(responseMessage);
                        }
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "An error occurred while sending notifications. Code: 1";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Mensajes/EnviarMensajeUsuarios",
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
                    responseMessage.Message = "An error occurred while sending notifications. Code: 2";
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the model while sending notifications. Code: 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("MarcarTodosComoLeidos")]
        public async Task<IActionResult> MarcarTodosComoLeidos([FromBody] MarcarMensajeLeidoRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    string updateCommand = "UPDATE public.mensajes_a_usuarios SET estado = FALSE WHERE persona_id IN (SELECT persona_id FROM personas WHERE user_id_thirdparty = :p_user_id_thirdparty)";

                    var command = new NpgsqlCommand(updateCommand, connection);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close();

                    responseMessage.IsSuccess = true;
                    responseMessage.Message = "Todos los mensajes se marcaron como leídos exitosamente.";
                    return Ok(responseMessage);
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Mensajes/MarcarTodosComoLeidos",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de marca de mensajes como leidos";
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "El modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

    }
}
