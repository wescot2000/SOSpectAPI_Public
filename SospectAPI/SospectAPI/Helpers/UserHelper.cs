using SospectAPI.Data;
using SospectAPI.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SospectAPI.Helpers
{
    public class UserHelper : IUserHelper
    {
        private readonly DataContext _context;

        public UserHelper(DataContext context)
        {
            _context = context;
        }

        public async Task<bool> AddUserAsync(Personas user)
        {
            _context.Personas.Add(user);
            var entriesSaved = await _context.SaveChangesAsync();
            if (entriesSaved > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
               
        public async Task<Personas?> GetUserAsync(string email)
        {
            Personas? persona = await _context.Personas.FirstOrDefaultAsync(u => u.login == email);
            if (persona is not null)
            {
                return persona;
            }
            else
            {
                return null;
            }
        }
    }
}
