using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SwiftPay.Repositories.Interfaces;
using SwiftPay.Configuration;
using SwiftPay.Models;

namespace SwiftPay.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;

        public UserRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<User> CreateAsync(User entity)
        {
            await _db.Set<User>().AddAsync(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<User> GetByIdAsync(int userId)
        {
            return await _db.Set<User>()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _db.Set<User>()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
        }

        public async Task<User> GetByPhoneAsync(string phone)
        {
            return await _db.Set<User>()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Phone == phone && !u.IsDeleted);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _db.Set<User>()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => !u.IsDeleted)
                .ToListAsync();
        }

        public async Task<User> UpdateAsync(User entity)
        {
            _db.Set<User>().Update(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            if (user == null)
                return false;

            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
