using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Abstractions;

public interface ISaveRepository
{
    SaveProfile LoadOrCreate(string profileId);
    void Save(SaveProfile profile);
}
