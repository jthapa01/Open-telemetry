using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Infrastructure.RabbitMQ;

public class RabbitMqPublisher
{
    private readonly ConnectionFactory _factory;

    public RabbitMqPublisher(string connectionString)
    {
        _factory = new ConnectionFactory()
        {
            Uri = new Uri(connectionString)
        };
    }

    public void Publish<T>(T @event, string exchange)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Fanout, durable: true);

        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        const string operation = "Publish";
        var eventType = @event!.GetType().Name;
        // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md
        var activityName = $"{eventType} {operation}";
        
        using var activity = RabbitMqDiagnostics.ActivitySource.StartActivity(activityName, ActivityKind.Producer);
        
        ActivityContext contextToInject = default;
        if (activity is not null)
        {
            contextToInject = activity.Context;
        }
        else if (Activity.Current is not null)
        {
            contextToInject = Activity.Current.Context;
        }
        
        var properties = channel.CreateBasicProperties();
        properties.DeliveryMode = 2; // Persistent
        
        RabbitMqDiagnostics.Propagator.Inject(
            new PropagationContext(contextToInject, Baggage.Current),
            properties,
            InjectTraceContextIntoBasicProperties);
        
        SetActivityContext(activity, eventType, operation);
        channel.BasicPublish(exchange: exchange, routingKey: string.Empty, basicProperties: null, body: body);
    }

    private void SetActivityContext(Activity? activity, string eventType, string operation)
    {
        if (activity == null) return;

        activity.SetTag(TagNames.MessagingSystem, "rabbitMQ");
        activity.SetTag(TagNames.MessagingDestinationKind, "exchange");
        activity.SetTag(TagNames.MessagingOperation, operation);
        activity.SetTag(TagNames.MessagingDestinationName, eventType);
    }

    private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
    {
        props.Headers ??= new Dictionary<string, object>();
        props.Headers[key] = value;
    }
}