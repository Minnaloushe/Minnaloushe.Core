using System.Text.Json;
using Minnaloushe.Core.Toolbox.RetryRoutines.Options;

namespace Minnaloushe.Core.Repositories.Abstractions;

public record RepositoryOptions
{
    public required string ConnectionName { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public required string DatabaseName { get; set; }
    public MigrationOptions Migrations { get; init; } = new();
    public TimeSpan LeaseRenewInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan DeletedTtl { get; init; } = TimeSpan.FromDays(2);
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; } = new Dictionary<string, JsonElement>();
    public RetryPolicyOptions RetryPolicy { get; init; } = new();
    public record MigrationOptions
    {
        public bool Enabled { get; init; } = false;
    }
}
