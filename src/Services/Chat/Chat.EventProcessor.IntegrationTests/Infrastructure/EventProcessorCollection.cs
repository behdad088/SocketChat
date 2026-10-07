namespace Chat.EventProcessor.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class EventProcessorCollection : ICollectionFixture<EventProcessorFixture>
{
    public const string Name = "EventProcessor";
}
