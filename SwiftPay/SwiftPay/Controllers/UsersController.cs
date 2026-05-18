using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using SwiftPay.Services.Interfaces;
using SwiftPay.DTOs.UserCustomerDTO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace SwiftPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="dto">User creation data</param>
        /// <returns>Created user DTO</returns>
        /// <response code="200">User created successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="409">Business conflict (e.g., duplicate user)</response>
        /// <response code="500">Server error</response>
        [HttpPost]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return Ok(new { message = "User created successfully.", data = created });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User DTO with roles</returns>
        /// <response code="200">User found</response>
        /// <response code="404">User not found</response>
        /// <response code="500">Server error</response>
        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int userId)
        {
            try
            {
                var user = await _service.GetByIdAsync(userId); 
                if (user == null)
                    return NotFound(new { message = $"User with ID {userId} not found." });

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Forbid();

                if (!int.TryParse(userIdClaim, out var currentUserId))
                    return Forbid();

                // Admins can view any user
                if (!User.IsInRole("Admin"))
                {
                    if (user.UserId != currentUserId)
                        return Forbid();
                }

                return Ok(new { message = "User retrieved successfully.", data = user });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get user by email
        /// </summary>
        /// <param name="email">User email</param>
        /// <returns>User DTO with roles</returns>
        /// <response code="200">User found</response>
        /// <response code="404">User not found</response>
        /// <response code="500">Server error</response>
        [HttpGet("email/{email}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByEmail(string email)
        {
            try
            {
                var user = await _service.GetByEmailAsync(email);
                if (user == null)
                    return NotFound(new { message = $"User with email {email} not found." });

                return Ok(new { message = "User retrieved successfully.", data = user });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all users
        /// </summary>
        /// <returns>List of all active user DTOs</returns>
        /// <response code="200">Users retrieved successfully</response>
        /// <response code="500">Server error</response>
        [HttpGet]
        [Authorize(Roles = "Admin,Ops")]
        [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var users = await _service.GetAllAsync();
                return Ok(new { message = "Users retrieved successfully.", data = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving users.", error = ex.Message });
            }
        }
    
        /// <summary>
        /// Update user information
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="dto">User update data</param>
        /// <returns>Updated user DTO</returns>
        /// <response code="200">User updated successfully</response>
        /// <response code="404">User not found</response>
        /// <response code="409">Business conflict (e.g., duplicate email/phone)</response>
        /// <response code="500">Server error</response>
        [HttpPut("{userId}")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int userId, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var existing = await _service.GetByIdAsync(userId);
                if (existing == null)
                    return NotFound(new { message = $"User with ID {userId} not found." });

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Forbid();

                if (!int.TryParse(userIdClaim, out var currentUserId))
                    return Forbid();

                if (!User.IsInRole("Admin"))
                {
                    if (existing.UserId != currentUserId)
                        return Forbid();
                }

                var updated = await _service.UpdateAsync(userId, dto);
                return Ok(new { message = "User updated successfully.", data = updated });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete (soft delete) a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success or failure message</returns>
        /// <response code="200">User deleted successfully</response>
        /// <response code="404">User not found</response>
        /// <response code="500">Server error</response>
        [HttpDelete("{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int userId)
        {
            try
            {
                var existing = await _service.GetByIdAsync(userId);
                if (existing == null)
                    return NotFound(new { message = $"User with ID {userId} not found." });

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Forbid();

                if (!int.TryParse(userIdClaim, out var currentUserId))
                    return Forbid();

                if (!User.IsInRole("Admin"))
                {
                    if (existing.UserId != currentUserId)
                        return Forbid();
                }

                var result = await _service.DeleteAsync(userId);
                if (!result)
                    return NotFound(new { message = $"User with ID {userId} not found." });

                return Ok(new { message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the user.", error = ex.Message });
            }
        }

// Practice
        // [HttpPost("create")]
        // [Authorize(Roles = "Admin")]
        // public async Task<IActionResult> createUser([FromQuery]int userId,[FromBody] CreateUserDto dto)
        // {
        //     try
        //     {
        //         var created = await _service.CreateAsync(dto);
        //         return Ok(created);
        //     }
        //     catch (Exception ex)
        //     {
        //         return StatusCode(500, new { message = "An error occurred while creating the user.", error = ex.Message });
        //     }
        // }
    }
}
