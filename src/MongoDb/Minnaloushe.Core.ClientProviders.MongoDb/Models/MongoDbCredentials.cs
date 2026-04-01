using Minnaloushe.Core.ClientProviders.Abstractions;

namespace Minnaloushe.Core.ClientProviders.MongoDb.Models;

public record MongoDbCredentials(string Username, string Password, string LeaseId, int LeaseDurationSeconds, bool Renewable) : ILeasedCredentials;