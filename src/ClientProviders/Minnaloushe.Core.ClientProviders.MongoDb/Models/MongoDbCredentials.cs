using Minnaloushe.Core.ClientProviders.Abstractions.CredentialsWatcher;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Models;

public record MongoDbCredentials(string Username, string Password) : LeasedCredentials;