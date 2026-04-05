using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "ok" });
}
