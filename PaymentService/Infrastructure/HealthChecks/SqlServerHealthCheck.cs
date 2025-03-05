using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PaymentService.Infrastructure.HealthChecks;

public class SqlServerHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerHealthCheck> _logger;

    public SqlServerHealthCheck(IConfiguration configuration, ILogger<SqlServerHealthCheck> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is not configured");
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "Database", connection.Database },
                { "ServerVersion", connection.ServerVersion },
                { "State", connection.State.ToString() },
                { "LastChecked", DateTime.UtcNow }
            };

            return HealthCheckResult.Healthy("SQL Server connection is healthy", data: data);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL Server health check failed");
            return HealthCheckResult.Unhealthy(
                "SQL Server connection is unhealthy", 
                ex,
                new Dictionary<string, object>
                {
                    { "ErrorNumber", ex.Number },
                    { "ErrorState", ex.State },
                    { "ErrorClass", ex.Class },
                    { "LastChecked", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL Server health check failed with unexpected error");
            return HealthCheckResult.Unhealthy(
                "SQL Server health check failed", 
                ex,
                new Dictionary<string, object>
                {
                    { "ExceptionType", ex.GetType().Name },
                    { "LastChecked", DateTime.UtcNow }
                });
        }
    }
} 