using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using SospectAPI.Data.Entities;
using SospectAPI.Helpers;
using SospectAPI.Models;
using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;

namespace SospectAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PersonasController : ControllerBase
    {
        private readonly IUserHelper _userHelper;
        private readonly IConfiguration _configuration;
        private readonly IAmazonS3 _s3;
        ResponseMessage responseMessage = new ResponseMessage();

        public PersonasController(IUserHelper userHelper, IConfiguration configuration, IAmazonS3 s3)
        {
            _userHelper = userHelper;
            _configuration = configuration;
            _s3 = s3;
        }

        [HttpPost]
        [Route("LoginUser")]
        public async Task<IActionResult> GetUserLoged([FromBody] LoginRequest model)
        {
            if (ModelState.IsValid)
            {
                Personas? user = await _userHelper.GetUserAsync(model.Email);
                if (user != null)
                {
                    var results = new
                    {
                        user
                    };

                    return Created(string.Empty, results);
                }
            }

            return BadRequest(new { message = "The user does not exist." });
        }

        
        [HttpPost]
        [Route("RegisterUser")]
        public async Task<IActionResult> RegisterUser([FromBody] Personas model)
        {
            if (ModelState.IsValid)
            {
                int result = -1;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
                    NpgsqlConnection connection = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await connection.OpenAsync();

                    var command = new NpgsqlCommand("CALL public.RegistrarUsuario(:p_login,:p_user_id_thirdparty,:p_RegistrationId,:p_Plataforma,:p_idioma);", connection);

                    command.Parameters.AddWithValue(":p_login", NpgsqlDbType.Varchar, model.login);
                    command.Parameters.AddWithValue(":p_user_id_thirdparty", NpgsqlDbType.Varchar, model.user_id_thirdparty);
                    command.Parameters.AddWithValue(":p_RegistrationId", NpgsqlDbType.Varchar, model.RegistrationId);
                    command.Parameters.AddWithValue(":p_Plataforma", NpgsqlDbType.Varchar, model.Plataforma);
                    command.Parameters.AddWithValue(":p_idioma", NpgsqlDbType.Varchar, model.Idioma);

                    result = await command.ExecuteNonQueryAsync();

                    if (result < 0)
                    {
                        connection.Close(); //close the current connection
                        responseMessage.IsSuccess = true;
                        responseMessage.Data = "";
                        responseMessage.Message = "User registration successful";
                        return Ok(responseMessage);
                    }
                    else
                    {
                        connection.Close();
                        responseMessage.IsSuccess = false;
                        responseMessage.Data = "";
                        responseMessage.Message = "An error occurred while registering the user code 1";
                        return BadRequest(responseMessage);
                    }
                }
                catch (Exception ex)
                {
                    var logData = new
                    {
                        timestamp = DateTime.UtcNow,
                        endpoint = "personas/RegisterUser",
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
                    responseMessage.Message = "An error occurred while registering the user code 2";
                    return BadRequest(responseMessage);
                }
            }
            else
            {
                responseMessage.IsSuccess = false;
                responseMessage.Data = "";
                responseMessage.Message = "An error occurred with the people model for user registration code 3";
                return BadRequest(responseMessage);
            }
        }

    }
}
