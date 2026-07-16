using System;
using Newtonsoft.Json;
using SM.Persistence.Abstractions.Models;

namespace SM.Editor.Validation;

/// <summary>진단 branch가 production save를 쓰지 않고 같은 in-memory profile을 복제하는 codec.</summary>
internal static class H100ProfileSnapshotCodec
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    public static string Capture(SaveProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        return JsonConvert.SerializeObject(profile, Formatting.None, Settings);
    }

    public static SaveProfile Restore(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            throw new ArgumentException("profile snapshot is empty", nameof(snapshot));
        }

        return JsonConvert.DeserializeObject<SaveProfile>(snapshot, Settings)
               ?? throw new InvalidOperationException("profile snapshot deserialized to null");
    }
}
