using Gonkers.NpgsqlOutbox;
using System.Text.Json.Nodes;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService(services =>
{
    var options = new MonitorOptions
    {
        ConnectionString = "Host=localhost; Username=postgres; Password=password; Database=postgres",
        ReplicationSlotName = "outbox_pub",
        PublicationName = "outbox_pub"
    };

    var logger = services.GetRequiredService<ILogger<NpgsqlOutboxMonitorService>>();
    return new NpgsqlOutboxMonitorService(options, ProcessPublication, logger);
});

var host = builder.Build();
host.Run();


async Task ProcessPublication(JsonNode json, CancellationToken cancellationToken)
{
    if (Random.Shared.Next() % 2 == 0)
        throw new NotImplementedException("boom!");

    Console.WriteLine("------------");
    Console.WriteLine(json.ToString());
}