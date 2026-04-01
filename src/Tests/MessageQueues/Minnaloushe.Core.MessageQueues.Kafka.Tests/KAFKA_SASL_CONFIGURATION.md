# Kafka SASL Authentication in Tests

## Overview
The Kafka integration tests are configured to use **SASL_PLAINTEXT** authentication to match production environments. This ensures that username/password authentication is properly tested.

## Configuration Details

### Container Setup (GlobalFixture)
The Kafka test containers are configured with the following environment variables:

```bash
KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=BROKER:PLAINTEXT,PLAINTEXT:SASL_PLAINTEXT
KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS=PLAIN
KAFKA_LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG=org.apache.kafka.common.security.plain.PlainLoginModule required username="admin" password="admin-secret" user_admin="admin-secret";
KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL=PLAIN
KAFKA_INTER_BROKER_LISTENER_NAME=BROKER
```

#### What This Configuration Does:
1. **LISTENER_SECURITY_PROTOCOL_MAP**: Maps two listeners:
   - `BROKER` (internal, PLAINTEXT, no auth) - used for inter-broker communication
   - `PLAINTEXT` (external, SASL_PLAINTEXT) - used by test clients

2. **LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS**: Enables PLAIN SASL mechanism for the external listener

3. **LISTENER_NAME_PLAINTEXT_PLAIN_SASL_JAAS_CONFIG**: Configures the JAAS authentication with:
   - Broker credentials: username="admin", password="admin-secret"
   - User credentials: user_admin="admin-secret" (allows client "admin" with password "admin-secret")

4. **SASL_MECHANISM_INTER_BROKER_PROTOCOL**: Sets inter-broker to use PLAIN (though BROKER uses PLAINTEXT)

5. **INTER_BROKER_LISTENER_NAME**: Specifies which listener brokers use to communicate (BROKER, which is PLAINTEXT)

### Client Configuration
All test clients (producers and consumers) must use:

```csharp
var config = new ProducerConfig // or ConsumerConfig
{
    BootstrapServers = GlobalFixture.KafkaInstance1.GetBootstrapAddress(),
    SecurityProtocol = SecurityProtocol.SaslPlaintext,
    SaslMechanism = SaslMechanism.Plain,
    SaslUsername = GlobalFixture.KafkaUsername,  // "admin"
    SaslPassword = GlobalFixture.KafkaPassword   // "admin-secret"
};
```

### Application Code Integration
When using the MessageQueues library with Kafka connections, pass credentials via the connection configuration:

```csharp
Helpers.CreateConnection(
    connectionName,
    bootstrapAddress,
    serviceKey,
    username: GlobalFixture.KafkaUsername,
    password: GlobalFixture.KafkaPassword
)
```

## Why SASL Authentication in Tests?

1. **Production Parity**: Most production Kafka clusters use authentication
2. **Security Testing**: Validates that authentication configuration works correctly
3. **Error Detection**: Catches configuration mismatches early
4. **Real-world Scenarios**: Tests behave more like actual deployment scenarios

## Troubleshooting

### Error: "protocol 'PLAINTEXT' does not match security.protocol setting 'sasl_plaintext'"
**Cause**: Client is configured for SASL but container expects PLAINTEXT (or vice versa)
**Solution**: Ensure both container and client configurations match

### Error: "No brokers configured"
**Cause**: Authentication failure preventing broker discovery
**Solution**: Verify username/password match between container JAAS config and client config

### Error: "Authentication failed"
**Cause**: Wrong credentials or JAAS configuration
**Solution**: Check that `user_<username>="<password>"` in JAAS config matches client credentials

## References
- [Confluent Kafka SASL Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/index.html)
- [Kafka Security Configuration](https://kafka.apache.org/documentation/#security)
