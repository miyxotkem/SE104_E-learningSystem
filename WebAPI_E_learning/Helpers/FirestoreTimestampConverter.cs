using Google.Cloud.Firestore;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAPI_E_learning.Helpers
{
    public class FirestoreTimestampConverter : JsonConverter<Timestamp>
    {
        public override Timestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (DateTime.TryParse(reader.GetString(), out DateTime dt))
                {
                    return Timestamp.FromDateTime(dt.ToUniversalTime());
                }
            }
            return default;
        }

        public override void Write(Utf8JsonWriter writer, Timestamp value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }
}
