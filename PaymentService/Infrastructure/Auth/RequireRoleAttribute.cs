using Microsoft.AspNetCore.Authorization;

namespace PaymentService.Infrastructure.Auth;

public class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(params string[] roles) : base()
    {
        Roles = string.Join(",", roles);
    }
} 