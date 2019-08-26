using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace StrawberryShake
{
    public class JsonOperationSerializer
        : IOperationSerializer
    {
        public Task SerializeAsync(IOperation operation, Stream requestStream)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (requestStream is null)
            {
                throw new ArgumentNullException(nameof(requestStream));
            }

            var writer = new Utf8JsonWriter(requestStream);
            writer.WriteStartObject();

            writer.WritePropertyName("id");
            writer.WriteStringValue(operation.Document.Hash);

            writer.WritePropertyName("query");
            writer.WriteStringValue(operation.Document.Content);

            writer.WritePropertyName("operationName");
            writer.WriteStringValue(operation.Name);

            if (operation.Variables.Count != 0)
            {
                writer.WritePropertyName("variables");
                WriteValue(operation.Variables, writer);
            }

            writer.WriteEndObject();
        }

        private static void WriteValue(
            object value,
            Utf8JsonWriter writer)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                switch (value)
                {
                    case IReadOnlyDictionary<string, object> obj:
                        writer.WriteStartObject();

                        foreach (KeyValuePair<string, object> variable in obj)
                        {
                            writer.WritePropertyName(variable.Key);
                            WriteValue(variable.Value, writer);
                        }

                        writer.WriteEndObject();
                        break;

                    case IReadOnlyList<object> list:
                        writer.WriteStartArray();

                        foreach (object item in list)
                        {
                            WriteValue(item, writer);
                        }

                        writer.WriteEndArray();
                        break;

                    case byte b:
                        writer.WriteNumberValue(b);
                        break;

                    case short s:
                        writer.WriteNumberValue(s);
                        break;

                    case int i:
                        writer.WriteNumberValue(i);
                        break;

                    case long l:
                        writer.WriteNumberValue(l);
                        break;

                    case ushort us:
                        writer.WriteNumberValue(us);
                        break;

                    case uint ui:
                        writer.WriteNumberValue(ui);
                        break;

                    case ulong ul:
                        writer.WriteNumberValue(ul);
                        break;

                    case float f:
                        writer.WriteNumberValue(f);
                        break;

                    case double d:
                        writer.WriteNumberValue(d);
                        break;

                    case decimal dec:
                        writer.WriteNumberValue(dec);
                        break;

                    case bool b:
                        writer.WriteBooleanValue(b);
                        break;

                    case string s:
                        writer.WriteStringValue(s);
                        break;
                }
            }

            writer.WriteEndObject();
        }
    }
}
