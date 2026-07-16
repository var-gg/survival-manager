using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SM.HeadlessMetrics;

/// <summary>property name ordinal 순서와 invariant number format을 고정한 metrics JSON codec.</summary>
public static class HeadlessMetricJson
{
    private static readonly JsonSerializerSettings WriteSettings = new()
    {
        ContractResolver = new OrdinalPropertyContractResolver(),
        Culture = CultureInfo.InvariantCulture,
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
    };

    private static readonly JsonSerializerSettings ReadSettings = new()
    {
        ContractResolver = new OrdinalPropertyContractResolver(),
        Culture = CultureInfo.InvariantCulture,
        MissingMemberHandling = MissingMemberHandling.Error,
    };

    public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, WriteSettings);

    public static T Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, ReadSettings)
               ?? throw new InvalidOperationException($"{typeof(T).Name} JSON이 null로 해석됐다.");
    }

    private sealed class OrdinalPropertyContractResolver : DefaultContractResolver
    {
        public OrdinalPropertyContractResolver()
        {
            NamingStrategy = new SnakeCaseNamingStrategy();
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization)
                .OrderBy(property => property.PropertyName, StringComparer.Ordinal)
                .ToList();
            for (var index = 0; index < properties.Count; index++)
            {
                properties[index].Order = index;
            }

            return properties;
        }
    }
}
