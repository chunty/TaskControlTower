# Task Turnstile

A thread-safe named task lifecycle manager for .NET. Prevents duplicate background job execution across threads and — optionally — across multiple application instances via a distributed backing store.

> **Think of it like a turnstile.** Every job that wants to run must push through first. Only one can hold the bar at a time — others wait their turn or are sent away. When the job is done, the bar rotates and the next one can step through.

## Pages

| | |
|---|---|
| [Setup](Setup) | Registering the store — in-memory, Redis, SQL Server, shared `IDistributedCache` — and all configuration options |
| [API Reference](API-Reference) | Full `ITaskStateManager` method reference |
| [Patterns](Patterns) | Real-world usage examples — Coravel, BackgroundService, manual start/stop |
| [Testing](Testing) | `FakeTaskStateManager` and manual mocking with Moq / NSubstitute |
| [Custom Store](Custom-Store) | Implementing `ITaskStateStore` to back the manager with any storage |
| [Migrating to v2](Migrating-to-v2) | Breaking changes and migration guide for v2.0 |
