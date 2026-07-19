using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Serialization;

public static class ContentSnapshotJsonSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StatKeyJsonConverter(), new StatKeyDictionaryJsonConverter(), new ReadOnlyCollectionConverter() },
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

internal sealed class StatKeyDictionaryJsonConverter : JsonConverter
{
    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
    {
        if (!objectType.IsGenericType || objectType.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>))
        {
            return false;
        }

        return objectType.GetGenericArguments()[0] == typeof(StatKey);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var valueType = objectType.GetGenericArguments()[1];
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(StatKey), valueType);
        var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;
        var jsonObject = JObject.Load(reader);
        foreach (var property in jsonObject.Properties())
        {
            dictionary.Add(new StatKey(property.Name), property.Value.ToObject(valueType, serializer));
        }

        return dictionary;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotSupportedException("StatKey dictionary writes use Newtonsoft.Json's default dictionary writer.");
    }
}

internal sealed class StatKeyJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(StatKey);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
        {
            throw new JsonSerializationException($"Expected a string StatKey, got {reader.TokenType}.");
        }

        return new StatKey((string?)reader.Value ?? string.Empty);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is not StatKey statKey)
        {
            throw new JsonSerializationException("Expected a StatKey value.");
        }

        writer.WriteValue(statKey.Value);
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
