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
    public class SubscripcionesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public SubscripcionesController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarTiposRelaciones")]
        public async Task<IActionResult> ListarTiposRelaciones()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<DescripcionesTipoRelacionesResponse> ListaTipoRelacion = new List<DescripcionesTipoRelacionesResponse>();
                    string sql2 = $"select tiporelacion_id,descripciontiporel from tiporelacion order by tiporelacion_id asc;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            DescripcionesTipoRelacionesResponse TipoRelacion = new DescripcionesTipoRelacionesResponse();

                            TipoRelacion.tiporelacion_id = reader.GetInt32(0);
                            TipoRelacion.descripciontiporel = reader[1].ToString();

                            ListaTipoRelacion.Add(TipoRelacion);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaTipoRelacion;
                    responseMessage.Message = "Descripciones de tipo de relacion consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarTiposRelaciones",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de descripciones de tipo de relaciones";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de descripciones de tipo de relaciones, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarSolicitudesPendientes")]
        public async Task<IActionResult> ListarSolicitudesPendientes(string user_id_thirdparty)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<SolicitudesPendientesResponse> ListaSolicitudes = new List<SolicitudesPendientesResponse>();
                    string sql2 = $"SELECT \r\nuser_id_thirdparty\r\n,user_id_thirdparty_protector\r\n,login\r\n,fecha_solicitud\r\nfrom vw_solicitudes_pendientes_protegido\r\nwhere user_id_thirdparty=\'{user_id_thirdparty}\'\r\norder by fecha_solicitud asc;";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {
                            
                            SolicitudesPendientesResponse Solicitudes = new SolicitudesPendientesResponse();

                            Solicitudes.user_id_thirdparty = reader[0].ToString();
                            Solicitudes.user_id_thirdparty_protector = reader[1].ToString();
                            Solicitudes.login = reader[2].ToString();
                            Solicitudes.fecha_solicitud = reader.GetDateTime(3);

                            ListaSolicitudes.Add(Solicitudes);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaSolicitudes;
                    responseMessage.Message = "Solicitudes de aprobacion consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarSolicitudesPendientes",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de solicitudes de aprobacion";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista solicitudes de aprobacion, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarSolicitudesAprobadas")]
        public async Task<IActionResult> ListarSolicitudesAprobadas(string user_id_thirdparty)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<SolicitudesAprobadasResponse> ListaSolicitudes = new List<SolicitudesAprobadasResponse>();
                    string sql2 = $"SELECT \r\nuser_id_thirdparty_protector\r\n,user_id_thirdparty_protegido\r\n,login\r\n,fecha_aprobado\r\n,tiporelacion_id\r\nfrom vw_solicitudes_aprobadas_sin_subscripcion\r\nwhere user_id_thirdparty_protector=\'{user_id_thirdparty}\'\r\norder by fecha_aprobado asc;\r\n";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            SolicitudesAprobadasResponse Solicitudes = new SolicitudesAprobadasResponse();

                            Solicitudes.user_id_thirdparty_protector = reader[0].ToString();
                            Solicitudes.user_id_thirdparty_protegido = reader[1].ToString();
                            Solicitudes.login = reader[2].ToString();
                            Solicitudes.fecha_aprobado = reader.GetDateTime(3);
                            Solicitudes.tiporelacion_id = reader.GetInt32(4);

                            ListaSolicitudes.Add(Solicitudes);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaSolicitudes;
                    responseMessage.Message = "Solicitudes aprobadas consultadas.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarSolicitudesAprobadas",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de solicitudes aprobadas";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista solicitudes aprobadas, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarProtegidos")]
        public async Task<IActionResult> ListarProtegidos(string user_id_thirdParty_protector, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ListaProtegidosResponse> ListaProtegidos = new List<ListaProtegidosResponse>();
                    string sql2 = $"SELECT\r\nuser_id_thirdParty_protector\r\n,user_id_thirdParty_protegido\r\n,login_protector\r\n,login_protegido\r\n,fecha_activacion\r\n,fecha_finalizacion\r\nfrom vw_lista_protegidos\r\nwhere user_id_thirdParty_protector=\'{user_id_thirdParty_protector}\';";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaProtegidosResponse Protegido = new ListaProtegidosResponse();

                            Protegido.user_id_thirdParty_protector = reader[0].ToString();
                            Protegido.user_id_thirdParty_protegido = reader[1].ToString();
                            Protegido.login_protector = reader[2].ToString();
                            Protegido.login_protegido = reader[3].ToString();
                            Protegido.fecha_activacion = reader.GetDateTime(4);
                            Protegido.fecha_finalizacion = reader.GetDateTime(5);

                            ListaProtegidos.Add(Protegido);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaProtegidos;
                    responseMessage.Message = "Lista de protegidos consultada exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarProtegidos",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de lista de protegidos";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de protegidos, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarProtectores")]
        public async Task<IActionResult> ListarProtectores(string user_id_thirdParty_protegido, string idioma_dispositivo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<ListaProtegidosResponse> ListaProtectores = new List<ListaProtegidosResponse>();
                    string sql2 = $"SELECT\r\nuser_id_thirdParty_protector\r\n,user_id_thirdParty_protegido\r\n,login_protector\r\n,login_protegido\r\n,fecha_activacion\r\n,fecha_finalizacion\r\nfrom vw_lista_protegidos\r\nwhere user_id_thirdParty_protegido=\'{user_id_thirdParty_protegido}\';";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaProtegidosResponse Protector = new ListaProtegidosResponse();

                            Protector.user_id_thirdParty_protector = reader[0].ToString();
                            Protector.user_id_thirdParty_protegido = reader[1].ToString();
                            Protector.login_protector = reader[2].ToString();
                            Protector.login_protegido = reader[3].ToString();
                            Protector.fecha_activacion = reader.GetDateTime(4);
                            Protector.fecha_finalizacion = reader.GetDateTime(5);

                            ListaProtectores.Add(Protector);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaProtectores;
                    responseMessage.Message = "Lista de protegidos consultada exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarProtectores",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de lista de protegidos";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de protegidos, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarMisSubscripciones")]
        public async Task<IActionResult> ListarMisSubscripciones(string user_id_thirdParty_protector, string idioma_dispositivo)
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

                    List<ListaSubscripcionesResponse> ListaSubscripciones = new List<ListaSubscripcionesResponse>();
                    string sql2 = $"select \r\nsubscripcion_id\r\n,user_id_thirdparty\r\n,descripcion_tipo\r\n,descripcion\r\n,fecha_finalizacion\r\n,poderes_renovacion\r\n,flag_subscr_vencida\r\n,observ_subscripcion\r\n,flag_renovable\r\n,texto_renovable\r\nfrom public.vw_mis_subscripciones\r\nwhere user_id_thirdparty=\'{user_id_thirdParty_protector}\';";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            ListaSubscripcionesResponse Subscripciones = new ListaSubscripcionesResponse();

                            Subscripciones.subscripcion_id = reader.GetInt64(0);
                            Subscripciones.user_id_thirdparty = reader[1].ToString();
                            Subscripciones.descripcion_tipo = reader[2].ToString();
                            Subscripciones.descripcion = reader[3].ToString();
                            Subscripciones.fecha_finalizacion = reader.GetDateTime(4);
                            Subscripciones.poderes_renovacion = reader.GetInt32(5);
                            Subscripciones.flag_subscr_vencida = reader.GetBoolean(6);
                            Subscripciones.observ_subscripcion = reader[7].ToString();
                            Subscripciones.flag_renovable = reader.GetBoolean(8);
                            Subscripciones.texto_renovable = reader[9].ToString();

                            if (v_idioma_origen != v_idioma_destino)
                            {
                                try
                                {
                                    Subscripciones.descripcion = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Subscripciones.descripcion);
                                }
                                catch (Exception)
                                {
                                    Subscripciones.descripcion = reader[3].ToString();
                                }

                            }

                            ListaSubscripciones.Add(Subscripciones);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaSubscripciones;
                    responseMessage.Message = "Lista de subscripciones consultada exitosamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarMisSubscripciones",
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
                    responseMessage.Message = "Ocurrio un error en el proceso de consulta de lista de subscripciones";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la lista de subscripciones, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("RenovarSubscripcion")]
        public async Task<IActionResult> RenovarSubscripcion([FromBody] RenovarSubscripcionRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    
                    var command = new NpgsqlCommand("CALL public.RenovarSubscripcion(:p_subscripcion_id,:p_user_id_thirdparty_protector,:p_cantidad_poderes);", connection);

                    command.Parameters.AddWithValue(":p_subscripcion_id", NpgsqlDbType.Bigint, model.subscripcion_id);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_cantidad_poderes", NpgsqlDbType.Integer, model.cantidad_poderes);

                    result = await command.ExecuteNonQueryAsync();
                    if (result < 0)
                    {
                        
                        var Message = "Se ha renovado la subscripcion solicitada, puede ingresar nuevamente a ésta opción del menú para confirmar.";
                        if (v_idioma_origen != v_idioma_destino)
                        {

                            try
                            {
                                Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                            }
                            catch (Exception)
                            {
                                Message = Message;
                            }

                        }
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = Message;
                        responseMessage.Message = "Renovacion realizada con exito";
                        connection.Close(); //close the current connection
                        return Ok(responseMessage);
                    }
                    else
                    {
                        var Message = "No se logró realizar la renovación, intente ingresar nuevamente a ésta opción del menú para confirmar y reintentar.";
                        if (v_idioma_origen != v_idioma_destino)
                        {

                            try
                            {
                                Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                            }
                            catch (Exception)
                            {
                                Message = Message;
                            }

                        }
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = Message;
                        responseMessage.Message = "Ocurrio un error al renovar subscripción. Codigo: 1";
                        return BadRequest(responseMessage);

                    }
                    

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/RenovarSubscripcion",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Ocurrio un error al renovar subscripción. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible realizar la renovacion de la subscripción, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("CancelarSubscripcion")]
        public async Task<IActionResult> CancelarSubscripcion([FromBody] CancelarSubscripcionRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.CancelarSubscripcion(:p_subscripcion_id,:p_user_id_thirdparty_protector);", connection);

                    command.Parameters.AddWithValue(":p_subscripcion_id", NpgsqlDbType.Bigint, model.subscripcion_id);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.user_id_thirdparty);
                    
                    result = await command.ExecuteNonQueryAsync();
                    if (result < 0)
                    {

                        var Message = "Se ha cancelado la subscripcion solicitada, puede ingresar nuevamente a ésta opción del menú para confirmar.";
                        if (v_idioma_origen != v_idioma_destino)
                        {

                            try
                            {
                                Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                            }
                            catch (Exception)
                            {
                                Message = Message;
                            }

                        }
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = Message;
                        responseMessage.Message = "Cancelacion realizada con exito";
                        connection.Close(); //close the current connection
                        return Ok(responseMessage);
                    }
                    else
                    {
                        var Message = "No se logró realizar la cancelación, intente ingresar nuevamente a ésta opción del menú para confirmar y reintentar.";
                        if (v_idioma_origen != v_idioma_destino)
                        {

                            try
                            {
                                Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                            }
                            catch (Exception)
                            {
                                Message = Message;
                            }

                        }
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = Message;
                        responseMessage.Message = "Ocurrio un error al cancelar la subscripción. Codigo: 1";
                        return BadRequest(responseMessage);

                    }


                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/CancelarSubscripcion",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Ocurrio un error al cancelar la subscripción. Codigo: 2";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible realizar la cancelación de la subscripción, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("SolicitarPermisoAProtegido")]
        public async Task<IActionResult> SolicitarPermisoAProtegido([FromBody] PermisoProtegido model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var Message = "Se ha enviado solicitud de autorizacion al usuario que usted desea proteger. Cuando el usuario ingrese de nuevo a SOSpect, se le solicitará autorizar ésta solicitud. Al ser aprobado, usted recibira un mensaje para continuar con la subscripción";
                    var command = new NpgsqlCommand("CALL public.SolicitarPermisoAProtegido(:p_user_id_thirdparty_protector,:p_user_id_thirdparty_protegido,:p_tiempo_subscripcion_dias,:p_TiporelacionId);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                    command.Parameters.AddWithValue(":p_tiempo_subscripcion_dias", NpgsqlDbType.Integer, model.tiempo_subscripcion_dias);
                    command.Parameters.AddWithValue(":p_TiporelacionId", NpgsqlDbType.Integer, model.TiporelacionId);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Permiso solicitado con exito";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/SolicitarPermisoAProtegido",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de solicitar permiso al protegido Codigo 1";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible realizar enviar la solicitud de permiso al protegido porque el modelo no es valido";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("EliminarProtegido")]
        public async Task<IActionResult> EliminarProtegido([FromBody] EliminarProtegidoRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var Message = "Se ha eliminado el protegido seleccionado. Los poderes y el dinero usados en ésta subscripción no se pueden recuperar, tal como se indicó antes de que confirmaras la solicitud.";
                    var command = new NpgsqlCommand("CALL public.EliminarProtegido(:p_user_id_thirdparty_protector,:p_user_id_thirdparty_protegido);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                    

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Eliminacion de protegido realizada con éxito";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/EliminarProtegido",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de eliminar al protegido. Codigo: 1";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible eliminar al protegido, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("EliminarProtector")]
        public async Task<IActionResult> EliminarProtector([FromBody] EliminarProtegidoRequest model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var Message = "Se ha cancelado la subscripcion para el protector seleccionado. Este movimiento se notificará al protector ya que los poderes y el dinero usado no se pueden recuperar, tal como se indicó antes de que confirmaras ésta solicitud.";
                    var command = new NpgsqlCommand("CALL public.EliminarProtector(:p_user_id_thirdparty_protector,:p_user_id_thirdparty_protegido);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);


                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Eliminacion de protector realizada con éxito";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/EliminarProtector",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = "Ocurrio un error en el proceso de eliminar al protector. Codigo: 1";
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible eliminar al protector, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("AprobarPermisoAProtector")]
        public async Task<IActionResult> AprobarPermisoAProtector([FromBody] AprobacionProtegido model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                var Message = "";
                var ErrorMessage = "";
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    Message = "Se ha enviado la respuesta de aprobación al usuario que desea estar pendiente de tus alarmas. Cuando el usuario protector ingrese de nuevo a SOSpect, se le informará de ésta aprobación para que complete la subscripción.";
                    var command = new NpgsqlCommand("CALL public.AprobarPermisoAProtector(:p_user_id_thirdparty_protegido,:p_user_id_thirdparty_protector);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/AprobarPermisoAProtector",
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
                    Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                            ErrorMessage = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error en el proceso de aprobar permiso. Codigo: 1");
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                            ErrorMessage = "An error occurred in the permit approval process. Code: 1";
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = ErrorMessage;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to send the permit approval, the model is invalid.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("RechazarPermisoAProtector")]
        public async Task<IActionResult> RechazarPermisoAProtector([FromBody] AprobacionProtegido model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                var Message = "";
                var ErrorMessage = "";
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    Message = "Se ha enviado la respuesta de rechazo al usuario que deseaba estar pendiente de tus alarmas.";
                    var command = new NpgsqlCommand("CALL public.RechazarPermisoAProtector(:p_user_id_thirdparty_protegido,:p_user_id_thirdparty_protector);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                            
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/RechazarPermisoAProtector",
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
                    Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                            ErrorMessage = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Ocurrio un error en el proceso de rechazar permiso. Codigo: 1");
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                            ErrorMessage = "An error occurred in the process of denying permission. Code: 1";
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = ErrorMessage;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to send permission rejection, the model is invalid.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("SuspenderPermisoAProtector")]
        public async Task<IActionResult> SuspenderPermisoAProtector([FromBody] SuspensionProtegido model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();
                    var Message = "Se ha suspendido el envio de alarmas a los protectores que puedas tener habilitados. El tiempo de la subscripcion no se extiende por este motivo y el protector no recibira notificacion de esta solicitud de suspension temporal.";
                    var command = new NpgsqlCommand("CALL public.SuspenderPermisoAProtector(:p_user_id_thirdparty_protegido,:p_tiempo_suspension);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                    command.Parameters.AddWithValue(":p_tiempo_suspension", NpgsqlDbType.Integer, model.p_tiempo_suspension);

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection

                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, Message);
                        }
                        catch (Exception)
                        {
                            Message = Message;
                        }

                    }
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/SuspenderPermisoAProtector",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to suspend notifications to protectors, the model is invalid.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("SubscripcionProtegido")]
        public async Task<IActionResult> SubscripcionProtegido([FromBody] Subscripciones model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.SubscripcionProtegido(:p_user_id_thirdparty_protector,:p_user_id_thirdparty_protegido);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protegido", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protegido);
                                        

                    result = await command.ExecuteNonQueryAsync();

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = "";
                    responseMessage.Message = "Subscripcion realizada con exito";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/SubscripcionProtegido",
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
                    var Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to make the subscription, the model is not valid.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("SubscripcionZonaVigilancia")]
        public async Task<IActionResult> SubscripcionZonaVigilancia([FromBody] SubscripcionesVigilancia model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                var Message = "";
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.SubscripcionZonaVigilancia(:p_user_id_thirdparty_protector,:p_Latitud_zona,:p_Longitud_zona);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_Latitud_zona", NpgsqlDbType.Numeric, model.Latitud_zona);
                    command.Parameters.AddWithValue(":p_Longitud_zona", NpgsqlDbType.Numeric, model.Longitud_zona);

                    result = await command.ExecuteNonQueryAsync();
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Subscripcion realizada con exito");
                        }
                        catch (Exception)
                        {
                            Message = "Successful subscription";
                        }

                    }

                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/SubscripcionZonaVigilancia",
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
                    Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to make the subscription, the model is not valid.";
                return BadRequest(responseMessage);
            }

        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet]
        [Route("ListarRadiosDisponibles")]
        public async Task<IActionResult> ListarRadiosDisponibles(string user_id_thirdparty)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    List<RadiosDisponiblesResponse> ListaRadiosDisponibles = new List<RadiosDisponiblesResponse>();
                    string sql2 = $"SELECT radio_alarmas_id,radio_mts FROM obtener_radio_alarmas(\'{user_id_thirdparty}\');";
                    using (NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection))
                    {
                        //ConsultaDePuntosDelMapaAColocarEnNuevaUbicacionDeUsuario;
                        NpgsqlDataReader reader = command3.ExecuteReader();

                        while (reader.Read())
                        {

                            RadiosDisponiblesResponse Solicitudes = new RadiosDisponiblesResponse();

                            Solicitudes.radio_alarmas_id = reader.GetInt32(0);
                            Solicitudes.radio_mts = reader.GetInt32(1);

                            ListaRadiosDisponibles.Add(Solicitudes);
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = ListaRadiosDisponibles;
                    responseMessage.Message = "Consulted available radios.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/ListarRadiosDisponibles",
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
                    responseMessage.Message = ex.Message;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "Failed to query the list of available radios, the model is invalid.";
                return BadRequest(responseMessage);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost]
        [Route("SubscripcionRadioAlarmas")]
        public async Task<IActionResult> SubscripcionRadioAlarmas([FromBody] SubscripcionesRadio model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                var v_idioma_origen = "es";
                var v_idioma_destino = model.idioma;
                var Message="";
                try
                {

                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.SubscripcionRadioAlarmas(:p_user_id_thirdparty_protector,:p_cantidad_subscripcion);", connection);

                    command.Parameters.AddWithValue(":p_user_id_thirdparty_protector", NpgsqlDbType.Varchar, model.p_user_id_thirdparty_protector);
                    command.Parameters.AddWithValue(":p_cantidad_subscripcion", NpgsqlDbType.Integer, model.cantidad_subscripcion);
                    

                    result = await command.ExecuteNonQueryAsync();
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, "Subscripcion realizada con exito");
                        }
                        catch (Exception)
                        {
                            Message = "Successful subscription";
                        }

                    }
                    connection.Close(); //close the current connection
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = "";
                    responseMessage.Message = Message;
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Subscripciones/SubscripcionRadioAlarmas",
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
                    Message = ex.Message;
                    if (v_idioma_origen != v_idioma_destino)
                    {
                        try
                        {
                            Message = await _traductorService.Traducir(v_idioma_origen, v_idioma_destino, ex.Message);
                        }
                        catch (Exception)
                        {
                            Message = ex.Message;
                        }

                    }
                    responseMessage.IsSuccess = false;
                    responseMessage.Data = Message;
                    responseMessage.Message = Message;
                    return BadRequest(responseMessage);
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "It was not possible to make the subscription, the model is not valid.";
                return BadRequest(responseMessage);
            }

        }
    }
}
