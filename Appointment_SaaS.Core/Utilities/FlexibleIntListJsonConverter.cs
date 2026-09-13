using System.Text.Json;
using System.Text.Json.Serialization;

namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// n8n bazen [20,18], bazen "20,18" gönderir. İkisini de List&lt;int&gt; yapar.
/// </summary>
public sealed class FlexibleIntListJsonConverter : JsonConverter<List<int>?>
{
    public override List<int>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                TryAdd(list, ReadOne(ref reader));
            return list.Count == 0 ? null : list;
        }

        if (reader.TokenType == JsonTokenType.String)
            return ParseCsv(reader.GetString());

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var one) && one > 0)
            return new List<int> { one };

        return null;
    }

    public override void Write(Utf8JsonWriter writer, List<int>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var id in value)
            writer.WriteNumberValue(id);
        writer.WriteEndArray();
    }

    private static int? ReadOne(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
            return n;
        if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var parsed))
            return parsed;
        return null;
    }

    private static List<int>? ParseCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var list = new List<int>();
        foreach (var part in raw.Trim().Trim('[', ']').Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), out var id) && id > 0 && !list.Contains(id))
                list.Add(id);
        }

        return list.Count == 0 ? null : list;
    }

    private static void TryAdd(List<int> list, int? id)
    {
        if (id is > 0 && !list.Contains(id.Value))
            list.Add(id.Value);
    }
}
