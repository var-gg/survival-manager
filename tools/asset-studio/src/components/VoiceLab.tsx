import { convertFileSrc } from "@tauri-apps/api/core";
import { AlertTriangle, Mic, Play, Power, RefreshCw } from "lucide-react";
import { useState } from "react";

import { generateVoice } from "../lib/ipc";
import type { EngineHealth, GenerateVoiceOutput } from "../types";
import "./VoiceLab.css";

interface Candidate {
  id: string;
  label: string;
  speakerId: string;
  direction: string;
  text: string;
  seed: number;
  exaggeration: number;
}

interface Take extends GenerateVoiceOutput {
  seed: number;
  exaggeration: number;
}

// Tier-1 in-game voices (Narrator + 단린 + 이빨바람 carry ~75% of in-game lines).
// JA lines mirror the real opening beats so they can be judged in context.
const INITIAL_CANDIDATES: Candidate[] = [
  {
    id: "narrator",
    label: "내레이터 (Narrator)",
    speakerId: "Narrator",
    direction: "지문/묘사 · 중저음 · 억양 절제 · 일정한 호흡 (라이트노벨 지문 톤)",
    text: "辺境の門は、内側から崩れ落ちていた。鉄の破片が、野まで飛び散っている。",
    seed: 777,
    exaggeration: 0.25,
  },
  {
    id: "dawn_priest",
    label: "단린 (hero_dawn_priest)",
    speakerId: "hero_dawn_priest",
    direction: "솔라룸 사제 · 정중한 존댓말 · 절제와 신념 · 흔들릴 때만 균열",
    text: "順番を…知らなければ、止めることはできないのです。",
    seed: 401,
    exaggeration: 0.4,
  },
  {
    id: "pack_raider",
    label: "이빨바람 (hero_pack_raider)",
    speakerId: "hero_pack_raider",
    direction: "변경 부족 전사 · 반말·직설·냉소 · 단린과 음색 대비가 핵심",
    text: "ここで戦ったのさ。お前たちと、俺たちがな。",
    seed: 42,
    exaggeration: 0.6,
  },
];

interface Props {
  health: EngineHealth[];
  onStartEngine: (engineId: string) => void;
}

export function VoiceLab({ health, onStartEngine }: Props) {
  const chatterbox = health.find((engine) => engine.id === "chatterbox");
  const online = chatterbox?.status === "online";

  const [candidates, setCandidates] = useState<Candidate[]>(INITIAL_CANDIDATES);
  const [takes, setTakes] = useState<Record<string, Take[]>>({});
  const [busy, setBusy] = useState<Record<string, boolean>>({});
  const [errors, setErrors] = useState<Record<string, string | null>>({});

  const patch = (id: string, next: Partial<Candidate>) =>
    setCandidates((list) => list.map((item) => (item.id === id ? { ...item, ...next } : item)));

  const handleGenerate = async (candidate: Candidate) => {
    setBusy((state) => ({ ...state, [candidate.id]: true }));
    setErrors((state) => ({ ...state, [candidate.id]: null }));
    try {
      const outputName = `audition_${candidate.id}_s${candidate.seed}_e${Math.round(
        candidate.exaggeration * 100,
      )}`;
      const output = await generateVoice({
        text: candidate.text,
        engine: "multilingual",
        language: "ja",
        seed: candidate.seed,
        exaggeration: candidate.exaggeration,
        cfgWeight: 0.5,
        outputName,
      });
      const take: Take = {
        ...output,
        seed: candidate.seed,
        exaggeration: candidate.exaggeration,
      };
      setTakes((state) => ({
        ...state,
        [candidate.id]: [take, ...(state[candidate.id] ?? [])].slice(0, 6),
      }));
    } catch (err) {
      setErrors((state) => ({
        ...state,
        [candidate.id]: err instanceof Error ? err.message : String(err),
      }));
    } finally {
      setBusy((state) => ({ ...state, [candidate.id]: false }));
    }
  };

  return (
    <div className="voice-lab">
      <div className={`voice-lab__status ${online ? "is-online" : "is-offline"}`}>
        <Mic size={16} />
        <span className="voice-lab__status-text">
          Chatterbox 엔진: <strong>{chatterbox ? chatterbox.status : "unknown"}</strong>
          {online
            ? " — multilingual / ja 로 생성합니다."
            : " — 생성하려면 먼저 엔진을 시작하세요 (첫 모델 로딩 ~1–2분)."}
        </span>
        {!online ? (
          <button
            type="button"
            className="voice-lab__start"
            onClick={() => onStartEngine("chatterbox")}
          >
            <Power size={14} />
            <span>Start Chatterbox</span>
          </button>
        ) : null}
      </div>

      <p className="voice-lab__hint">
        레퍼런스 없이 기본 보이스로 오디션합니다. <strong>seed</strong> 를 바꾸면 음색 정체성이,{" "}
        <strong>exaggeration</strong> 을 올리면 감정 강도가 달라지는지 비교하세요. 생성한 take 는
        아래에 쌓여 A/B 청취가 됩니다.
      </p>

      <div className="voice-lab__grid">
        {candidates.map((candidate) => {
          const candidateTakes = takes[candidate.id] ?? [];
          const isBusy = busy[candidate.id] ?? false;
          const error = errors[candidate.id] ?? null;
          return (
            <section className="voice-card" key={candidate.id}>
              <header className="voice-card__head">
                <h3>{candidate.label}</h3>
                <span className="voice-card__speaker">{candidate.speakerId}</span>
              </header>
              <p className="voice-card__direction">{candidate.direction}</p>

              <label className="voice-card__field">
                <span>일본어 대사 (JA)</span>
                <textarea
                  value={candidate.text}
                  rows={2}
                  onChange={(event) => patch(candidate.id, { text: event.target.value })}
                />
              </label>

              <div className="voice-card__params">
                <label>
                  <span>seed</span>
                  <input
                    type="number"
                    min={0}
                    value={candidate.seed}
                    onChange={(event) =>
                      patch(candidate.id, { seed: Math.max(0, Math.trunc(Number(event.target.value) || 0)) })
                    }
                  />
                </label>
                <label>
                  <span>exaggeration</span>
                  <input
                    type="number"
                    min={0}
                    max={2}
                    step={0.05}
                    value={candidate.exaggeration}
                    onChange={(event) =>
                      patch(candidate.id, { exaggeration: Number(event.target.value) || 0 })
                    }
                  />
                </label>
                <button
                  type="button"
                  className="voice-card__generate"
                  disabled={isBusy}
                  onClick={() => void handleGenerate(candidate)}
                >
                  {isBusy ? <RefreshCw className="spin" size={15} /> : <Play size={15} />}
                  <span>{isBusy ? "생성 중…" : "생성 + 재생"}</span>
                </button>
              </div>

              {error ? (
                <p className="voice-card__error">
                  <AlertTriangle size={14} />
                  <span>{error}</span>
                </p>
              ) : null}

              <div className="voice-card__takes">
                {candidateTakes.length === 0 ? (
                  <p className="voice-card__empty">아직 take 없음.</p>
                ) : (
                  candidateTakes.map((take, index) => (
                    <div className="voice-take" key={`${take.outputPath}-${index}`}>
                      <div className="voice-take__meta">
                        <span>seed {take.seed}</span>
                        <span>exag {take.exaggeration}</span>
                        <span>{take.audioDuration.toFixed(1)}s</span>
                      </div>
                      <audio src={convertFileSrc(take.outputPath)} controls />
                    </div>
                  ))
                )}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
