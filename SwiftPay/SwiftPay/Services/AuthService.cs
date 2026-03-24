using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SwiftPay.DTOs.UserCustomerDTO;
using SwiftPay.Models;
using SwiftPay.Repositories.Interfaces;
using SwiftPay.Services.Interfaces;
using SwiftPay.Constants.Enums;

namespace SwiftPay.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IUserRoleRepository _userRoleRepo;
        private readonly IMapper _mapper;
        private readonly JwtTokenSettings _jwtSettings;
        private readonly SwiftPay.Configuration.AppDbContext _db;

        public AuthService(IUserRepository userRepo, IRoleRepository roleRepo, IUserRoleRepository userRoleRepo, IMapper mapper, IOptions<JwtTokenSettings> jwtOptions, SwiftPay.Configuration.AppDbContext db) => (_userRepo, _roleRepo, _userRoleRepo, _mapper, _jwtSettings, _db) = (userRepo, roleRepo, userRoleRepo, mapper, jwtOptions?.Value ?? new JwtTokenSettings(), db);

        public async Task<AuthResponseDto> RegisterAsync(RegisterUserDto dto)
        {
            var existing = await _userRepo.GetByEmailAsync(dto.Email);
            if (existing != null) throw new InvalidOperationException("Email already registered.");

            var existingPhone = await _userRepo.GetByPhoneAsync(dto.Phone);
            if (existingPhone != null) throw new InvalidOperationException("Phone number already registered.");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var user = _mapper.Map<User>(dto);
                user.PasswordHash = await Task.Run(() => BCrypt.Net.BCrypt.EnhancedHashPassword(dto.Password));
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                user.Status = UserStatus.Active;

                var created = await _userRepo.CreateAsync(user);

                var customerRole = await _roleRepo.GetByRoleTypeAsync(Constants.Enums.RoleType.Customer);
                if (customerRole == null)
                {
                    // Defensive: create the default Customer role if it does not exist to avoid blocking public registration.
                    customerRole = await _roleRepo.CreateAsync(new Role
                    {
                        RoleType = Constants.Enums.RoleType.Customer,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                var ur = new UserRole { UserId = created.UserId, RoleId = customerRole.RoleId, IsActive = true, CreatedAt = DateTime.UtcNow };
                await _userRoleRepo.CreateAsync(ur);

                await tx.CommitAsync();
                return _mapper.Map<AuthResponseDto>(created);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userRepo.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new InvalidOperationException("Invalid Email.");

            var hash = user.PasswordHash;
            if (string.IsNullOrEmpty(hash))
                throw new InvalidOperationException("Account has no password set.");

            // Enhanced verify on background thread
            var verified = await Task.Run(() => BCrypt.Net.BCrypt.EnhancedVerify(dto.Password, hash));
            if (!verified)
                throw new InvalidOperationException("Password is incorrect.");

            // Gather roles for claims
            var userRoles = await _userRoleRepo.GetByUserIdAsync(user.UserId);
            var roles = new List<string>();
            foreach (var ur in userRoles)
            {
                var r = await _roleRepo.GetByIdAsync(ur.RoleId);
                if (r != null) roles.Add(r.RoleType.ToString());
            }


            var token = GenerateJwtToken(user, roles);

            // Note: we intentionally do not perform automatic rehashing here.
            // Verification is done only against the stored hash to avoid unnecessary writes.

            var authDto = _mapper.Map<AuthResponseDto>(user);
            return new LoginResponseDto { Token = token, User = authDto };
        }

        private string GenerateJwtToken(User user, IEnumerable<string> roles)
        {
            var key = _jwtSettings.Key;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Jwt:Key is not configured.");

            var issuer = _jwtSettings.Issuer;
            var audience = _jwtSettings.Audience;

            // Hard limit to 1440 minutes (24 hours) as required
            var expiresMinutes = Math.Min(_jwtSettings.ExpiresMinutes, 1440);
    
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
