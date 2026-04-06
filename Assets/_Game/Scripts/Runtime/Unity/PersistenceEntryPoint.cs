using System;
using System.IO;
using SM.Persistence.Abstractions;
using SM.Persistence.Json;

namespace SM.Unity;

public sealed class PersistenceEntryPoint
{
    public PersistenceConfig Config { get; }
    public ISaveRepository Repository { get; }

    public PersistenceEntryPoint()
        : this(null, null)
    {
    }

    public PersistenceEntryPoint(PersistenceConfig? config, ISaveRepository? repository)
    {
        Config = config ?? PersistenceConfig.FromEnvironment();
        Config.LocalSaveDirectory = ResolveSaveDirectory(Config.LocalSaveDirectory);
        Repository = repository ?? SM.Persistence.Json.PersistenceFacade.CreateDefault(Config);
    }

    private static string ResolveSaveDirectory(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(Environment.CurrentDirectory, configuredPath);
    }
}
