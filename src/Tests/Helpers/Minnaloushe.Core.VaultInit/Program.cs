using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Database;
using VaultSharp.V1.SecretsEngines.Database.Models;
using Role = VaultSharp.V1.SecretsEngines.Database.Role;

namespace Inpx.Processor.VaultInit;
#pragma warning disable CA1873

public class Program
{
    public static async Task<int> Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();

        });

        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var vaultAddress = config["VAULT_ADDR"] ?? "http://vault:8200";
            var vaultToken = config["VAULT_TOKEN"] ?? "root";
            var mongoUser = config["MONGO_INITDB_ROOT_USERNAME"] ?? "root";
            var mongoPass = config["MONGO_INITDB_ROOT_PASSWORD"] ?? "example";
            var consulAddress = config["CONSUL_ADDR"] ?? "http://consul:8500";

            // RabbitMQ environment variables
            var rabbitUser = config["RABBITMQ_DEFAULT_USER"] ?? "user";
            var rabbitPass = config["RABBITMQ_DEFAULT_PASS"] ?? "password";
            var rabbitHost = config["RABBITMQ_HOST"] ?? "rabbitmq";
            var rabbitPort = int.TryParse(config["RABBITMQ_PORT"], out var rp) ? rp : 5672;

            logger.LogInformation("Configuring Vault...");
            logger.LogInformation("Vault Address: {VaultAddress}", vaultAddress);

            var vaultClient = new VaultClient(new VaultClientSettings(
                vaultAddress,
                new TokenAuthMethodInfo(vaultToken)
            ));

            // Wait for Vault to be ready
            logger.LogInformation("Waiting for Vault to be ready...");
            var maxRetries = 30;
            var isReady = false;

            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var health = await vaultClient.V1.System.GetHealthStatusAsync();
                    if (health.Initialized)
                    {
                        isReady = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Vault not ready yet: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (!isReady)
            {
                logger.LogError("Vault failed to become ready after {MaxRetries} seconds", maxRetries);
                return 1;
            }

            logger.LogInformation("Vault is ready (dev mode)");

            // Check if KV v2 secrets engine is enabled
            logger.LogInformation("Checking KV secrets engine...");
            var mounts = await vaultClient.V1.System.GetSecretBackendsAsync();

            if (!mounts.Data.ContainsKey("secret/"))
            {
                logger.LogInformation("Enabling KV v2 secrets engine at secret/");

                var secretsEngine = new SecretsEngine
                {
                    Path = "secret",
                    Type = SecretsEngineType.KeyValueV2
                };

                await vaultClient.V1.System.MountSecretBackendAsync(secretsEngine);
            }
            else
            {
                logger.LogInformation("KV secrets engine already enabled at secret/");
            }

            // Write MongoDB credentials
            logger.LogInformation("Writing MongoDB credentials to Vault...");

            var mongoConnectionString = $"mongodb://{mongoUser}:{mongoPass}@mongodb:27017";

            var secretData = new Dictionary<string, object>
            {
                ["username"] = mongoUser,
                ["password"] = mongoPass,
                ["host"] = "mongodb",
                ["port"] = "27017",
                ["connection_string"] = mongoConnectionString
            };

            await vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                path: "mongodb",
                data: secretData,
                mountPoint: "secret"
            );

            // Write RabbitMQ credentials
            logger.LogInformation("Writing RabbitMQ credentials to Vault...");

            var rabbitConnectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}";

            var rabbitSecret = new Dictionary<string, object>
            {
                ["username"] = rabbitUser,
                ["password"] = rabbitPass,
            };

            await vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                path: "rabbitmq",
                data: rabbitSecret,
                mountPoint: "secret"
            );

            // Enable database secrets engine if not already enabled
            logger.LogInformation("Checking database secrets engine...");
            if (!mounts.Data.ContainsKey("database/"))
            {
                logger.LogInformation("Enabling database secrets engine...");
                await vaultClient.V1.System.MountSecretBackendAsync(new SecretsEngine
                {
                    Path = "database",
                    Type = new SecretsEngineType("database")
                });
            }
            else
            {
                logger.LogInformation("Database secrets engine already enabled");
            }

            // Configure Database secrets engine for MongoDB "develop--dev-inpx-processor-host-role" role
            logger.LogInformation("Configuring MongoDB connection 'develop-mongo-default'...");
            await vaultClient.V1.Secrets.Database.ConfigureConnectionAsync("mongodb",
                new ConnectionConfigModel()
                {
                    
                    Username = mongoUser,
                    Password = mongoPass,
                    ConnectionUrl =
                        "mongodb://{{username}}:{{password}}@mongodb:27017/admin",
                    AllowedRoles = ["develop-mongo-default-dev-inpx-processor-host-role"],
                    PluginName = "mongodb-database-plugin",
                    VerifyConnection = true,
                    
                });
            
            logger.LogInformation("Creating database role 'develop--dev-inpx-processor-host-role'...");
            await vaultClient.V1.Secrets.Database.CreateRoleAsync("develop-mongo-default-dev-inpx-processor-host-role",
                new Role()
                {
                    
                    DatabaseProviderType = DatabaseProviderType.MongoDB,
//                    DatabaseName = "develop-mongo-default",
                    CreationStatements =
                        ["{\"db\":\"admin\",\"roles\":[{\"role\":\"readWrite\",\"db\":\"InpxProcessor\"}]}"] ,
                });
            
            logger.LogInformation("✓ Database role 'develop-mongo-default-dev-inpx-processor-host-role' configured successfully");
            logger.LogInformation("MongoDB credentials stored at secret/mongodb");

            // Verify the secret was written
            logger.LogInformation("Verifying stored secret...");
            var secret = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                path: "mongodb",
                mountPoint: "secret"
            );

            if (secret?.Data?.Data != null)
            {
                logger.LogInformation("✓ Vault configuration completed successfully!");
                logger.LogInformation("Stored keys: {Keys}",
                    string.Join(", ", secret.Data.Data.Keys));
            }
            else
            {
                logger.LogWarning("Could not verify secret/mongodb");
                return 1;
            }

            // Register Vault in Consul
            logger.LogInformation("Registering Vault in Consul...");
            logger.LogInformation("Consul Address: {ConsulAddress}", consulAddress);

            using var consulClient = new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(consulAddress);
            });

            // Wait for Consul to be ready
            logger.LogInformation("Waiting for Consul to be ready...");
            var consulReady = false;

            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    await consulClient.Agent.Self();
                    consulReady = true;
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogDebug("Consul not ready yet: {Message}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (!consulReady)
            {
                logger.LogError("Consul failed to become ready after {MaxRetries} seconds", maxRetries);
                return 1;
            }

            logger.LogInformation("Consul is ready");

            // Register Vault service
            var vaultUri = new Uri(vaultAddress);
            var registration = new AgentServiceRegistration
            {
                ID = "vault",
                Name = "vault",
                Address = vaultUri.Host,
                Port = vaultUri.Port,
                Tags = ["secrets", "kv", "dev-mode"],
                Meta = new Dictionary<string, string>
                {
                    ["version"] = "1.18.1",
                    ["mode"] = "dev"
                },
                Check = new AgentServiceCheck
                {
                    HTTP = $"{vaultAddress}/v1/sys/health",
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5)
                }
            };

            
            var mongoRegistration = new AgentServiceRegistration
            {
                ID = "mongo-default",
                Name = "mongo-default",
                Address = "mongodb",
                Port = 27017,
                Tags = ["secrets", "kv", "dev-mode"],
                Meta = new Dictionary<string, string>
                {
                    ["version"] = "1.18.1",
                    ["mode"] = "dev"
                }
                
            };

            var rabbitRegistration = new AgentServiceRegistration
            {
                ID = "develop-rabbitmq",
                Name = "develop-rabbitmq",
                Address = rabbitHost,
                Port = rabbitPort,
                Tags = ["messaging", "amqp", "dev-mode"],
                Meta = new Dictionary<string, string>
                {
                    ["version"] = "4.2.0", // optional, adjust if needed
                    ["mode"] = "dev"
                },
                Check = new AgentServiceCheck
                {
                    TCP = $"{rabbitHost}:{rabbitPort}",
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5)
                }
            };

            await consulClient.Agent.ServiceRegister(registration);
            await consulClient.Agent.ServiceRegister(mongoRegistration);
            await consulClient.Agent.ServiceRegister(rabbitRegistration);
            logger.LogInformation("✓ Vault registered in Consul as service 'vault'");

            // Verify registration
            var services = await consulClient.Agent.Services();
            if (services.Response.TryGetValue("vault", out var vaultService))
            {
                logger.LogInformation("✓ Verified Vault service registration in Consul");
                logger.LogInformation("Service Details - Address: {Address}, Port: {Port}, Tags: {Tags}",

                    vaultService.Address, vaultService.Port, string.Join(", ", vaultService.Tags));
            }
            else
            {
                logger.LogWarning("Could not verify Vault service in Consul");
            }

            if (services.Response.TryGetValue("develop-mongodb", out var mongoService))
            {
                logger.LogInformation("✓ Verified Vault service registration in Consul");
                logger.LogInformation("Service Details - Address: {Address}, Port: {Port}, Tags: {Tags}",
                    mongoService.Address, mongoService.Port, string.Join(", ", mongoService.Tags));
            }
            else
            {
                logger.LogWarning("Could not verify Vault service in Consul");
            }

            if (services.Response.TryGetValue("develop-rabbitmq", out var rabbitService))
            {
                logger.LogInformation("✓ Verified RabbitMQ service registration in Consul");
                logger.LogInformation("Service Details - Address: {Address}, Port: {Port}, Tags: {Tags}",
                    rabbitService.Address, rabbitService.Port, string.Join(", ", rabbitService.Tags));
            }
            else
            {
                logger.LogWarning("Could not verify RabbitMQ service in Consul");
            }

            logger.LogInformation("✓ All initialization tasks completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure services: {Message}", ex.Message);
            return 1;
        }
    }
}
#pragma warning restore CA1873