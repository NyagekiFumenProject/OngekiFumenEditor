using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public static class McpOperationLogHelper
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        public static void LogRequest(string operationName, object payload)
        {
            LogInfo("REQUEST", operationName, payload);
        }

        public static void LogResult(string operationName, object payload)
        {
            LogInfo("RESULT", operationName, payload);
        }

        public static void LogAuthorization(string operationName, object payload)
        {
            LogInfo("AUTH", operationName, payload);
        }

        public static void LogWarning(string stage, string operationName, object payload)
        {
            CoreLog.LogWarn(BuildMessage(stage, operationName, payload));
        }

        public static void LogError(string stage, string operationName, object payload)
        {
            CoreLog.LogError(BuildMessage(stage, operationName, payload));
        }

        private static void LogInfo(string stage, string operationName, object payload)
        {
            CoreLog.LogInfo(BuildMessage(stage, operationName, payload));
        }

        private static string BuildMessage(string stage, string operationName, object payload)
        {
            var payloadJson = SerializePayload(payload);
            return $"[MCP {stage}] {operationName} {payloadJson}";
        }

        private static string SerializePayload(object payload)
        {
            try
            {
                if (payload is null)
                    return "null";

                var jsonElement = JsonSerializer.SerializeToElement(payload, payload.GetType(), JsonSerializerOptions);
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    WriteSanitizedJson(writer, jsonElement);
                }

                return NormalizeJsonForArchive(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (Exception ex)
            {
                var fallback = payload?.ToString() ?? "null";
                fallback = NormalizeJsonForArchive(fallback);
                return $"{{\"serializationError\":\"{NormalizeJsonForArchive(ex.Message)}\",\"fallback\":\"{fallback}\"}}";
            }
        }

        public static string NormalizeJsonForArchive(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            return content
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        private static void WriteSanitizedJson(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        WriteSanitizedJson(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                        WriteSanitizedJson(writer, item);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(NormalizeStringValue(element.GetString()));
                    break;
                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private static string NormalizeStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }
    }
}
