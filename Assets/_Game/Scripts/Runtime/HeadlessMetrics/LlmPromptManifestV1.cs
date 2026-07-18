namespace SM.HeadlessMetrics;

/// <summary>
/// prompt template, briefing, model snapshot, decoding 설정을 하나의 schema hash로 묶는 immutable manifest.
/// </summary>
public sealed record LlmPromptManifestV1(
    string PromptTemplateId,
    string PromptTemplate,
    string ColdStartBriefing,
    string ModelSnapshotId,
    string DecodingConfigCanonical)
{
    public const string WireSchemaVersion = "LlmWireV1";

    public string PromptSchemaHash => LlmWireCanonicalSerializer.Hash(this);
}
