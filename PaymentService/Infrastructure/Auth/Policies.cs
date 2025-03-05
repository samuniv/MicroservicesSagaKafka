using Microsoft.AspNetCore.Authorization;

namespace PaymentService.Infrastructure.Auth;

public static class Policies
{
    public const string ViewDlqStatistics = "ViewDlqStatistics";
    public const string RetryDlqMessages = "RetryDlqMessages";
    public const string RetryAllDlqMessages = "RetryAllDlqMessages";
    public const string ManageDlqSettings = "ManageDlqSettings";

    public static void AddDlqPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ViewDlqStatistics, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") ||
                context.User.IsInRole("Support") ||
                context.User.HasClaim("dlq_permissions", "view")));

        options.AddPolicy(RetryDlqMessages, policy =>
            policy.RequireAssertion(context =>
                (context.User.IsInRole("Admin") ||
                 context.User.IsInRole("Support")) &&
                context.User.HasClaim("dlq_permissions", "retry")));

        options.AddPolicy(RetryAllDlqMessages, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") &&
                context.User.HasClaim("dlq_permissions", "retry_all")));

        options.AddPolicy(ManageDlqSettings, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") &&
                context.User.HasClaim("dlq_permissions", "manage")));
    }
} 