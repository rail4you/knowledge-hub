using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowledgeHub.Json;

public class NullableGuidConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Guid.TryParse(value, out var result))
            {
                return result;
            }

            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return new Guid(reader.GetInt64().ToString());
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value);
        }
    }
}
