// Ignore Spelling: Outbox Npgsql

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using System.Text.Json.Nodes;

namespace Gonkers.NpgsqlOutbox;

public class NpgsqlOutboxMonitorService : BackgroundService
{
    private readonly Func<JsonNode, CancellationToken, Task> _action;
    private readonly ILogger<NpgsqlOutboxMonitorService> _logger;
    private readonly string
        _connectionString,
        _replicationSlotName,
        _publicationName;
    private readonly int _actionMaximumAttempts;

    public NpgsqlOutboxMonitorService(
        MonitorOptions options,
        Func<JsonNode, CancellationToken, Task> action,
        ILogger<NpgsqlOutboxMonitorService> logger)
    {
        _connectionString = options.ConnectionString;
        _replicationSlotName = options.ReplicationSlotName;
        _publicationName = options.PublicationName;
        _actionMaximumAttempts = options.ActionFailureRetryCount + 1;
        _action = action;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var conn = new LogicalReplicationConnection(_connectionString);
        var replicationSlot = new PgOutputReplicationSlot(_replicationSlotName);
        var replicationOptions = new PgOutputReplicationOptions(_publicationName, 1);
        await conn.Open(stoppingToken);
        JsonArray jsonArray = [];
        if (stoppingToken.IsCancellationRequested) return;

        // https://www.npgsql.org/doc/replication.html
        await foreach (var message in conn.StartReplication(replicationSlot, replicationOptions, stoppingToken))
        {
            _logger.LogDebug("Received message of type '{messageType}'", message.GetType().FullName);

            if (message is RelationMessage relationMessage)
            {
                _logger.LogTrace("Skipping message type '{messageType}'.", message.GetType().FullName);
                continue;
            }
            else if (message is BeginMessage)
            {
                jsonArray = [];
            }
            else if (message is InsertMessage insertMessage)
            {
                var json = await GetRowDataAsJson(insertMessage, stoppingToken);
                jsonArray.Add(json);
                _logger.LogTrace("{json}", json.ToJsonString());
            }
            else if (message is CommitMessage commitMessage)
            {
                for (var attempt = 1; attempt <= _actionMaximumAttempts; attempt++)
                {
                    try
                    {
                        _logger.LogTrace("Executing user defined action attempt {attempt}.", attempt);
                        await _action(jsonArray, stoppingToken);
                        _logger.LogDebug("Executed user defined action with {attempt} attempt(s).", attempt);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unable to complete user defined action. Reason: {error}", ex.Message);
                        if (attempt >= _actionMaximumAttempts)
                        {
                            _logger.LogCritical("The user defined action failed after {attempt} attempts.", attempt);
                            throw;
                        }
                        await Task.Delay(100 * (attempt + 1), stoppingToken);
                    }

                    if (stoppingToken.IsCancellationRequested) break;
                }


                conn.SetReplicationStatus(commitMessage.WalEnd);
                await conn.SendStatusUpdate(stoppingToken);
            }
            else
            {
                const string unsupportedMessage = """
                    The replication message type {messageType} is not supported. Update your publication definition to only include inserts.
                    CREATE PUBLICATION outbox_pub FOR TABLE outbox.publication WITH (publish = 'insert');
                    """;

                _logger.LogInformation(unsupportedMessage, message.GetType().FullName);
            }
        }
    }

    /// <summary>
    /// Convert the InsertMessage to a JsonObject representing the row data.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected async Task<JsonObject> GetRowDataAsJson(InsertMessage message, CancellationToken stoppingToken)
    {
        string[]
            jsonLiteralTypes = ["bool", "bit", "float4", "float8", "int2", "int4", "int8", "money", "numeric"],
            jsonObjectTypes = ["json", "jsonb"],
            jsonStringTypes = ["char", "date", "text", "time", "time_stamp", "timestamp", "timestamptz", "timetz", "uuid", "varchar"];

        JsonObject json = [];

        int col = 0;
        await foreach (var value in message.NewRow)
        {
            var propertyName = message.Relation.Columns[col++].ColumnName;
            var propertyType = value.GetPostgresType();
            _logger.LogTrace("Reading column '{columnName}' of type '{columnType}'", propertyName, propertyType.FullName);

            var propertyValue = await value.Get<string>(stoppingToken);
            _logger.LogTrace("Value: {columnValue}", propertyValue);

            if (jsonLiteralTypes.Contains(propertyType.InternalName))
                json.Add(propertyName, JsonNode.Parse(propertyValue));
            else if (jsonObjectTypes.Contains(propertyType.InternalName))
                json.Add(propertyName, JsonNode.Parse(propertyValue));
            else if (jsonStringTypes.Contains(propertyType.InternalName))
                json.Add(propertyName, JsonValue.Create(propertyValue));
            else
            {
                _logger.LogInformation("The PostgreSQL type '{columnType}' is not supported. Column name: '{columnName}'", propertyType.FullName, propertyName);
                continue;
            }

            if (stoppingToken.IsCancellationRequested) return json;
        }
        return json;
    }
}
