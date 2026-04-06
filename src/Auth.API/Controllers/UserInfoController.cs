using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("MercantecSpa")]
    public sealed class UserInfoController : ControllerBase
    {
        private readonly AuthDbContext _db;

        public UserInfoController(AuthDbContext db) => _db = db;

        [HttpGet("/userinfo")]
        [Produces("application/json")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
                return Unauthorized();

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDisabled, cancellationToken);

            if (user is null)
                return Unauthorized();

            var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Minimal OIDC-style payload (vi binder ikke scopes her endnu).
            return Ok(new
            {
                sub = user.Id.ToString(),
                name = user.DisplayName,
                email = user.Email,
                login_method = user.LastLoginMethod ?? MercantecAuthClaims.LoginMethodValues.Unknown,
                role = roles,
            });
        }
    }
}

