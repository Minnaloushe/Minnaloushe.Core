# Home Infrastructure Core Library

Personal collection of infrastructure abstractions, client wrappers and common patterns I use across my home services, homelab and self-hosted applications. Built on **.NET 10**.

**Status**: Evolving / actively used in personal projects

## Why this exists

This repository is **not** trying to become a general-purpose library for everyone.

It is my personal **infrastructure foundation** that helps me:

- Avoid copy-pasting the same connection logic, retry policies and configuration patterns across many small projects
- Standardize how I talk to RabbitMQ, Kafka, MongoDB, PostgreSQL, MinIO/S3, Telegram, MikroTik, etc.
- Quickly spin up new microservices / workers with consistent observability, secrets handling and service discovery behavior

Think of it as **my private dotnet infrastructure kit** — a curated grab-bag of integrations and utilities shaped entirely by real homelab usage.

## Main Goals

- **Encapsulate** infrastructure-specific boilerplate  
  (service discovery, secret management, connection pooling, health checks, configuration conventions)
- **Unify** common messaging, persistence and storage patterns into reusable abstractions
- **Collect** small, battle-tested utilities I reach for repeatedly

## Key Packages / Modules

| Folder             | Purpose                                                              | Main abstractions / highlights                                                           |
|--------------------|----------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `ClientProviders`  | Lifecycle-managed wrappers for external service clients             | `IClientProvider<TClient>` + implementations for Kafka, RabbitMQ, MongoDB, PostgreSQL, MinIO, Telegram, MikroTik |
| `MessageQueues`    | Unified producer/consumer abstractions                              | RabbitMQ & Kafka producers/consumers behind a consistent API; DI registration helpers    |
| `Rabbit`           | Low-level RabbitMQ building blocks                                  | Connection/channel configuration, raw consumer & producer base types                    |
| `Repositories`     | Database access layer wrappers                                      | MongoDB & PostgreSQL providers behind a common repository interface                     |
| `S3`               | Object storage adapters                                             | MinIO / AWS S3 compatible client wrappers                                               |
| `Vault`            | HashiCorp Vault integration                                         | `VaultStoredOptions` base type; async secret loading via `IResolvedOptions<T>`          |
| `Logging`          | Structured logging helpers                                          | NLog wiring + `Redaction` support for masking sensitive data in log output              |
| `ServiceDiscovery` | Service discovery abstractions                                      | Consul-backed service registration and resolution                                       |
| `Api`              | Shared API building blocks                                          | Common contracts, base controllers, typed `ApiClient` wrapper                           |
| `K8s`              | Kubernetes-oriented utilities                                       | Startup/liveness routines, readiness probe middleware                                   |
| `Toolbox`          | Miscellaneous small helpers                                         | Retry policies, async initializers, caching, cancellation context, collections, polling folder watcher, and more |
| `Templates`        | Reference / example projects                                        | Minimal API and Worker Service templates showing real wiring                            |

> Most infrastructure modules ship paired **`.Vault` variants** (e.g. `ClientProviders.Kafka.Vault`, `Repositories.Postgres.Vault`) that pull connection credentials directly from HashiCorp Vault instead of `appsettings.json`.

## Documentation

Supplementary usage guides live in [`docs/`](docs/):

- [Vault Options](docs/Vault-Options-Usage.md) — loading secrets from Vault KV v2
- [Message Queue Usage](docs/MessageQueue-Usage.md) — producers / consumers walkthrough
- [Message Queue Registration Flow](docs/MessageQueue-Registration-Flow.md) — DI wiring details
- [Repository Usage](docs/Repository-Usage.md) — MongoDB & PostgreSQL patterns
- [Telegram Multiple Bots](docs/TelegramMultipleBots.md) — running several Telegram bots side-by-side

## Project Philosophy

- Heavily opinionated — shaped by actual home infrastructure needs
- Favors **simple & explicit** over magic / heavy abstraction
- Minimal external dependencies wherever possible
- Designed to be **copied**, vendored or partially adopted — not necessarily published to NuGet

## GitHub Copilot

This project was developed with assistance from GitHub Copilot.

My personal opinion: it can't create perfect code from scratch, but it becomes extremely useful for routine tasks and for wiring things together.  
Tests and DI registration code were heavily supported by it.