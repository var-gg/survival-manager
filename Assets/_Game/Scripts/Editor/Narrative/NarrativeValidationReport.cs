using System.Collections.Generic;

namespace SM.Editor.Narrative;

public sealed record NarrativeValidationReport(
    bool Succeeded,
    IReadOnlyList<NarrativeDiagnostic> Diagnostics);

public interface INarrativeValidator
{
    NarrativeValidationReport Validate(NarrativeSeedManifest manifest);
}
