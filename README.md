# Gonkers.NpgsqlOutbox

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Description

In short, this is a .NET background service worker that uses [Npgsql to watch a logical replication publication](https://www.npgsql.org/doc/replication.html) for database `INSERT` commands.

This service is used to help implement the [outbox pattern in .NET with PostgreSQL](https://event-driven.io/en/push_based_outbox_pattern_with_postgres_logical_replication/).
The basic idea behind the outbox pattern is to store outgoing messages or events in a 
local database table called the "outbox". Instead of sending the messages directly, 
the service writes them to the outbox table as part of the same database transaction 
that updates the application state. This ensures that the messages are persisted 
atomically with the state changes.

A separate background process often called the "outbox processor" or "message 
dispatcher", will (typically) periodically poll the outbox table and send 
the messages to their intended destinations. But __NOT__ in this case! This service
uses the built-in logical replication process of PostgreSQL to reliably "push" 
messages to this replication client. This means messages are dealt with in real time.

## Features

- Real time dispatching of messages inserted into the tables being monitored.
- Multiple messages that are inserted in the same transaction are batched together.
- Lots of logging at the Trace level for debugging.

## Usage

There is a [sample project](./samples/OutboxWorker/) that shows the basic usage.

## Contributing

Contributions are welcome! I don't have any guidelines, but feel free to open a
pull request.

## License

This project is licensed under the [MIT License](LICENSE).

## Contact

- Author: Gonkers
- GitHub: [Gonkers](https://github.com/gonkers)
