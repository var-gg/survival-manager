using System.IO;
using Newtonsoft.Json;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Json;

public sealed class JsonSaveRepository : ISaveRepository
{
    private readonly string _rootDirectory;

    public JsonSaveRepository(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public SaveProfile LoadOrCreate(string profileId)
    {
        var path = GetPath(profileId);
        if (!File.Exists(path))
        {
            var profile = new SaveProfile { ProfileId = profileId };
            Save(profile);
            return profile;
        }

        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<SaveProfile>(json) ?? new SaveProfile { ProfileId = profileId };
    }

    public void Save(SaveProfile profile)
    {
        var path = GetPath(profile.ProfileId);
        var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    private string GetPath(string profileId) => Path.Combine(_rootDirectory, $"{profileId}.json");
}
