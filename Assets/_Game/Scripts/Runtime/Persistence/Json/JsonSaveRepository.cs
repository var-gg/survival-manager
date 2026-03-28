using System.IO;
using System.Text.Json;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Json;

public sealed class JsonSaveRepository : ISaveRepository
{
    private readonly string _rootDirectory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

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
        return JsonSerializer.Deserialize<SaveProfile>(json, _options) ?? new SaveProfile { ProfileId = profileId };
    }

    public void Save(SaveProfile profile)
    {
        var path = GetPath(profile.ProfileId);
        var json = JsonSerializer.Serialize(profile, _options);
        File.WriteAllText(path, json);
    }

    private string GetPath(string profileId) => Path.Combine(_rootDirectory, $"{profileId}.json");
}
