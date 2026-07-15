using System.Text.Json;
using Identity.API.Messaging.CloudEvents;
using Identity.API.Messaging.Events;

namespace Identity.API.Tests.UnitTests;

public class CloudEventFactoryTests
{
    private static UserCreatedEvent SampleEvent(DateTimeOffset occurredAt) => new(
        Id: "user-1",
        Email: "ada@example.com",
        Username: "ada@example.com",
        Name: "Ada",
        LastName: "Lovelace",
        ProfilePicture: "https://example.com/p.png",
        EmailConfirmed: false,
        Version: 0,
        OccurredAt: occurredAt);

    [Fact]
    public void Create_sets_envelope_fields_from_event_metadata()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var @event = SampleEvent(occurredAt);
        var id = Guid.NewGuid();

        var cloudEvent = CloudEventFactory.Create(@event, id, occurredAt);

        cloudEvent.Id.ShouldBe(id.ToString());
        cloudEvent.Type.ShouldBe("com.socketchat.identity.user.created");
        cloudEvent.Source.ShouldBe("urn:socketchat:identity-api");
        cloudEvent.SpecVersion.ShouldBe("1.0");
        cloudEvent.DataContentType.ShouldBe("application/json");
        cloudEvent.Time.ShouldBe(occurredAt);
        cloudEvent.Data.ShouldBe(@event);
        cloudEvent.TraceParent.ShouldBeNull();
    }

    [Fact]
    public void CloudEvent_serializes_with_spec_field_names_and_snake_case_data()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var cloudEvent = CloudEventFactory.Create(SampleEvent(occurredAt), Guid.NewGuid(), occurredAt);

        var json = JsonDocument.Parse(JsonSerializer.Serialize(cloudEvent)).RootElement;

        json.GetProperty("specversion").GetString().ShouldBe("1.0");
        json.GetProperty("type").GetString().ShouldBe("com.socketchat.identity.user.created");
        json.GetProperty("source").GetString().ShouldBe("urn:socketchat:identity-api");
        json.GetProperty("datacontenttype").GetString().ShouldBe("application/json");
        json.TryGetProperty("traceparent", out _).ShouldBeFalse("traceparent must be omitted when null");
        json.TryGetProperty("tracestate", out _).ShouldBeFalse("tracestate must be omitted when null");

        var data = json.GetProperty("data");
        data.GetProperty("id").GetString().ShouldBe("user-1");
        data.GetProperty("email").GetString().ShouldBe("ada@example.com");
        data.GetProperty("username").GetString().ShouldBe("ada@example.com");
        data.GetProperty("name").GetString().ShouldBe("Ada");
        data.GetProperty("last_name").GetString().ShouldBe("Lovelace");
        data.GetProperty("profile_picture").GetString().ShouldBe("https://example.com/p.png");
        data.GetProperty("email_confirmed").GetBoolean().ShouldBeFalse();
        data.GetProperty("version").GetInt32().ShouldBe(0);
        data.TryGetProperty("occurred_at", out _).ShouldBeTrue();
    }

    [Fact]
    public void Event_metadata_matches_spec()
    {
        UserCreatedEvent.CloudEventType.ShouldBe("com.socketchat.identity.user.created");
        UserCreatedEvent.ExchangeName.ShouldBe("identity.user.created");
        UserUpdatedEvent.CloudEventType.ShouldBe("com.socketchat.identity.user.updated");
        UserUpdatedEvent.ExchangeName.ShouldBe("identity.user.updated");
    }
}
