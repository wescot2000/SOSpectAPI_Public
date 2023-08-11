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
using NuGet.Protocol.Plugins;

namespace SospectAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VersionesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ITraductorService _traductorService;
        private readonly IAmazonS3 _s3;
        private DataContext _dataDb;
        ResponseMessage responseMessage = new ResponseMessage();

        public VersionesController(IConfiguration configuration, DataContext dataDb, ITraductorService traductorService, IAmazonS3 s3)
        {
            _configuration = configuration;
            _dataDb = dataDb;
            _traductorService = traductorService;
            _s3 = s3;
        }

        [HttpGet]
        [Route("ComprobarVersionActiva")]
        public async Task<IActionResult> ComprobarVersionActiva(string versioncliente)
        {
            if (ModelState.IsValid)
            {
                NpgsqlConnection connection = null;
                NpgsqlDataReader reader = null;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    RevisarVersionResponse RevisaVersion = new RevisarVersionResponse();
                    string sql2 = "SELECT COALESCE((SELECT is_supported FROM app_versions WHERE version_number = @version), FALSE) as flag_soportada;";
                    NpgsqlCommand command3 = new NpgsqlCommand(sql2, connection);
                    command3.Parameters.AddWithValue("@version", NpgsqlDbType.Varchar, versioncliente);
                    
                    reader = command3.ExecuteReader();

                    while (reader.Read())
                    {

                        RevisaVersion.flag_soportada = reader.GetBoolean(0);
 
                    }

                    
                    responseMessage.IsSuccess = true;
                    responseMessage.Data = RevisaVersion;
                    responseMessage.Message = "Version consultada correctamente.";
                    return Ok(responseMessage);

                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "Versiones/ComprobarVersionActiva",
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
                    responseMessage.Message = "Error al consultar version activa";
                    return BadRequest(responseMessage);
                }
                finally
                {
                    if (connection != null)
                    {
                        reader.Close();
                        connection.Close();
                    }
                }
            }

            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "No fue posible consultar la version activa, el modelo no es válido.";
                return BadRequest(responseMessage);
            }
        }
    }
}
