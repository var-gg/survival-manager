using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Content;

[CreateAssetMenu(menuName = "SM/Narrative/Story Archive Catalog")]
public sealed class StoryArchiveCatalogDefinition : ScriptableObject
{
    [SerializeField] private StoryArchiveEntryDefinition[] _entries = Array.Empty<StoryArchiveEntryDefinition>();

    public IReadOnlyList<StoryArchiveEntryDefinition> Entries => _entries;
}
