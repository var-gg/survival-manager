using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SM.HeadlessCensus;

internal static class BuildSpaceJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new OrdinalPropertyContractResolver(),
        Culture = CultureInfo.InvariantCulture,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
    };

    public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

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
