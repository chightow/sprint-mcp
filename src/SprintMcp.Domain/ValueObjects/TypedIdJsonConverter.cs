using System.Text.Json;
using System.Text.Json.Serialization;

namespace SprintMcp.Domain.ValueObjects;

public class SprintIdJsonConverter : JsonConverter<SprintId>
{
    public override SprintId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => SprintId.FromString(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SprintId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

public class TicketIdJsonConverter : JsonConverter<TicketId>
{
    public override TicketId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TicketId.FromString(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, TicketId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
