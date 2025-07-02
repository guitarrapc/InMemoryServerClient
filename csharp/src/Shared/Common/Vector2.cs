namespace Shared.Common;

/// <summary>
/// Vector2 for positions
/// </summary>
/// <remarks>
/// Creates a new Vector2
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(Vector2JsonConverter))]
public readonly record struct Vector2
{
    /// <summary>
    /// X coordinate
    /// </summary>
    public readonly int X { get; init; }

    /// <summary>
    /// Y coordinate
    /// </summary>
    public readonly int Y { get; init; }

    /// <summary>
    /// Creates a new Vector2
    /// </summary>
    public Vector2(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => $"({X}, {Y})";

    public static Vector2 InvalidPosition { get; } = new Vector2(-1, -1);
}

/// <summary>
/// Custom JSON converter for Vector2 to avoid property name conflicts
/// </summary>
public class Vector2JsonConverter : System.Text.Json.Serialization.JsonConverter<Vector2>
{
    public override Vector2 Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
        {
            throw new System.Text.Json.JsonException();
        }

        int x = 0;
        int y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
            {
                return new Vector2(x, y);
            }

            if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName)
            {
                throw new System.Text.Json.JsonException();
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "X":
                    x = reader.GetInt32();
                    break;
                case "Y":
                    y = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new System.Text.Json.JsonException();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, Vector2 value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}
