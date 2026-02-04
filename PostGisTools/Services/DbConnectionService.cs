using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PostGisTools.Services
{
    public interface IDbConnectionService
    {
        string CurrentConnectionString { get; set; }
        string BuildConnectionString(string host, int port, string database, string username, string password);
        Task<(bool ok, string message)> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    }

    public sealed class DbConnectionService : IDbConnectionService
    {
        public string CurrentConnectionString { get; set; } = string.Empty;

        public string BuildConnectionString(string host, int port, string database, string username, string password)
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host?.Trim(),
                Port = port,
                Database = database?.Trim(),
                Username = username?.Trim(),
                Password = password ?? string.Empty,

                // Safe defaults; no schema loading here.
                Pooling = true,
                Timeout = 5,
                CommandTimeout = 5,
                SslMode = SslMode.Prefer
            };

            return builder.ConnectionString;
        }

        public async Task<(bool ok, string message)> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            try
            {
                // Use DbContextOptionsBuilder as requested; CanConnect touches the server but doesn't query schema.
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;

                await using var ctx = new TestDbContext(options);

                var canConnect = await ctx.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? (true, "Connection successful")
                    : (false, "Unable to connect (CanConnect returned false)");
            }
            catch (OperationCanceledException)
            {
                return (false, "Connection test cancelled");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private sealed class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
            {
            }
        }
    }
}
