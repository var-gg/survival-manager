namespace SM.Persistence.Abstractions;

public sealed class PersistenceConfig
{
    public string ProfileId = "default";
    public string LocalSaveDirectory = "Saves";
    public bool PreferPostgres;
    public string? PostgresConnectionString;

    public static PersistenceConfig FromEnvironment()
    {
        return new PersistenceConfig
        {
            ProfileId = System.Environment.GetEnvironmentVariable("SM_PROFILE_ID") ?? "default",
            LocalSaveDirectory = System.Environment.GetEnvironmentVariable("SM_SAVE_DIR") ?? "Saves",
            PreferPostgres = (System.Environment.GetEnvironmentVariable("SM_PERSISTENCE_MODE") ?? string.Empty).Equals("postgres", System.StringComparison.OrdinalIgnoreCase),
            PostgresConnectionString = System.Environment.GetEnvironmentVariable("SM_POSTGRES_CONNECTION")
        };
    }
}
