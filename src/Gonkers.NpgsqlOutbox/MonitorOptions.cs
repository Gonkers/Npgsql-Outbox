namespace Gonkers.NpgsqlOutbox;

public record MonitorOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string ReplicationSlotName { get; init; } = string.Empty;
    public string PublicationName { get; init; } = string.Empty;
    public int ActionFailureRetryCount { get; init; } = 2;
}
