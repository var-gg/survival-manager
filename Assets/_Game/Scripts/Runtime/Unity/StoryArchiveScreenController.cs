using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content;
using SM.Meta;
using SM.Unity.ContentConversion;
using SM.Unity.Narrative;
using UnityEngine;

namespace SM.Unity;

[DisallowMultipleComponent]
public sealed class StoryArchiveScreenController : MonoBehaviour
{
    private const string ArchiveCatalogResourcesPath = "_Game/Content/Definitions/StoryArchive/StoryArchiveCatalog";

    private StoryArchiveCatalogDefinition? _catalog;
    private StoryArchiveCatalogService? _catalogService;
    private StoryArchiveReplayAssembler? _replayAssembler;
    private StoryPresentationRunner? _runner;
    private IReadOnlyList<StoryArchiveEntryRecord>? _entries;
    private bool _isReplaying;

    public event Action? CatalogReady;
    public event Action? ReplayStarted;
    public event Action? ReplayCompleted;

    public IReadOnlyList<StoryArchiveEntryRecord> Entries => _entries ?? Array.Empty<StoryArchiveEntryRecord>();
    public bool IsReplaying => _isReplaying;

    private void Start()
    {
        _catalog = Resources.Load<StoryArchiveCatalogDefinition>(ArchiveCatalogResourcesPath);
        if (_catalog == null)
        {
            Debug.LogWarning("[StoryArchive] Archive catalog not found. Theater mode is empty.");
            _entries = Array.Empty<StoryArchiveEntryRecord>();
            CatalogReady?.Invoke();
            return;
        }

        var root = GameSessionRoot.EnsureInstance();
        if (root == null)
        {
            Debug.LogError("[StoryArchive] GameSessionRoot not found.");
            return;
        }

        var assemblyService = new DialogueAssemblyService(
            Resources.LoadAll<DialogueSequenceDefinition>("_Game/Content/Definitions/DialogueSequences")
                .Select(NarrativeRuntimeContentAdapter.ToSpec)
                .ToArray(),
            Resources.LoadAll<HeroLoreDefinition>("_Game/Content/Definitions/HeroLore")
                .Select(NarrativeRuntimeContentAdapter.ToSpec)
                .ToArray());

        _catalogService = new StoryArchiveCatalogService();
        _replayAssembler = new StoryArchiveReplayAssembler(assemblyService);
        _runner = GetComponentInChildren<StoryPresentationRunner>();

        var progress = root.SessionState.StoryDirector?.Progress ?? NarrativeProgressRecord.Empty;
        _entries = _catalogService.BuildCatalog(NarrativeRuntimeContentAdapter.ToSpec(_catalog), progress);

        CatalogReady?.Invoke();
    }

    public void RequestReplay(StoryArchiveEntryRecord entry)
    {
        if (_isReplaying || _replayAssembler == null || _runner == null)
        {
            return;
        }

        if (!entry.Unlocked)
        {
            Debug.LogWarning($"[StoryArchive] Entry '{entry.EventId}' is locked.");
            return;
        }

        var requests = _replayAssembler.BuildReplayBatch(entry);
        if (requests.Count == 0)
        {
            Debug.Log($"[StoryArchive] Entry '{entry.EventId}' replay policy is Skip.");
            return;
        }

        _isReplaying = true;
        ReplayStarted?.Invoke();

        _runner.Enqueue(requests, () =>
        {
            _isReplaying = false;
            ReplayCompleted?.Invoke();
        });
    }
}
