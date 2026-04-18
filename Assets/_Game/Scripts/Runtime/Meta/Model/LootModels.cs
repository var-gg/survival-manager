using System.Collections.Generic;
using SM.Core.Content;

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
