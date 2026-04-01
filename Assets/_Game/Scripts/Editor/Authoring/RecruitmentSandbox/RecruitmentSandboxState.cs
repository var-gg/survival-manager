using SM.Core.Contracts;
using UnityEngine;

namespace SM.Editor.Authoring.RecruitmentSandbox;

public sealed class RecruitmentSandboxState : ScriptableObject
{
    public int PreviewSeed = 17;
    public string PreviewRosterArchetypeIdsCsv = string.Empty;
    public string PreviewTemporaryAugmentIdsCsv = string.Empty;
    public string PreviewPermanentAugmentIdsCsv = string.Empty;
    public ScoutDirectiveKind PreviewScoutDirectiveKind = ScoutDirectiveKind.None;
    public string PreviewScoutSynergyTagId = string.Empty;
    public int PreviewRarePity;
    public int PreviewEpicPity;
    public RetrainOperationKind RetrainOperation = RetrainOperationKind.FullRetrain;
    public ScoutDirectiveKind RuntimeScoutDirectiveKind = ScoutDirectiveKind.Backline;
    public string RuntimeScoutSynergyTagId = string.Empty;
    public string DuplicateGrantArchetypeId = string.Empty;
    public int SelectedHeroIndex;
    [TextArea(8, 24)] public string LastPackReport = string.Empty;
    [TextArea(4, 16)] public string LastRuntimeReport = string.Empty;
    [TextArea(4, 16)] public string LastRetrainReport = string.Empty;
    [TextArea(4, 16)] public string LastDuplicateReport = string.Empty;
    [TextArea(4, 16)] public string LastDismissReport = string.Empty;
}
