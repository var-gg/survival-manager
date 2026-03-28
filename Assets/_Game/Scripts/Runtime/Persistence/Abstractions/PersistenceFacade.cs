namespace SM.Persistence.Abstractions;

public static class PersistenceFacade
{
    public static ISaveRepository CreateDefault(PersistenceConfig config)
    {
        if (config.PreferPostgres && !string.IsNullOrWhiteSpace(config.PostgresConnectionString))
        {
            try
            {
                return new SM.Persistence.Postgres.PostgresSaveRepository(config.PostgresConnectionString!);
            }
            catch
            {
                // Fall through to JSON to keep MVP playable.
            }
        }

        return new SM.Persistence.Json.JsonSaveRepository(config.LocalSaveDirectory);
    }
}
