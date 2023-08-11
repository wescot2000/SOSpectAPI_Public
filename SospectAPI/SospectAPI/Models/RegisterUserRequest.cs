using SospectAPI.Data.Entities;

namespace SospectAPI.Models
{
        public class RegisterUserRequest : Personas
        {
            public string Password { get; set; }
        }

}
