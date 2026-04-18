using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core;

namespace SM.Meta;

public sealed class StoryArchiveCatalogService
{
    public IReadOnlyList<StoryArchiveEntryRecord> BuildCatalog(
        StoryArchiveCatalogSpec catalog,
        NarrativeProgressRecord progress)
    {
        if (catalog == null || catalog.Entries.Count == 0)
        {
            return Array.Empty<StoryArchiveEntryRecord>();
        }

        var seenIds = new HashSet<string>(progress.SeenEventIds, StringComparer.Ordinal);
        var entries = new List<StoryArchiveEntryRecord>();

        foreach (var entry in catalog.Entries)
        {
            if (entry == null)
            {
                continue;
            }

            var labelText = entry.LabelTextKey;
            var unlocked = seenIds.Contains(entry.EventId);

            entries.Add(new StoryArchiveEntryRecord(
                entry.EventId,
                entry.ChapterId,
                entry.SiteId,
                entry.PresentationKey,
                entry.PresentationKind,
                labelText,
                unlocked,
                entry.RuntimeContext,
                entry.ReplayPolicy,
                entry.SourceOrder));
        }

        return entries
            .OrderBy(e => e.ChapterId, StringComparer.Ordinal)
            .ThenBy(e => e.SiteId, StringComparer.Ordinal)
            .ThenBy(e => e.SourceOrder)
            .ToList();
    }
}
