using UnityEngine;

namespace SM.Content;

[CreateAssetMenu(menuName = "SM/Definitions/Narrative/Chapter Beat", fileName = "chapter_beat_")]
public sealed class ChapterBeatDefinition : ScriptableObject
{
    [SerializeField] private string _id = string.Empty;
    [SerializeField] private string _chapterId = string.Empty;
    [SerializeField] private string _siteId = string.Empty;
    [SerializeField] private int _nodeIndex;
    [SerializeField] private string _beatLabel = string.Empty;
    [SerializeField] private float _tensionTarget;
    [SerializeField] private float _reliefTarget;
    [SerializeField] private float _humorTarget;
    [SerializeField] private float _catharsisTarget;

    public string Id => _id;
    public string ChapterId => _chapterId;
    public string SiteId => _siteId;
    public int NodeIndex => _nodeIndex;
    public string BeatLabel => _beatLabel;
    public float TensionTarget => _tensionTarget;
    public float ReliefTarget => _reliefTarget;
    public float HumorTarget => _humorTarget;
    public float CatharsisTarget => _catharsisTarget;
}
