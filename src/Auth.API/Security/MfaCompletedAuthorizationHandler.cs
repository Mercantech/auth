using Microsoft.AspNetCore.Authorization;

namespace Auth.API.Security;

public sealed class MfaCompletedRequirement : IAuthorizationRequirement;

public sealed class MfaCompletedAuthorizationHandler : AuthorizationHandler<MfaCompletedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaCompletedRequirement requirement)
    {
        var pending = context.User.FindFirst(MercantecAuthClaims.MfaPending)?.Value;
        if (!string.Equals(pending, "true", StringComparison.OrdinalIgnoreCase))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public sealed class AuthenticatedRequirement : IAuthorizationRequirement;

public sealed class AuthenticatedAuthorizationHandler : AuthorizationHandler<AuthenticatedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AuthenticatedRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
