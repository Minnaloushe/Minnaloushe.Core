using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

namespace Minnaloushe.Core.ClientProviders.Postgres.Models;

public record PostgresCredentials(string Username, string Password) : LeasedCredentials;
