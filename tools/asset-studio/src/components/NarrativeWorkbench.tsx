import { convertFileSrc } from "@tauri-apps/api/core";
import {
  AlertTriangle,
  BookOpenText,
  Clapperboard,
  FileText,
  FolderOpen,
  Gamepad2,
  ImageOff,
  Languages,
  Mic2,
  Music,
  PackageCheck,
  Palette,
  RefreshCw,
  Search,
  Users,
  Volume2,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";

import { compactPath, formatDate } from "../lib/format";
import {
  getNarrativeIndex,
  openAssetExternal,
  resolveBackdropImage,
  resolveCueTrack,
} from "../lib/ipc";
import type {
  NarrativeIndex,
  NarrativeLine,
  NarrativeSequence,
  ResolvedBackdrop,
  ResolvedCue,
  SceneAudio,
  SceneVisual,
} from "../types";
import "./NarrativeWorkbench.css";

export function NarrativeWorkbench() {
  const [index, setIndex] = useState<NarrativeIndex | null>(null);
  const [query, setQuery] = useState("");
  const [selectedSequenceId, setSelectedSequenceId] = useState<string | null>(null);
  const [selectedLineId, setSelectedLineId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [liveOnly, setLiveOnly] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const next = await getNarrativeIndex();
      setIndex(next);
      setSelectedSequenceId((current) => {
        if (current && next.sequences.some((sequence) => sequence.id === current)) {
          return current;
        }
        return next.sequences[0]?.id ?? null;
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const selectedSequence = useMemo(() => {
    if (!index || !selectedSequenceId) {
      return null;
    }
    return index.sequences.find((sequence) => sequence.id === selectedSequenceId) ?? null;
  }, [index, selectedSequenceId]);

  useEffect(() => {
    if (!selectedSequence) {
      setSelectedLineId(null);
      return;
    }
    setSelectedLineId((current) => {
      if (current && selectedSequence.lines.some((line) => line.id === current)) {
        return current;
      }
      return selectedSequence.lines[0]?.id ?? null;
    });
  }, [selectedSequence]);

  const selectedLine = useMemo(() => {
    if (!selectedSequence || !selectedLineId) {
      return null;
    }
    return selectedSequence.lines.find((line) => line.id === selectedLineId) ?? null;
  }, [selectedLineId, selectedSequence]);

  const filteredSequences = useMemo(() => {
    if (!index) {
      return [];
    }
    const needle = query.trim().toLowerCase();
    return index.sequences.filter((sequence) => {
      if (liveOnly && sequence.reachability !== "live") {
        return false;
      }
      if (needle && !sequenceSearchText(sequence).includes(needle)) {
        return false;
      }
      return true;
    });
  }, [index, query, liveOnly]);

  const topSpeakers = index?.speakers.slice(0, 6) ?? [];

  return (
    <div className="narrative-workbench">
      <aside className="narrative-panel narrative-scenes">
        <div className="narrative-panel__heading">
          <div>
            <h2>Story Voice</h2>
            <p>{index ? `${index.totalLines.toLocaleString()} source lines` : "Loading index"}</p>
          </div>
          <button className="icon-button" type="button" onClick={refresh} disabled={loading}>
            <RefreshCw className={loading ? "spin" : ""} size={16} />
          </button>
        </div>

        <label className="search-box">
          <Search size={17} />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search story lines"
          />
        </label>

        <label className="narrative-live-toggle">
          <input
            type="checkbox"
            checked={liveOnly}
            onChange={(event) => setLiveOnly(event.target.checked)}
          />
          <Gamepad2 size={14} />
          <span>인게임에 실제 적용된 scene만 보기</span>
        </label>

        {index ? <NarrativeSummary index={index} /> : null}

        {error ? (
          <div className="narrative-error">
            <AlertTriangle size={15} />
            <span>{error}</span>
          </div>
        ) : null}

        <div className="narrative-sequence-list">
          {filteredSequences.map((sequence) => (
            <button
              aria-pressed={sequence.id === selectedSequence?.id}
              className={
                sequence.id === selectedSequence?.id
                  ? "narrative-sequence is-selected"
                  : "narrative-sequence"
              }
              key={sequence.id}
              type="button"
              onClick={() => {
                setSelectedSequenceId(sequence.id);
                setSelectedLineId(sequence.lines[0]?.id ?? null);
              }}
            >
              <span className="narrative-sequence__title">
                {sequence.title || sequence.presentationKey}
              </span>
              <span className="narrative-sequence__id">{sequence.presentationKey}</span>
              <span className="narrative-sequence__chips">
                <VisualChip meta={sequence.meta} />
                <AudioChip meta={sequence.meta} />
                <StatusPill tone={reachabilityTone(sequence.reachability)}>
                  {reachabilityLabel(sequence.reachability)}
                </StatusPill>
                <StatusPill tone={unityTone(sequence.unity.status)}>{sequence.unity.label}</StatusPill>
                <StatusPill tone={sequence.jaReadyCount === sequence.lineCount ? "good" : "warn"}>
                  JP {sequence.jaReadyCount}/{sequence.lineCount}
                </StatusPill>
                <StatusPill tone={sequence.voiceGeneratedCount > 0 ? "good" : "neutral"}>
                  Voice {sequence.voiceGeneratedCount}/{sequence.lineCount}
                </StatusPill>
              </span>
            </button>
          ))}
        </div>

        {topSpeakers.length > 0 ? (
          <section className="narrative-speakers">
            <h3>
              <Users size={14} />
              Speaker Load
            </h3>
            {topSpeakers.map((speaker) => (
              <div className="speaker-row" key={speaker.speakerId}>
                <span>{speaker.alias || speaker.speakerId}</span>
                <em>{speaker.lineCount}</em>
              </div>
            ))}
          </section>
        ) : null}
      </aside>

      <main className="narrative-panel narrative-lines">
        {selectedSequence ? (
          <>
            <div className="narrative-lines__header">
              <div>
                <h2>{selectedSequence.title || selectedSequence.presentationKey}</h2>
                <p>{selectedSequence.artifactSlug || selectedSequence.presentationKind}</p>
              </div>
              <div className="narrative-lines__stats">
                <Metric icon={<BookOpenText size={15} />} value={selectedSequence.lineCount} label="KO" />
                <Metric icon={<Languages size={15} />} value={selectedSequence.jaReadyCount} label="JP" />
                <Metric icon={<Volume2 size={15} />} value={selectedSequence.voiceGeneratedCount} label="Voice" />
              </div>
            </div>

            <div className="narrative-status-row">
              <StatusPill tone={unityTone(selectedSequence.unity.status)}>
                {selectedSequence.unity.label}
              </StatusPill>
              <StatusPill tone="neutral">{selectedSequence.presentationKind}</StatusPill>
              {selectedSequence.voiceRoleCount > 0 ? (
                <StatusPill tone="good">Voice Role {selectedSequence.voiceRoleCount}</StatusPill>
              ) : (
                <StatusPill tone="neutral">No Voice Role</StatusPill>
              )}
            </div>

            <div className="narrative-table-wrap">
              <table className="narrative-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Speaker</th>
                    <th>Emotion</th>
                    <th>KO Source</th>
                    <th>JP TTS</th>
                    <th>Voice</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedSequence.lines.map((line) => (
                    <tr
                      className={line.id === selectedLine?.id ? "is-selected" : ""}
                      key={line.id}
                      onClick={() => setSelectedLineId(line.id)}
                    >
                      <td>{line.lineIndex}</td>
                      <td>
                        <strong>{line.speakerAlias}</strong>
                        <span>{line.speakerId}</span>
                      </td>
                      <td>{line.emotionId || "-"}</td>
                      <td>{line.koText}</td>
                      <td>
                        <StatusPill tone={line.ttsTextStatus === "ready" ? "good" : "warn"}>
                          {line.ttsTextStatus === "ready" ? "Ready" : "Pending"}
                        </StatusPill>
                      </td>
                      <td>
                        <StatusPill tone={line.voiceStatus === "generated" ? "good" : "neutral"}>
                          {line.voiceStatus === "generated" ? "Generated" : "Missing"}
                        </StatusPill>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : (
          <div className="narrative-empty">
            <FileText size={28} />
            <p>No narrative sequence selected.</p>
          </div>
        )}
      </main>

      <NarrativeDetail sequence={selectedSequence} line={selectedLine} index={index} />
    </div>
  );
}

function NarrativeSummary({ index }: { index: NarrativeIndex }) {
  return (
    <section className="narrative-summary">
      <Metric icon={<FileText size={15} />} value={index.totalSequences} label="Scenes" />
      <Metric icon={<Gamepad2 size={15} />} value={index.liveSequences} label="인게임" />
      <Metric icon={<PackageCheck size={15} />} value={index.unityMatchedSequences} label="Unity" />
      <Metric icon={<Languages size={15} />} value={index.jaReadyLines} label="JP Ready" />
      <Metric icon={<Mic2 size={15} />} value={index.voiceGeneratedLines} label="Voiced" />
    </section>
  );
}

function NarrativeDetail({
  sequence,
  line,
  index,
}: {
  sequence: NarrativeSequence | null;
  line: NarrativeLine | null;
  index: NarrativeIndex | null;
}) {
  const [openError, setOpenError] = useState<string | null>(null);

  useEffect(() => {
    setOpenError(null);
  }, [line?.id]);

  if (!sequence || !line || !index) {
    return (
      <aside className="narrative-panel narrative-detail narrative-empty">
        <FileText size={28} />
        <p>Select a story line.</p>
      </aside>
    );
  }

  const meta = metaEntries(sequence.meta);

  const openVoice = () => {
    if (!line.voiceAssetPath) {
      return;
    }
    void openAssetExternal(line.voiceAssetPath).catch((err: unknown) => {
      setOpenError(err instanceof Error ? err.message : String(err));
    });
  };

  return (
    <aside className="narrative-panel narrative-detail">
      <section className="narrative-detail__section">
        <h2>Line</h2>
        <p className="narrative-ko-text">{line.koText}</p>
        <dl>
          <dt>Speaker</dt>
          <dd>{line.speakerAlias}</dd>
          <dt>Speaker ID</dt>
          <dd>{line.speakerId}</dd>
          <dt>Emotion</dt>
          <dd>{line.emotionId || "-"}</dd>
          <dt>Emote</dt>
          <dd>{line.emoteId || "-"}</dd>
          <dt>Line ID</dt>
          <dd>{line.lineId}</dd>
          <dt>Text Key</dt>
          <dd title={line.textKey}>{line.textKey}</dd>
        </dl>
      </section>

      <section className="narrative-detail__section">
        <h2>TTS</h2>
        <dl>
          <dt>Source</dt>
          <dd>KO</dd>
          <dt>Voice Locale</dt>
          <dd>JA</dd>
          <dt>JP Text</dt>
          <dd>{line.jaTtsText || "Pending translation"}</dd>
          <dt>Voice Key</dt>
          <dd title={line.voiceKey}>{line.voiceKey}</dd>
          <dt>Voice Role</dt>
          <dd>{line.voiceRole || "-"}</dd>
          <dt>Status</dt>
          <dd>{line.voiceStatus === "generated" ? "Generated" : "Missing"}</dd>
        </dl>
        {line.voiceAssetPath ? (
          <button className="command-button narrative-open" type="button" onClick={openVoice}>
            <FolderOpen size={15} />
            <span>Open Voice</span>
          </button>
        ) : null}
        {openError ? <p className="narrative-open-error">{openError}</p> : null}
      </section>

      <section className="narrative-detail__section">
        <h2>Sequence</h2>
        <dl>
          <dt>ID</dt>
          <dd>{sequence.sequenceId}</dd>
          <dt>Presentation</dt>
          <dd>{sequence.presentationKey}</dd>
          <dt>Artifact</dt>
          <dd>{sequence.artifactSlug || "-"}</dd>
          <dt>In-Game</dt>
          <dd>
            {reachabilityLabel(sequence.reachability)}
            {sequence.liveMoment ? ` · ${sequence.liveMoment}` : ""}
          </dd>
          <dt>Unity</dt>
          <dd>{sequence.unity.label}</dd>
          <dt>Unity Lines</dt>
          <dd>
            {sequence.unity.lineCount}/{sequence.lineCount}
          </dd>
        </dl>
        {sequence.unity.assetPath ? (
          <p className="path-line">
            <FolderOpen size={15} />
            <span>{compactPath(sequence.unity.assetPath)}</span>
          </p>
        ) : null}
      </section>

      <VisualSection meta={sequence.meta} />

      <AudioSection meta={sequence.meta} />

      {meta.length > 0 ? (
        <section className="narrative-detail__section">
          <h2>Meta</h2>
          <dl>
            {meta.map(([key, value]) => (
              <span className="narrative-meta-pair" key={key}>
                <dt>{key}</dt>
                <dd>{value}</dd>
              </span>
            ))}
          </dl>
        </section>
      ) : null}

      <section className="narrative-detail__section">
        <h2>Index</h2>
        <dl>
          <dt>Source</dt>
          <dd>{index.source}</dd>
          <dt>Hash</dt>
          <dd>{index.sourceHash || "-"}</dd>
          <dt>Seed</dt>
          <dd>{formatDate(index.seedModifiedMs)}</dd>
        </dl>
      </section>
    </aside>
  );
}

function Metric({
  icon,
  value,
  label,
}: {
  icon: ReactNode;
  value: number;
  label: string;
}) {
  return (
    <div className="narrative-metric">
      {icon}
      <strong>{value.toLocaleString()}</strong>
      <span>{label}</span>
    </div>
  );
}

function StatusPill({ tone, children }: { tone: string; children: ReactNode }) {
  return <span className={`status-pill status-pill--${tone}`}>{children}</span>;
}

function unityTone(status: string): string {
  if (status === "matched") {
    return "good";
  }
  if (status === "line_mismatch") {
    return "warn";
  }
  if (status === "missing") {
    return "danger";
  }
  return "neutral";
}

function reachabilityLabel(state: string): string {
  if (state === "live") {
    return "인게임";
  }
  if (state === "wired_unbuilt") {
    return "배선·미빌드";
  }
  return "미적용";
}

function reachabilityTone(state: string): string {
  if (state === "live") {
    return "good";
  }
  if (state === "wired_unbuilt") {
    return "warn";
  }
  return "neutral";
}

function sequenceSearchText(sequence: NarrativeSequence): string {
  return [
    sequence.sequenceId,
    sequence.presentationKey,
    sequence.presentationKind,
    sequence.artifactSlug,
    sequence.title,
    sequence.speakers.join(" "),
    sequence.lines.map((line) => `${line.speakerAlias} ${line.speakerId} ${line.koText}`).join(" "),
  ]
    .join(" ")
    .toLowerCase();
}

function metaEntries(meta: Record<string, unknown> | null): Array<[string, string]> {
  if (!meta || typeof meta !== "object") {
    return [];
  }
  return Object.entries(meta)
    .filter(([key]) => key !== "visual" && key !== "audio")
    .map(([key, value]) => [key, String(value)]);
}

function sceneVisual(meta: Record<string, unknown> | null): SceneVisual | null {
  if (!meta || typeof meta !== "object") {
    return null;
  }
  const raw = (meta as Record<string, unknown>).visual;
  if (!raw || typeof raw !== "object") {
    return null;
  }
  return raw as SceneVisual;
}

function visualTone(tier: string): string {
  switch (tier) {
    case "T4":
      return "danger";
    case "T3":
      return "warn";
    case "T2":
      return "good";
    default:
      return "neutral";
  }
}

function backdropLabel(backdrop: string | null): string {
  if (!backdrop) {
    return "없음 (포트레잇만)";
  }
  const [mode, id] = backdrop.split(":");
  if (mode === "shared") {
    return `공용 · ${id ?? ""}`;
  }
  if (mode === "bespoke") {
    return `전용 일러 · ${id ?? ""}`;
  }
  return backdrop;
}

function kindLabel(kind: string | null): string {
  switch (kind) {
    case "env":
      return "환경 배경 (캐릭터 ref 없음)";
    case "char":
      return "캐릭터 CG (ref 필수)";
    case "prop":
      return "오브젝트";
    default:
      return "—";
  }
}

function VisualChip({ meta }: { meta: Record<string, unknown> | null }) {
  const visual = sceneVisual(meta);
  if (!visual) {
    return null;
  }
  const bespoke = visual.backdrop?.startsWith("bespoke") ?? false;
  return (
    <StatusPill tone={visualTone(visual.tier)}>
      {visual.tier}
      {bespoke ? " ✦" : ""}
    </StatusPill>
  );
}

function backdropSourceLabel(source: string | null): string {
  if (source === "cg") {
    return "정전 자산";
  }
  if (source === "output") {
    return "raw 후보";
  }
  return "";
}

function sceneAudio(meta: Record<string, unknown> | null): SceneAudio | null {
  if (!meta || typeof meta !== "object") {
    return null;
  }
  const raw = (meta as Record<string, unknown>).audio;
  if (!raw || typeof raw !== "object") {
    return null;
  }
  return raw as SceneAudio;
}

function registerLabel(register: string): string {
  switch (register) {
    case "politics-weight":
      return "정치-무게";
    case "growth-emotion":
      return "성장-정서";
    case "warmth-humor":
      return "유머-온기";
    default:
      return register;
  }
}

function registerTone(register: string): string {
  switch (register) {
    case "politics-weight":
      return "warn";
    case "growth-emotion":
      return "good";
    case "warmth-humor":
      return "neutral";
    default:
      return "neutral";
  }
}

function AudioChip({ meta }: { meta: Record<string, unknown> | null }) {
  const audio = sceneAudio(meta);
  if (!audio) {
    return null;
  }
  return <StatusPill tone={registerTone(audio.register)}>{registerLabel(audio.register)}</StatusPill>;
}

function AudioSection({ meta }: { meta: Record<string, unknown> | null }) {
  const audio = sceneAudio(meta);
  const cueId = audio?.cue_id ?? "";
  const [resolved, setResolved] = useState<ResolvedCue | null>(null);

  useEffect(() => {
    if (!cueId) {
      setResolved(null);
      return;
    }
    let disposed = false;
    resolveCueTrack(cueId)
      .then((next) => {
        if (!disposed) {
          setResolved(next);
        }
      })
      .catch(() => {
        if (!disposed) {
          setResolved(null);
        }
      });
    return () => {
      disposed = true;
    };
  }, [cueId]);

  if (!audio) {
    return null;
  }

  return (
    <section className="narrative-detail__section">
      <h2>
        <Music size={15} /> Audio (BGM)
      </h2>
      {resolved?.exists && resolved.path ? (
        <audio
          controls
          preload="none"
          style={{ width: "100%", marginBottom: "8px" }}
          src={convertFileSrc(resolved.path)}
        />
      ) : (
        <p style={{ margin: "0 0 8px", opacity: 0.65, fontSize: "12px" }}>
          <Volume2 size={13} /> 미생성 cue · {cueId || "—"}
        </p>
      )}
      <dl>
        <dt>Register</dt>
        <dd>
          <StatusPill tone={registerTone(audio.register)}>{registerLabel(audio.register)}</StatusPill>
          {audio.curated ? "" : " (추론)"}
        </dd>
        <dt>Mood</dt>
        <dd>{audio.mood}</dd>
        <dt>Cue</dt>
        <dd>
          {audio.cue_id}
          {audio.reuse === "bespoke" ? " · 전용" : " · 공용"}
        </dd>
        {audio.leitmotif ? (
          <>
            <dt>Leitmotif</dt>
            <dd>{audio.leitmotif}</dd>
          </>
        ) : null}
        {audio.note ? (
          <>
            <dt>Note</dt>
            <dd>{audio.note}</dd>
          </>
        ) : null}
      </dl>
    </section>
  );
}

function VisualSection({ meta }: { meta: Record<string, unknown> | null }) {
  const visual = sceneVisual(meta);
  const backdrop = visual?.backdrop ?? null;
  const [resolved, setResolved] = useState<ResolvedBackdrop | null>(null);
  const [resolving, setResolving] = useState(false);

  useEffect(() => {
    if (!backdrop) {
      setResolved(null);
      setResolving(false);
      return;
    }
    let disposed = false;
    setResolving(true);
    resolveBackdropImage(backdrop)
      .then((next) => {
        if (!disposed) {
          setResolved(next);
        }
      })
      .catch(() => {
        if (!disposed) {
          setResolved(null);
        }
      })
      .finally(() => {
        if (!disposed) {
          setResolving(false);
        }
      });
    return () => {
      disposed = true;
    };
  }, [backdrop]);

  if (!visual) {
    return null;
  }

  const openImage = () => {
    if (resolved?.path) {
      void openAssetExternal(resolved.path);
    }
  };

  return (
    <section className="narrative-detail__section">
      <h2>
        <Clapperboard size={15} /> Visual
      </h2>
      {backdrop ? (
        <div className="narrative-backdrop">
          {resolving ? (
            <div className="narrative-backdrop__placeholder">
              <RefreshCw className="spin" size={16} />
              <span>일러 조회 중</span>
            </div>
          ) : resolved?.exists && resolved.path ? (
            <button
              className="narrative-backdrop__thumb"
              type="button"
              onClick={openImage}
              title="원본 일러 열기"
            >
              <img src={convertFileSrc(resolved.path)} alt="" />
              <span className={`narrative-backdrop__badge narrative-backdrop__badge--${resolved.source ?? "none"}`}>
                {backdropSourceLabel(resolved.source)}
              </span>
            </button>
          ) : (
            <div className="narrative-backdrop__placeholder narrative-backdrop__placeholder--missing">
              <ImageOff size={18} />
              <span>미생성{resolved?.id ? ` · ${resolved.id}` : ""}</span>
            </div>
          )}
        </div>
      ) : null}
      <dl>
        <dt>Tier</dt>
        <dd>
          <StatusPill tone={visualTone(visual.tier)}>{visual.tier}</StatusPill>
          {visual.curated ? "" : " (추론)"}
        </dd>
        <dt>Medium</dt>
        <dd>{visual.medium}</dd>
        <dt>Backdrop</dt>
        <dd>{backdropLabel(visual.backdrop)}</dd>
        <dt>Kind</dt>
        <dd>
          {kindLabel(visual.kind)}
          {visual.subjects && visual.subjects.length > 0 ? ` · ${visual.subjects.join(", ")}` : ""}
        </dd>
        <dt>Motion</dt>
        <dd>{visual.motion}</dd>
        <dt>LUT</dt>
        <dd>
          <Palette size={13} /> {visual.lut}
        </dd>
        {visual.note ? (
          <>
            <dt>Note</dt>
            <dd>{visual.note}</dd>
          </>
        ) : null}
      </dl>
    </section>
  );
}
