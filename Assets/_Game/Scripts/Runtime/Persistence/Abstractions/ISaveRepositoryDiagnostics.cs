using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Abstractions;

public interface ISaveRepositoryDiagnostics
{
    SaveRepositoryLoadResult LoadOrCreateDetailed(string profileId, SaveRepositoryRequest? request = null);
    SaveRepositorySaveResult SaveDetailed(SaveProfile profile, SaveRepositoryRequest? request = null);
}
