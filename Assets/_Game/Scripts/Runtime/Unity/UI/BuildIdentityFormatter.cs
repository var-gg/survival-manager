using System;
using System.Text;

namespace SM.Unity.UI;

public sealed class BuildIdentityFormatter
{
    private readonly ContentTextResolver _contentText;

    public BuildIdentityFormatter(ContentTextResolver contentText)
    {
        _contentText = contentText;
    }

    public string BuildBlueprintSummary(GameSessionState session)
    {
        var equippedPermanentId = session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
        var benchAugmentIds = session.Profile.UnlockedPermanentAugmentIds
            .Where(id => !string.Equals(id, equippedPermanentId, StringComparison.Ordinal))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"Blueprint: {session.Profile.ActiveBlueprintId}");
        builder.AppendLine($"Posture: {session.SelectedTeamPosture}");
        builder.AppendLine($"Permanent: {FormatAugmentName(equippedPermanentId)}");
        builder.AppendLine($"Bench: {FormatAugmentList(benchAugmentIds)}");
        builder.AppendLine($"Thesis: {session.SelectedTeamPosture} / {FormatAugmentName(equippedPermanentId)} / Squad {session.ExpeditionSquadHeroIds.Count}");
        return builder.ToString();
    }

    private string FormatAugmentName(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return "None";
        }

        return _contentText.GetAugmentName(augmentId);
    }

    private string FormatAugmentList(IReadOnlyCollection<string> augmentIds)
    {
        if (augmentIds.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", augmentIds.Select(FormatAugmentName));
    }
}
