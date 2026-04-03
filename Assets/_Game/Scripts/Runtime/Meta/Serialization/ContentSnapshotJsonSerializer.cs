using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SM.Meta.Model;

namespace SM.Meta.Serialization;

public static class ContentSnapshotJsonSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new ReadOnlyCollectionConverter() },
    };

    public static string Serialize(CombatContentSnapshot snapshot)
    {
        return JsonConvert.SerializeObject(snapshot, Settings);
    }

    public static CombatContentSnapshot Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<CombatContentSnapshot>(json, Settings)
               ?? throw new InvalidOperationException("Failed to deserialize CombatContentSnapshot from JSON.");
    }
}

internal sealed class ReadOnlyCollectionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        if (!objectType.IsGenericType) return false;
        var generic = objectType.GetGenericTypeDefinition();
        return generic == typeof(IReadOnlyDictionary<,>)
            || generic == typeof(IReadOnlyList<>);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var generic = objectType.GetGenericTypeDefinition();
        if (generic == typeof(IReadOnlyDictionary<,>))
        {
            var args = objectType.GetGenericArguments();
            var dictType = typeof(Dictionary<,>).MakeGenericType(args);
            return serializer.Deserialize(reader, dictType);
        }

        if (generic == typeof(IReadOnlyList<>))
        {
            var args = objectType.GetGenericArguments();
            var listType = typeof(List<>).MakeGenericType(args);
            return serializer.Deserialize(reader, listType);
        }

        return serializer.Deserialize(reader, objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}
