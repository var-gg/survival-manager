using SM.Persistence.Abstractions;

namespace SM.Persistence.Json;

public static class PersistenceFacade
{
    public static ISaveRepository CreateDefault(PersistenceConfig config)
    {
        return new JsonSaveRepository(config.LocalSaveDirectory);
    }
}
