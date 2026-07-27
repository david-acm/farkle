using FluentAssertions;
using OpenTelemetry.Logs;
using WebApp.Telemetry;

namespace HotDice.WebTests.CrossCutting;

/// <summary>
/// Pins the Application Insights custom-event contract. The event name is a product-branded string
/// (<c>HotDice.&lt;EventType&gt;</c>), which means a product rename silently changes what dashboards
/// and KQL queries have to match — exactly what happened in the Farkle → HotDice rename, where the
/// prefix moved with no test to notice. This makes the next such change deliberate.
/// </summary>
public class DomainEventTelemetryShould
{
    [Fact]
    public void NameDomainEventsWithTheProductPrefix()
    {
        var record = RecordWith(("EventType", "DiceRolled"));

        Process(record);

        Attribute(record, "microsoft.custom_event.name").Should().Be("HotDice.DiceRolled");
    }

    [Fact]
    public void MapGameAndPlayerOntoSyntheticSessionAndUserIds()
    {
        // Synthetic ids only — a display name here would put PII into telemetry.
        var record = RecordWith(("EventType", "TurnPassed"), ("GameId", 992615), ("PlayerId", 7));

        Process(record);

        Attribute(record, "session.id").Should().Be("g992615");
        Attribute(record, "enduser.id").Should().Be("g992615-p7");
    }

    [Fact]
    public void LeaveNonDomainRecordsAlone()
    {
        var record = RecordWith(("SomethingElse", "value"));

        Process(record);

        Attribute(record, "microsoft.custom_event.name").Should().BeNull();
    }

    [Fact]
    public void NotRenameARecordThatIsAlreadyTagged()
    {
        var record = RecordWith(("EventType", "DiceRolled"), ("microsoft.custom_event.name", "Already.Set"));

        Process(record);

        Attribute(record, "microsoft.custom_event.name").Should().Be("Already.Set");
    }

    private static void Process(LogRecord record) => new DomainEventLogProcessor().OnEnd(record);

    private static object? Attribute(LogRecord record, string key) =>
        record.Attributes?.FirstOrDefault(a => a.Key == key).Value;

    /// <summary>
    /// <see cref="LogRecord"/> has no public constructor and the in-memory exporter lives in a
    /// package this project does not reference, so the instance is created uninitialised. The
    /// processor only reads and replaces <c>Attributes</c>, so nothing else needs to be set up.
    /// </summary>
    private static LogRecord RecordWith(params (string Key, object? Value)[] attributes)
    {
        var record = (LogRecord)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(LogRecord));

        record.Attributes = attributes
            .Select(a => new KeyValuePair<string, object?>(a.Key, a.Value))
            .ToList();

        return record;
    }
}
