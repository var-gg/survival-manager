namespace SM.Persistence.Abstractions;

public sealed class PersistenceConfig
{
    public string ProfileId = "default";
    public string LocalSaveDirectory = "Saves";

    public static PersistenceConfig FromEnvironment()
    {
        return new PersistenceConfig
        {
            ProfileId = System.Environment.GetEnvironmentVariable("SM_PROFILE_ID") ?? "default",
            LocalSaveDirectory = System.Environment.GetEnvironmentVariable("SM_SAVE_DIR") ?? "Saves",
        };
    }
}
