using System.Collections.Generic;

namespace SM.Editor.Narrative;

public sealed record NarrativeSeedImportOptions(
    bool CleanRemovedAssets,
    bool InsertMissingEnglishEntries,
    bool DryRun);

public sealed record NarrativeSeedImportResult(
    int CreatedAssets,
    int UpdatedAssets,
    int DeletedAssets,
    int CreatedLocalizationEntries,
    int UpdatedLocalizationEntries,
    IReadOnlyList<NarrativeDiagnostic> Diagnostics);

public static class NarrativeSeedImporter
{
    public static NarrativeSeedImportResult Import(
        NarrativeSeedManifest manifest,
        NarrativeSeedImportOptions options)
    {
        // Phase 1: SO generation from manifest
        // TODO: implement actual SO creation/update logic
        return new NarrativeSeedImportResult(0, 0, 0, 0, 0, manifest.Diagnostics);
    }
}
