using SospectAPI.Data.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SospectAPI.Helpers
{
    public interface IUserHelper
    {
        Task<Personas?> GetUserAsync(string email);

        Task<bool> AddUserAsync(Personas user);

    }
}
