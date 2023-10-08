using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.NotificationHubs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SospectAPI.Data;
using SospectAPI.Data.Entities;
using SospectAPI.Data.ObjetosConsulta;
using SospectAPI.Helpers;
using SospectAPI.Models;
using SospectAPI.Services;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;

namespace SospectAPI.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class AlarmaController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        ResponseMessage responseMessage = new ResponseMessage();
        INotificationService _NotificationHubService;


        public AlarmaController(IConfiguration configuration, INotificationService NotificationHubService, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _NotificationHubService = NotificationHubService;
            _traductorService = traductorService;
            _s3 = s3;
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("InsertarAlarma")]
        public async Task<IActionResult> InsertarAlarma([FromBody] InsertaAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {

                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    using (NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();

                        var command = new NpgsqlCommand("SELECT * FROM public.funcInsertaAlarmaYNotifica(:p_user_id_thirdparty, :p_tipoalarma_id, :p_latitud, :p_longitud, :p_IpUsuario, :p_AlarmaId);", connection);

                        command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                        command.Parameters.AddWithValue(":p_tipoalarma_id", NpgsqlDbType.Integer, model.p_tipoalarma_id);
                        command.Parameters.AddWithValue(":p_latitud", NpgsqlDbType.Numeric, model.p_latitud);
                        command.Parameters.AddWithValue(":p_longitud", NpgsqlDbType.Numeric, model.p_longitud);
                        command.Parameters.AddWithValue(":p_IpUsuario", NpgsqlDbType.Varchar, model.ip_usuario ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue(":p_AlarmaId", NpgsqlDbType.Bigint, model.p_alarma_id.HasValue ? (object)model.p_alarma_id.Value : DBNull.Value);

                        List<UsuariosCercanosResponse> usuariosParaNotificar = new List<UsuariosCercanosResponse>();

                        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {

                                UsuariosCercanosResponse usuario = new UsuariosCercanosResponse
                                {
                                    user_id_thirdparty = reader["user_id_thirdparty"].ToString(),
                                    persona_id = (long)reader["persona_id"],
                                    alarma_id = (long)reader["alarma_id"],
                                    latitud_alarma = (decimal)reader["latitud_alarma"],
                                    longitud_alarma = (decimal)reader["longitud_alarma"],
                                    txt_notif = reader["txt_notif"].ToString(),
                                    idioma_destino = string.IsNullOrEmpty(reader["idioma_destino"].ToString()) ? model.idioma_dispositivo : reader["idioma_destino"].ToString(),
                                    txt_notif_original = reader["txt_notif"].ToString()

                                };

                                usuariosParaNotificar.Add(usuario);

                            }
                        }

                        Task.Run(async () =>
                        {
                            foreach (var UsuarioNotificar in usuariosParaNotificar)
                            {
                                var v_idioma_origen = "es";
                                var v_idioma_destino = UsuarioNotificar.idioma_destino;

                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        UsuarioNotificar.txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, UsuarioNotificar.txt_notif);
                                    }
                                    catch (Exception)
                                    {
                                        UsuarioNotificar.txt_notif = UsuarioNotificar.txt_notif_original;
                                    }
                                }

                                NotificationRequest notificationRequest = new NotificationRequest()
                                {
                                    Action = "A",
                                    Silent = false,
                                    Tags = new string[1] { UsuarioNotificar.user_id_thirdparty },
                                    Text = UsuarioNotificar.txt_notif,
                                    userThirdParty = UsuarioNotificar.user_id_thirdparty,
                                    alarma_id = UsuarioNotificar.alarma_id
                                };

                                _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);
                            }
                        });

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = "";
                    responseMessage.Message = "Insertion done. notifications sent";
                    return Ok(responseMessage);
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/InsertarAlarma",
                        method = "POST",
                        request_body = model,
                        response_body = responseMessage,
                        db_response = -1,
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
                    responseMessage.Message = "An error occurred while inserting the alarm. Code: 2";
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm model. Code: 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("CalificarAlarma")]
        public async Task<IActionResult> CalificarAlarma([FromBody] CalificarAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                string result = "";
                NpgsqlConnection connection = null;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("SELECT public.CalificarAlarma(:p_user_id_thirdparty, :p_alarma_id, :p_VeracidadAlarma);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.alarma_id);
                    command.Parameters.AddWithValue(":p_VeracidadAlarma", NpgsqlDbType.Boolean, model.VeracidadAlarma);


                    result = (string)await command.ExecuteScalarAsync();

                    if (result == "Success")
                    {
                        connection.Close();
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = "Insertion done";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = result;
                        responseMessage.Message = "An error occurred while qualifying the alarm Code 1";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/CalificarAlarma",
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
                    responseMessage.Message = "An error occurred while qualifying the alarm Code 2";
                    return BadRequest(responseMessage);
                }
                finally
                {
                    connection?.Close();
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm qualification model Code 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("CerrarAlarma")]
        public async Task<IActionResult> CerrarAlarma([FromBody] CerrarAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                NpgsqlConnection connection = null;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.cierrealarma(:p_alarma_id,:p_user_id_thirdparty,:p_descripcion_cierre,:p_flag_es_falsaalarma,:p_flag_hubo_captura,:p_idioma);", connection);

                    command.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.p_alarma_id);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_descripcion_cierre", NpgsqlDbType.Varchar, model.p_descripcion_cierre);
                    command.Parameters.AddWithValue(":p_flag_es_falsaalarma", NpgsqlDbType.Boolean, model.p_flag_es_falsaalarma);
                    command.Parameters.AddWithValue(":p_flag_hubo_captura", NpgsqlDbType.Boolean, model.p_flag_hubo_captura);
                    command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.p_idioma);


                    result = await command.ExecuteNonQueryAsync();

                    if (result < 0)
                    {
                        connection.Close();
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = "Insertion done";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = result;
                        responseMessage.Message = "An error occurred while closing the alarm Code 1";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/CerrarAlarma",
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
                    responseMessage.Message = ex.Message;
                    return BadRequest(responseMessage);
                }
                finally
                {
                    connection?.Close();
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm closing model Code 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("AsignarAlarma")]
        public async Task<IActionResult> AsignarAlarma([FromBody] AsignarAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    using (NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                    {
                        await connection.OpenAsync();

                        var command = new NpgsqlCommand("SELECT * FROM  public.fn_asignaralarma(:p_alarma_id,:p_user_id_thirdparty,:p_idioma);", connection);

                        command.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.p_alarma_id);
                        command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                        command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.p_idioma);
                        

                        List<AlarmaAsignadaResponse> usuariosParaNotificar = new List<AlarmaAsignadaResponse>();

                        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {

                                AlarmaAsignadaResponse usuario = new AlarmaAsignadaResponse
                                {
                                    user_id_thirdparty = reader["v_user_id_thirdparty_creador_out"].ToString(),
                                    alarma_id = (long)reader["alarma_id_out"],
                                    txt_notif = reader["txt_notif"].ToString(),
                                    idioma_destino = reader["idioma_destino"].ToString(),
                                    txt_notif_original = reader["txt_notif"].ToString()
                                };

                                usuariosParaNotificar.Add(usuario);

                            }
                        }

                        Task.Run(async () =>
                        {
                            foreach (var UsuarioNotificar in usuariosParaNotificar)
                            {
                                var v_idioma_origen = "es";
                                var v_idioma_destino = UsuarioNotificar.idioma_destino;

                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {
                                        UsuarioNotificar.txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, UsuarioNotificar.txt_notif);
                                    }
                                    catch (Exception)
                                    {
                                        UsuarioNotificar.txt_notif = UsuarioNotificar.txt_notif_original;
                                    }
                                }

                                NotificationRequest notificationRequest = new NotificationRequest()
                                {
                                    Action = "A",
                                    Silent = false,
                                    Tags = new string[1] { UsuarioNotificar.user_id_thirdparty },
                                    Text = UsuarioNotificar.txt_notif,
                                    userThirdParty = UsuarioNotificar.user_id_thirdparty,
                                    alarma_id = UsuarioNotificar.alarma_id
                                };

                                _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);
                            }
                        });

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = "";
                    responseMessage.Message = "Insertion done. notifications sent";
                    return Ok(responseMessage);
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/AsignarAlarma",
                        method = "POST",
                        request_body = model,
                        response_body = responseMessage,
                        db_response = -1,
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
                    responseMessage.Message = "An error occurred while assigning the alarm Code 2";
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm assigning model Code 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("DescribirAlarma")]
        public async Task<IActionResult> DescribirAlarma([FromBody] DescribirAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_destino = model.idioma_descripcion;
                var v_idioma_origen = "es";
                var MensajeSalida = "";
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.DescribirAlarma(:p_user_id_thirdparty,:p_alarma_id,:p_DescripcionAlarma,:p_DescripcionSospechoso,:p_DescripcionVehiculo,:p_DescripcionArmas,:p_tipoalarma_id, :p_IpUsuario,:p_idioma);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.alarma_id);
                    command.Parameters.AddWithValue(":p_DescripcionAlarma", NpgsqlDbType.Varchar, model.DescripcionAlarma ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionSospechoso", NpgsqlDbType.Varchar, model.DescripcionSospechoso ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionVehiculo", NpgsqlDbType.Varchar, model.DescripcionVehiculo ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionArmas", NpgsqlDbType.Varchar, model.DescripcionArmas ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_IpUsuario", NpgsqlDbType.Varchar, model.ip_usuario ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_tipoalarma_id", NpgsqlDbType.Integer, model.p_tipoalarma_id ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.idioma_descripcion ?? (object)DBNull.Value);


                    result = await command.ExecuteNonQueryAsync();

                    if (model.latitud_escape != null && model.longitud_escape != null)
                    {
                        int result2 = -1;
                        try
                        {
                            var command2 = new NpgsqlCommand("CALL public.InsertaAlarma(:p_user_id_thirdparty,:p_tipoalarma_id,:p_latitud,:p_longitud,:p_IpUsuario,:p_alarma_id);", connection);

                            command2.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                            command2.Parameters.AddWithValue(":p_tipoalarma_id", NpgsqlDbType.Integer, 9);
                            command2.Parameters.AddWithValue(":p_latitud", NpgsqlDbType.Numeric, model.latitud_escape);
                            command2.Parameters.AddWithValue(":p_longitud", NpgsqlDbType.Numeric, model.longitud_escape);
                            command2.Parameters.AddWithValue(":p_IpUsuario", NpgsqlDbType.Varchar, model.ip_usuario);
                            command2.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.alarma_id);

                            result2 = await command2.ExecuteNonQueryAsync();

                            if (result2 < 0)
                            {
                                List<UsuariosCercanosResponse> LstUsuariosCercanos = new List<UsuariosCercanosResponse>();
                                string sql = $"SELECT  \r\nuser_id_thirdparty\r\n,alarma_id\r\n,latitud_alarma\r\n,longitud_alarma\r\n,txt_notif\r\n,idioma_destino\r\n  FROM vw_notificacion_alarmas\r\nWHERE latitud_alarma = {model.latitud_escape}\r\nAND longitud_alarma = {model.longitud_escape}\r\nAND user_id_creador_alarma = \'{model.p_user_id_thirdparty}\';";

                                using (NpgsqlCommand command3 = new NpgsqlCommand(sql, connection))
                                {
                                    //string val;
                                    NpgsqlDataReader reader = command3.ExecuteReader();

                                    while (reader.Read())
                                    {

                                        UsuariosCercanosResponse UsuarioNotificar = new UsuariosCercanosResponse();

                                        UsuarioNotificar.user_id_thirdparty = reader[0].ToString();
                                        UsuarioNotificar.alarma_id = reader.GetInt64(1);
                                        UsuarioNotificar.latitud_alarma = reader.GetDecimal(2);
                                        UsuarioNotificar.longitud_alarma = reader.GetDecimal(3);
                                        UsuarioNotificar.txt_notif = reader[4].ToString();

                                        if (v_idioma_origen != v_idioma_destino)
                                        {
                                            try
                                            {
                                                UsuarioNotificar.txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, UsuarioNotificar.txt_notif);
                                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Insercion realizada. Notificaciones enviadas");
                                            }
                                            catch (Exception)
                                            {
                                                UsuarioNotificar.txt_notif = reader[4].ToString();
                                                MensajeSalida = "Insertion done. notifications sent";
                                            }

                                        }

                                        LstUsuariosCercanos.Add(UsuarioNotificar);

                                        NotificationRequest notificationRequest = new NotificationRequest()
                                        {
                                            Action = "A",
                                            Silent = false,
                                            Tags = new string[1] { UsuarioNotificar.user_id_thirdparty },
                                            Text = UsuarioNotificar.txt_notif,
                                            userThirdParty = UsuarioNotificar.user_id_thirdparty,
                                            alarma_id = UsuarioNotificar.alarma_id,
                                            latitud_alarma = UsuarioNotificar.latitud_alarma,
                                            longitud_alarma = UsuarioNotificar.longitud_alarma

                                        };

                                        await _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);
                                    }


                                    connection.Close(); //close the current connection
                                    responseMessage.IsSuccess = true;
                                    responseMessage.Data = "";
                                    responseMessage.Message = MensajeSalida;
                                    return Ok(responseMessage);
                                }
                            }
                            else
                            {
                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {

                                        MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error al insertar la alarma. Codigo: 1");
                                    }
                                    catch (Exception)
                                    {

                                        MensajeSalida = "An error occurred while inserting the alarm. Code: 1";
                                    }

                                }
                                connection.Close();
                                responseMessage.IsSuccess = false;
                                responseMessage.Data = "";
                                responseMessage.Message = MensajeSalida;
                                return BadRequest(responseMessage);
                            }

                        }
                        catch (Exception ex)
                        {
                            var logData = new
                            {
                                timestamp = DateTime.UtcNow,
                                endpoint = "alarma/DescribirAlarma-CrimenCometido",
                                method = "POST",
                                request_body = model,
                                response_body = responseMessage,
                                db_response = result2,
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
                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                                }
                                catch (Exception)
                                {

                                    MensajeSalida = "An error occurred while describing the alarm. Code: 2";
                                }

                            }
                            responseMessage.IsSuccess = false;
                            responseMessage.Data = MensajeSalida;
                            responseMessage.Message = MensajeSalida;
                            return BadRequest(responseMessage);
                        }


                    }

                    if (result < 0)
                    {
                        if (v_idioma_origen != v_idioma_destino)
                        {
                            try
                            {

                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Insercion de descripcion de alarma realizada.");
                            }
                            catch (Exception)
                            {

                                MensajeSalida = "Insertion of alarm description carried out.";
                            }

                        }
                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = MensajeSalida;
                        return Ok(responseMessage);
                    }
                    else
                    {
                        if (v_idioma_origen != v_idioma_destino)
                        {
                            try
                            {

                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error al insertar la descripcion de la alarma. Codigo: 1");
                            }
                            catch (Exception)
                            {

                                MensajeSalida = "An error occurred while inserting the alarm description. Code: 1";
                            }

                        }
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = MensajeSalida;
                        return BadRequest(responseMessage);
                    }




                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/DescribirAlarma",
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
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {

                            MensajeSalida = "An error occurred while describing the alarm. Code: 2";
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = MensajeSalida;
                    responseMessage.Message = MensajeSalida;
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm description model. Code: 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("TraerAlarma")]
        public async Task<IActionResult> TraerAlarma(long alarma_id, string idioma_dispositivo, string user_id_thirdparty)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var v_idioma_origen = "es";
                    var v_idioma_destino = idioma_dispositivo;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();


                    List<AlarmaCercanasResponse> LstAlarmasCercanas = new List<AlarmaCercanasResponse>();
                    NpgsqlDataReader reader;
                    string sql2 = $"SELECT  user_id_thirdparty \r\n,persona_id      \r\n,user_id_creador_alarma       \r\n,login_usuario_notificar       \r\n,latitud_alarma       \r\n,longitud_alarma       \r\n,latitud_entrada       \r\n,longitud_entrada       \r\n,coalesce(tipo_subscr_activa_usuario,'Ninguna')       \r\n,coalesce(cast(fecha_activacion_subscr as timestamp with time zone),cast(now() as timestamp with time zone))    \r\n,coalesce(cast(fecha_finalizacion_subscr as timestamp with time zone),cast(now() as timestamp with time zone))       \r\n,distancia_en_metros    \r\n,alarma_id    \r\n,fecha_alarma    \r\n,descripciontipoalarma    \r\n,tipoalarma_id    \r\n,TiempoRefrescoUbicacion    \r\n,case when user_id_thirdparty=\'{user_id_thirdparty}\' then cast(true as boolean) else cast(false as boolean) end flag_propietario_alarma   \r\n,calificacion_actual_alarma    \r\n,UsuarioCalificoAlarma    \r\n,CalificacionAlarmaUsuario \r\n,EsAlarmaActiva\r\n,calificacion_alarma\r\n,estado_alarma\n,Flag_hubo_captura\n,flag_alarma_siendo_atendida\n,cantidad_agentes_atendiendo\n,cantidad_interacciones\n, (select flag_es_policia from personas where user_id_thirdparty=\'{user_id_thirdparty}\') \n FROM vw_busca_alarma_por_id \r\nWHERE alarma_id = {alarma_id}\r\norder by fecha_alarma DESC;\r\n";
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
                            RegistroDeAlarma.calificacion_alarma = reader.IsDBNull(22) ? (long?)null : reader.GetDecimal(22);
                            RegistroDeAlarma.estado_alarma = reader.GetBoolean(23);
                            RegistroDeAlarma.Flag_hubo_captura = reader.GetBoolean(24);
                            RegistroDeAlarma.flag_alarma_siendo_atendida = reader.GetBoolean(25);
                            RegistroDeAlarma.cantidad_agentes_atendiendo = reader.GetInt32(26);
                            RegistroDeAlarma.cantidad_interacciones = reader.GetInt32(27);
                            RegistroDeAlarma.flag_es_policia = reader.GetBoolean(28);

                            LstAlarmasCercanas.Add(RegistroDeAlarma);
                        }

                    }
                    reader.Close(); // Cierra el lector antes de ejecutar otro comando en la conexión

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = LstAlarmasCercanas;
                    responseMessage.Message = "Descripciones de tipo de alarmas consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/TraerAlarma",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de descripciones de tipo de alarma";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de descripciones de tipo de alarma, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarTiposAlarma")]
        public async Task<IActionResult> ListarTiposAlarma(string idioma)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<DescripcionesTipoAlarmaResponse> ListaTipoAlarma = new List<DescripcionesTipoAlarmaResponse>();
                    string sql2 = $"select  \r\nta.tipoalarma_id\r\n,ta.descripciontipoalarma\r\n,ta.icono\r\nfrom tipoalarma ta\r\nwhere tipoalarma_id<>9\r\norder by ta.tipoalarma_id asc;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            DescripcionesTipoAlarmaResponse TipoAlarma = new DescripcionesTipoAlarmaResponse();

                            TipoAlarma.tipoalarma_id = reader.GetInt32(0);
                            TipoAlarma.descripciontipoalarma = reader[1].ToString();
                            TipoAlarma.icono = reader[2].ToString();

                            ListaTipoAlarma.Add(TipoAlarma);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaTipoAlarma;
                    responseMessage.Message = "Descripciones de tipo de alarmas consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/ListarTiposAlarma",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de descripciones de tipo de alarma";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de descripciones de tipo de alarma, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarDescripcionesAlarma")]
        public async Task<IActionResult> ListarDescripcionesAlarma(long alarma_id, string user_id_thirdparty, string idioma_dispositivo)
        {
            var v_idioma_destino = idioma_dispositivo;
            var v_idioma_origen = "es";
            var MensajeSalida = "";

            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    NpgsqlDataReader reader;

                    List<DescripcionesAlarmasResponse> ListaDescripcionesAlarmas = new List<DescripcionesAlarmasResponse>();

                    string sql2 = @"SELECT 
                                    iddescripcion,
                                    alarma_id,
                                    persona_id,
                                    descripcionalarma,
                                    descripcionsospechoso,
                                    descripcionvehiculo,
                                    descripcionarmas,
                                    FlagEditado,
                                    fechadescripcion,
                                    propietario_descripcion,
                                    calificacion_otras_descripciones,
                                    calificaciondescripcion,
                                    tipoalarma_id,
                                    descripciontipoalarma,
                                    idioma_origen,
                                    EsAlarmaActiva
                                FROM public.fn_ListarDescripcionesAlarma(:p_alarma_id, :p_user_id_thirdparty)
                                ORDER BY FECHADESCRIPCION ASC;";

                    NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection);
                    command3.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, alarma_id);
                    command3.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, user_id_thirdparty);
                    using (reader = command3.ExecuteReader())
                    {

                        while (reader.Read())
                        {

                            DescripcionesAlarmasResponse RegistroDeDescripciones = new DescripcionesAlarmasResponse();

                            RegistroDeDescripciones.iddescripcion = reader.GetInt64(0);
                            RegistroDeDescripciones.alarma_id = reader.GetInt64(1);
                            RegistroDeDescripciones.persona_id = reader.GetInt64(2);
                            RegistroDeDescripciones.descripcionalarma = reader[3].ToString();
                            RegistroDeDescripciones.descripcionsospechoso = reader[4].ToString();
                            RegistroDeDescripciones.descripcionvehiculo = reader[5].ToString();
                            RegistroDeDescripciones.descripcionarmas = reader[6].ToString();
                            RegistroDeDescripciones.FlagEditado = reader.GetBoolean(7);
                            RegistroDeDescripciones.fechadescripcion = reader.GetDateTime(8);
                            RegistroDeDescripciones.propietario_descripcion = reader.GetBoolean(9);
                            RegistroDeDescripciones.calificacion_otras_descripciones = reader[10].ToString();
                            RegistroDeDescripciones.calificaciondescripcion = reader.GetInt16(11);
                            RegistroDeDescripciones.tipoalarma_id = reader.GetInt16(12);
                            RegistroDeDescripciones.descripciontipoalarma = reader[13].ToString();
                            v_idioma_origen = reader[14].ToString();
                            RegistroDeDescripciones.EsAlarmaActiva = reader.GetBoolean(15);

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    RegistroDeDescripciones.descripcionalarma = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeDescripciones.descripcionalarma);
                                    RegistroDeDescripciones.descripcionsospechoso = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeDescripciones.descripcionsospechoso);
                                    RegistroDeDescripciones.descripcionvehiculo = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeDescripciones.descripcionvehiculo);
                                    RegistroDeDescripciones.descripcionarmas = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeDescripciones.descripcionarmas);
                                    RegistroDeDescripciones.descripciontipoalarma = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, RegistroDeDescripciones.descripciontipoalarma);
                                    MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Descripciones de alarmas consultadas.");
                                }
                                catch (Exception)
                                {
                                    RegistroDeDescripciones.descripcionalarma = reader[3].ToString();
                                    RegistroDeDescripciones.descripcionsospechoso = reader[4].ToString();
                                    RegistroDeDescripciones.descripcionvehiculo = reader[5].ToString();
                                    RegistroDeDescripciones.descripcionarmas = reader[6].ToString();
                                    RegistroDeDescripciones.descripciontipoalarma = reader[13].ToString();
                                    MensajeSalida = "Descriptions of alarms consulted.";
                                }

                            }

                            ListaDescripcionesAlarmas.Add(RegistroDeDescripciones);
                        }

                    }

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaDescripcionesAlarmas;
                    responseMessage.Message = MensajeSalida;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/ListarDescripcionesAlarma",
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
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {

                            MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error en el proceso de consulta de descripciones para la alarma seleccionada.");
                        }
                        catch (Exception)
                        {

                            MensajeSalida = "An error occurred in the description query process for the selected alarm.";
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = ex.Message;
                    responseMessage.Message = MensajeSalida;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                if (v_idioma_origen != v_idioma_destino)
                {
                    try
                    {

                        MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "No fue posible consultar la lista de descripciones de la alarma solicitada, el modelo no es válido.");
                    }
                    catch (Exception)
                    {

                        MensajeSalida = "It was not possible to consult the list of descriptions of the requested alarm, the model is invalid.";
                    }

                }
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = MensajeSalida;
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("ActualizarDescripcionAlarma")]
        public async Task<IActionResult> ActualizarDescripcionAlarma([FromBody] DescribirAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_destino = model.idioma_descripcion;
                var v_idioma_origen = "es";
                var MensajeSalida = "";
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.ActualizarDescripcionAlarma(:p_user_id_thirdparty,:p_alarma_id,:p_DescripcionAlarma,:p_DescripcionSospechoso,:p_DescripcionVehiculo,:p_DescripcionArmas,:p_tipoalarma_id,:p_IpUsuario,:p_idioma);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.alarma_id);
                    command.Parameters.AddWithValue(":p_DescripcionAlarma", NpgsqlDbType.Varchar, model.DescripcionAlarma ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionSospechoso", NpgsqlDbType.Varchar, model.DescripcionSospechoso ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionVehiculo", NpgsqlDbType.Varchar, model.DescripcionVehiculo ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_DescripcionArmas", NpgsqlDbType.Varchar, model.DescripcionArmas ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_IpUsuario", NpgsqlDbType.Varchar, model.ip_usuario ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_tipoalarma_id", NpgsqlDbType.Integer, model.p_tipoalarma_id ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.idioma_descripcion ?? (object)DBNull.Value);


                    result = await command.ExecuteNonQueryAsync();

                    if (model.latitud_escape != null && model.longitud_escape != null)
                    {
                        int result2 = -1;
                        try
                        {
                            var command2 = new NpgsqlCommand("CALL public.InsertaAlarma(:p_user_id_thirdparty,:p_tipoalarma_id,:p_latitud,:p_longitud,:p_IpUsuario,:p_alarma_id);", connection);

                            command2.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                            command2.Parameters.AddWithValue(":p_tipoalarma_id", NpgsqlDbType.Integer, 9);
                            command2.Parameters.AddWithValue(":p_latitud", NpgsqlDbType.Numeric, model.latitud_escape);
                            command2.Parameters.AddWithValue(":p_longitud", NpgsqlDbType.Numeric, model.longitud_escape);
                            command2.Parameters.AddWithValue(":p_IpUsuario", NpgsqlDbType.Varchar, model.ip_usuario);
                            command2.Parameters.AddWithValue(":p_alarma_id", NpgsqlDbType.Bigint, model.alarma_id);

                            result2 = await command2.ExecuteNonQueryAsync();

                            if (result2 < 0)
                            {
                                List<UsuariosCercanosResponse> LstUsuariosCercanos = new List<UsuariosCercanosResponse>();
                                string sql = $"SELECT  \r\nuser_id_thirdparty\r\n,alarma_id\r\n,latitud_alarma\r\n,longitud_alarma\r\n,txt_notif\r\n,idioma_destino\r\n  FROM vw_notificacion_alarmas\r\nWHERE latitud_alarma = {model.latitud_escape}\r\nAND longitud_alarma = {model.longitud_escape}\r\nAND user_id_creador_alarma = \'{model.p_user_id_thirdparty}\';";

                                using (NpgsqlCommand command3 = new NpgsqlCommand(sql, connection))
                                {
                                    //string val;
                                    NpgsqlDataReader reader = command3.ExecuteReader();

                                    while (reader.Read())
                                    {

                                        v_idioma_destino = reader[5].ToString();
                                        UsuariosCercanosResponse UsuarioNotificar = new UsuariosCercanosResponse();

                                        UsuarioNotificar.user_id_thirdparty = reader[0].ToString();
                                        UsuarioNotificar.alarma_id = reader.GetInt64(1);
                                        UsuarioNotificar.latitud_alarma = reader.GetDecimal(2);
                                        UsuarioNotificar.longitud_alarma = reader.GetDecimal(3);
                                        UsuarioNotificar.txt_notif = reader[4].ToString();

                                        if (v_idioma_origen != v_idioma_destino)
                                        {
                                            try
                                            {
                                                UsuarioNotificar.txt_notif = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, UsuarioNotificar.txt_notif);
                                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Insercion realizada. Notificaciones enviadas");
                                            }
                                            catch (Exception)
                                            {
                                                UsuarioNotificar.txt_notif = reader[4].ToString();
                                                MensajeSalida = "Insertion done. notifications sent.";
                                            }

                                        }

                                        LstUsuariosCercanos.Add(UsuarioNotificar);

                                        NotificationRequest notificationRequest = new NotificationRequest()
                                        {
                                            Action = "A",
                                            Silent = false,
                                            Tags = new string[1] { UsuarioNotificar.user_id_thirdparty },
                                            Text = UsuarioNotificar.txt_notif,
                                            userThirdParty = UsuarioNotificar.user_id_thirdparty,
                                            alarma_id = UsuarioNotificar.alarma_id,
                                            latitud_alarma = UsuarioNotificar.latitud_alarma,
                                            longitud_alarma = UsuarioNotificar.longitud_alarma

                                        };

                                        await _NotificationHubService.RequestNotificationAsync(notificationRequest, CancellationToken.None);
                                    }


                                    connection.Close(); //close the current connection
                                    responseMessage.IsSuccess = true;
                                    responseMessage.Data = "";
                                    responseMessage.Message = MensajeSalida;
                                    return Ok(responseMessage);
                                }
                            }
                            else
                            {
                                if (v_idioma_origen != v_idioma_destino)
                                {
                                    try
                                    {

                                        MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error al insertar la alarma. Codigo: 1");
                                    }
                                    catch (Exception)
                                    {

                                        MensajeSalida = "An error occurred while inserting the alarm. Code: 1";
                                    }

                                }
                                connection.Close();
                                responseMessage.IsSuccess = false;
                                responseMessage.Data = "";
                                responseMessage.Message = MensajeSalida;
                                return BadRequest(responseMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            var logData = new
                            {
                                timestamp = DateTime.UtcNow,
                                endpoint = "alarma/ActualizarDescripcionAlarma",
                                method = "POST",
                                request_body = model,
                                response_body = responseMessage,
                                db_response = result2,
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
                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {

                                    MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                                }
                                catch (Exception)
                                {

                                    MensajeSalida = "An error occurred while updating the alarm description. Code: 2";
                                }

                            }
                            responseMessage.IsSuccess = false;
                            responseMessage.Data = MensajeSalida;
                            responseMessage.Message = MensajeSalida;
                            return BadRequest(responseMessage);
                        }


                    }

                    if (result < 0)
                    {
                        if (v_idioma_origen != v_idioma_destino)
                        {
                            try
                            {

                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Actualizacion de descripcion de alarma realizada.");
                            }
                            catch (Exception)
                            {

                                MensajeSalida = "Alarm description update done.";
                            }

                        }
                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = MensajeSalida;
                        return Ok(responseMessage);
                    }
                    else
                    {
                        if (v_idioma_origen != v_idioma_destino)
                        {
                            try
                            {

                                MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error al actualizar la descripcion de la alarma. Codigo: 1");
                            }
                            catch (Exception)
                            {

                                MensajeSalida = "An error occurred while updating the alarm description. Code: 1";
                            }

                        }
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = MensajeSalida;
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/ActualizarDescripcionAlarma",
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
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {

                            MensajeSalida = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {

                            MensajeSalida = "An error occurred while updating the alarm description. Code: 2";
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = MensajeSalida;
                    responseMessage.Message = MensajeSalida;
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the alarm description model. Code: 3";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("CalificarDescripcionesAlarma")]
        public async Task<IActionResult> CalificarDescripcionesAlarma([FromBody] CalificarDescAlarmaRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.CalificarDescripcionesAlarma(:p_user_id_thirdparty,:p_iddescripcion,:p_CalificacionDescripcion);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.p_user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_iddescripcion", NpgsqlDbType.Bigint, model.iddescripcion);
                    command.Parameters.AddWithValue(":p_CalificacionDescripcion", NpgsqlDbType.Varchar, model.CalificacionDescripcion);



                    result = await command.ExecuteNonQueryAsync();

                    if (result < 0)
                    {
                        DescripcionesCalificacionResponse Calificacion = new DescripcionesCalificacionResponse();
                        string sql2 = $"select \r\niddescripcion\r\n,calificaciondescripcion\r\nfrom vw_ListarDescripcionesAlarma\r\nwhere iddescripcion={model.iddescripcion}\r\nand user_id_thirdparty_calificador=\'{model.p_user_id_thirdparty}\';";
                        using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                        {
                            //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                            NpgsqlDataReader reader = command3.ExecuteReader();

                            while (reader.Read())
                            {
                                Calificacion.iddescripcion = reader.GetInt32(0);
                                Calificacion.calificaciondescripcion = reader.GetInt16(1);

                            }

                        }

                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = Calificacion;
                        responseMessage.Message = "Calificacion de descripcion de alarma realizada.";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "Ocurrio un error al calificar la descripcion de la alarma. Codigo: 1";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "alarma/CalificarDescripcionesAlarma",
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
                    responseMessage.Message = ex.Message;
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "Ocurrio un error con el modelo de calificacion de la alarma.  Codigo: 3";
                return BadRequest(responseMessage);
            }

        }

    }
}
