using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Reads an RFC 7662 <c>exp</c>-style Unix-seconds claim, tolerating this OpenEMR fork's actual
/// <c>/introspect</c> response (confirmed live 2026-07-10): it serializes <c>exp</c> as a PHP
/// <c>DateTime</c> object (<c>{"date":..,"timezone_type":..,"timezone":..}</c>) rather than a
/// number. Nothing in this codebase currently consumes <see cref="IntrospectionResponse.ExpiresAtUnixSeconds"/>
/// (only <c>Active</c>/<c>Subject</c> gate the SMART launch), so an unrecognized shape degrades to
/// <c>null</c> - honestly representing "not determined" - rather than throwing and failing the
/// entire introspection call, and with it every SMART launch (the strict default converter took the
/// whole callback down with a 500).
/// </summary>
public sealed class LenientUnixSecondsConverter : JsonConverter<long?>
{
    /// <inheritdoc />
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var value))
        {
            return value;
        }

        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        return null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
