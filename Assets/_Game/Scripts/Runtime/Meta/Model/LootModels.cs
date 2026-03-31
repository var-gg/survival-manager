using System.Collections.Generic;
using SM.Content.Definitions;

namespace SM.Meta.Model;

public sealed record LootEntry(
    string Id,
    RewardType RewardType,
    int Amount,
    RarityBracketValue RarityBracket);

public sealed record LootBundleResult(
    string SourceId,
    string SourceKind,
    IReadOnlyList<LootEntry> Entries);
