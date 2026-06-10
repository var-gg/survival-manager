# SFX sound style bible

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-10
- 소스오브트루스: `docs/03_architecture/sfx-sound-style-bible.md`
- 관련문서:
  - `docs/03_architecture/sfx-hook-id-contract.md`
  - `docs/03_architecture/content-pipeline.md`
  - `docs/03_architecture/battle-actor-wrapper-and-asset-intake-seam.md`

## 목적

이 문서는 `sfx-hook-id-contract.md`의 hook id를 실제 SFX 생성 주문서로 바꿀 때 사용하는 사운드 스타일 기준, MOSS 프롬프트 템플릿, layered 합성 워크플로우를 정의한다. 대량 생성은 이 문서의 범위가 아니다. 검증 샘플은 스타일 확인용으로만 만들고, Unity import 대상은 별도 승인 후 이동한다.

## 기준 질감

레퍼런스 질감은 FF10~12 계열의 오케스트라 판타지다. 전투음은 저역 무게와 금속 foley가 살아 있어야 하고, 마법음은 웅장하지만 UI/전투 판독성을 해치지 않는 규모로 유지한다. 비프음, 16비트 레트로, 현대 스마트폰 알림음, 노골적인 신스 EDM 리드, 보이스/성대성 샤우트는 기본 금지다.

## 카테고리별 음색 규칙

| 카테고리 | 질감 | 길이 기준 | 공간계 |
| --- | --- | --- | --- |
| `combat_common.impact` | 소재별 variant 단위로 생성. 살/천, 가죽, 금속, 목재 block을 한 prompt에 섞지 않음 | 0.25~0.9 s | raw에 가깝게, room tail 0.15~0.35 s 이하 |
| `skill.layered.magic` | charge, cast, impact, tail 4 layer. 오케스트라 저역, 금속성 마법 입자, 공명 tail | 최종 1.2~2.4 s | tail/spatial layer에서만 hall/plate 처리 |
| `skill.physical` | 무기 재질과 몸체 충격을 우선. 과도한 마법 shimmer 금지 | 0.45~1.4 s | 짧은 room, wet 낮게 |
| `status.apply` | 상태 식별용 색채. burn/bleed/root/silence 등은 고유 재질을 짧게 노출 | 0.35~1.0 s | 짧고 건조하게, hard control만 약한 tail 허용 |
| `ui` | 고역 클릭/천/금속 소형 foley. 전투 저역과 충돌하지 않음 | 0.08~0.45 s | 거의 dry, reverb 금지 또는 매우 짧게 |

## 음역대 분리

| 영역 | 담당 | 규칙 |
| --- | --- | --- |
| 30~80 Hz | 대형 impact의 sub weight | 전투/보스급에만 제한적으로 사용. UI/status에서는 제거 |
| 80~180 Hz | 전투 무게 중심 | melee hit, guard, death, heavy magic impact의 주 저역 |
| 180~600 Hz | body/warmth | 과하면 탁해지므로 layered 합성 후 250~350 Hz를 점검 |
| 600 Hz~2 kHz | 재질 식별 | 금속, 나무, 천, 마법 core tone. UI와 충돌 시 UI 우선 |
| 2~5 kHz | transient/readability | hit/click/cast onset. harshness가 생기면 3.5~4.5 kHz를 좁게 감쇠 |
| 5~10 kHz | shimmer/air | 마법 tail과 UI sparkle. 전투 impact에는 과사용 금지 |

## 공간계와 마스터 기준

공간계는 일괄 마스터 effect가 아니라 layer 합성 단계의 마지막 질감으로 포함한다. 단발 SFX는 raw에 가깝게 유지한다.

| 처리 | 기본값 | 적용 |
| --- | --- | --- |
| project sample rate | 48 kHz | MOSS output 및 후처리 target |
| render format | WAV PCM 16-bit | 검증 샘플과 Unity import 후보 |
| raw layer peak | -12~-6 dBFS | 합성 headroom 확보 |
| final peak | -3 dBFS 이하 | clipping 방지. 최종 import 전 -1 dBTP 재검토 |
| reverb pre-delay | 12~35 ms | layered magic tail/spatial layer |
| reverb decay | 0.8~1.6 s | 일반 magic/status |
| reverb decay large | 1.6~2.4 s | boss/large spell만 |
| reverb low cut | 160~220 Hz | tail 저역 번짐 방지 |
| reverb high cut | 7~9 kHz | 얇은 hiss 방지 |
| dry/wet target | wet -18~-10 dB | tail layer만. impact/UI에는 기본 미적용 |

## Hook id와 layer id

runtime hook id는 `sfx-hook-id-contract.md`를 따른다. `SkillDefinitionAsset.SfxHookId`는 base id인 `sfx.skill.<skill_id>`만 저장하며, 최종 생성물은 `sfx.skill.<skill_id>.cast`와 `sfx.skill.<skill_id>.impact`다.

Layered magic 작업에서는 runtime hook을 늘리지 않고 작업용 layer id를 별도로 둔다.

| 작업용 layer id | 용도 | 최종 hook |
| --- | --- | --- |
| `work.sfx.skill.<skill_id>.charge.raw` | cast 전 상승/응집 | `sfx.skill.<skill_id>.cast` |
| `work.sfx.skill.<skill_id>.cast.raw` | 손/무기에서 발화되는 transient | `sfx.skill.<skill_id>.cast` |
| `work.sfx.skill.<skill_id>.impact.raw` | 명중 순간의 body/sub/transient | `sfx.skill.<skill_id>.impact` |
| `work.sfx.skill.<skill_id>.tail.raw` | 공간계, shimmer, magic residue | `sfx.skill.<skill_id>.impact` |

검증용 preview 파일은 네 layer를 모두 이어 붙인 `preview.sfx.skill.<skill_id>.full`로 만들 수 있지만, Unity runtime hook으로 쓰지 않는다.

## MOSS 공통 프롬프트 구조

MOSS request는 `prompt`, `negative_prompt`, `seconds`, `output_name`, `num_inference_steps`, `cfg_scale`, `seed`를 사용한다. 기본값은 검증 단계에서 `num_inference_steps=80~120`, `cfg_scale=3.5~5.0`, `seconds<=3.0`을 권장한다. 같은 prompt wording을 비교할 때만 seed를 고정하고, 후보 질감 다양화가 목적이면 seed를 바꾼다.

MOSS의 `prompt`는 게임 설계 문장이 아니라 구체적인 소리 캡션이다. 첫 문장에 물리 소스와 동작을 넣고, 뒤에 재질, 강도, 거리, 공간감을 짧게 붙인다. ai-infra MOSS pipeline이 내부에서 `duration: <seconds>s`를 덧붙이므로 prompt 본문에 `Duration about ...`을 반복하지 않는다.

```text
{SOURCE_ACTION}. {MATERIAL_TIMBRE}. {DYNAMICS_AND_DISTANCE}. {SPACE_AND_SHAPE}.
```

### 모델 prompt에 넣지 않는 정보

아래 정보는 생성 sidecar 또는 asset studio review metadata에 남기고 MOSS prompt에는 넣지 않는다.

| 정보 | 위치 | 이유 |
| --- | --- | --- |
| `hook_id`, `work.sfx.*`, Unity asset 경로 | metadata | 모델이 소리 의미로 해석할 수 없는 토큰이다 |
| `where_used`, `review_focus`, 한국어 검수 설명 | metadata | 검수자 문맥이지 소리 캡션이 아니다 |
| `FF10~12`, `JRPG`, `game-ready`, `readable` | style bible / review metadata | 모델에는 추상적이고 반복되면 질감이 수렴한다 |
| 긴 `Avoid: ...` 블록 | `negative_prompt` | `prompt` 안의 Avoid는 positive text로 들어가 혼선을 만든다 |
| `Duration about ...` | `seconds` request | pipeline이 duration suffix를 자동으로 붙인다 |

### 공통 negative_prompt

`negative_prompt`는 짧은 comma-separated 제외어로 유지한다. 카테고리별로 필요한 단어만 더한다.

```text
music, voice, spoken words, sci-fi laser, phone notification, 8-bit, chiptune, EDM lead, clipping, harsh digital distortion
```

### 생성 주문서 sidecar 필드

대량 생성 manifest 또는 Asset Studio sidecar는 prompt와 검수 문맥을 분리한다.

```json
{
  "runtime_hook_id": "sfx.combat.impact_damage",
  "hook_id": "sfx.combat.impact_damage.leather.light",
  "variant_key": "combat.impact.leather.light",
  "profile_key": "combat.impact.leather.light",
  "category": "combat_common.impact",
  "trigger": "Damage impact / Hit",
  "where_used": "BattleActorAudioSurface가 ImpactDamage cue를 받을 때 선택할 수 있는 leather/light 피격 variant 후보",
  "variant_role": "가죽 방어구에 닿는 가벼운 피격. 금속/목재/살 타격을 대신하지 않는다.",
  "review_focus": "가죽과 천의 짧은 접촉으로 들리는지, 차량 충돌/오함마/큰 금속 충돌이 아닌지 확인",
  "prompt": "A light leather armor hit, short cloth body thump, leather creak, close dry fantasy foley, one-shot.",
  "negative_prompt": "music, voice, vehicle crash, warhammer slam, trailer boom, explosion, sci-fi laser",
  "expected_audio_profile": {
    "duration_s": { "min": 0.25, "max": 0.7 },
    "attack_ms": { "max": 45 },
    "low_band_ratio": { "max": 0.35 },
    "leading_silence_ms": { "max": 50 }
  },
  "seconds": 0.8,
  "num_inference_steps": 100,
  "cfg_scale": 4.0,
  "seed": 6201
}
```

## 카테고리별 프롬프트 템플릿

### Combat impact

combat impact는 `sfx.combat.impact_damage` 공용 runtime hook 하나로 생성하지 않는다. 생성 단위는 소재별 variant다. 한 prompt에 가죽, 금속, 천, 뼈를 모두 넣으면 모델이 불분명한 잡음이나 과장된 trailer hit로 수렴하므로 금지한다.

| `variant_key` | prompt |
| --- | --- |
| `combat.impact.flesh.light` | `A light blunt hit on a humanoid body, short cloth thump, soft skin impact, close dry foley, one-shot.` |
| `combat.impact.leather.light` | `A light leather armor hit, short cloth body thump, leather creak, close dry fantasy foley, one-shot.` |
| `combat.impact.metal.light` | `A small metal armor tap from a light weapon hit, quick bright clink, tiny buckle rattle, close dry foley, one-shot.` |
| `combat.impact.wood_block.light` | `A light hit stopped by a wooden shield, short hollow wood knock, small leather strap movement, close dry foley, one-shot.` |

기본 템플릿:

```text
A {weight} {single_material} hit, {one_body_contact}, {one_material_detail}, close dry fantasy foley, one-shot.
```

권장 negative_prompt:

```text
music, voice, vehicle crash, warhammer slam, trailer boom, explosion, giant armor crash, sci-fi laser, electrical zap
```

권장 request:

```json
{
  "seconds": 0.7,
  "num_inference_steps": 100,
  "cfg_scale": 4.0,
  "seed": 6201
}
```

검수 기준:

- `flesh.light`: 금속 clink 없이 body/cloth 중심이다.
- `leather.light`: leather creak가 들리되 금속 장갑 충돌처럼 커지지 않는다.
- `metal.light`: 작고 짧은 clink이지 대형 갑옷 충돌이나 망치 타격이 아니다.
- `wood_block.light`: shield/block으로 읽히되 damage body thump와 섞이지 않는다.

### Layered magic charge

```text
A low magical energy charge gathers in the air, soft rising hum, tiny crystalline particles, restrained tension, no impact.
```

### Layered magic cast

```text
A spell releases from a staff tip, bright glass snap, quick metallic shimmer, short forward whoosh, clear attack onset.
```

### Layered magic impact

```text
A focused magical strike hits a target, compact low thump, sharp crystalline burst, short sparkling debris, close fantasy impact.
```

### Layered magic tail

```text
Soft magical resonance decays after a hit, airy shimmer, tiny glass particles fading in a stone hall, no new attack.
```

### Status apply

```text
A short {status_material} mark applies to a target, {status_motion}, compact tactile magical foley, close dry one-shot.
```

### UI

```text
Small parchment and brass UI click, light tactile tap, clear high-mid transient, close dry interface foley.
```

UI negative_prompt:

```text
music, voice, phone notification, 8-bit, chiptune, combat impact, explosion, harsh click
```

## Layered 합성 워크플로우

1. 매니페스트에서 `hook_id`, `source_asset_id`, `category`, `trigger`, `estimated_length`, `prompt`, `negative_prompt`, `where_used`, `review_focus`를 만든다. `prompt`는 MOSS용 소리 캡션만 담고, 나머지는 검수와 asset binding metadata로 유지한다.
2. layered skill은 `charge`, `cast`, `impact`, `tail` raw layer를 MOSS로 따로 생성한다. 각 layer의 prompt도 hook id 없이 물리 소리 캡션으로 작성한다.
3. raw layer를 `art-pipeline/sfx/validation/<date>/raw/` 또는 후보 batch의 `raw/`에 보관한다.
4. 각 layer의 leading silence를 20~60 ms 이하로 trim한다. tail layer는 앞 transient가 생기면 fade-in 80~120 ms로 눌러준다.
5. layer별 EQ를 적용한다.
   - charge: 80 Hz high-pass, 1~2 kHz 상승감을 유지
   - cast: 120 Hz high-pass, 2~5 kHz onset 유지
   - impact: 35~50 Hz high-pass, 80~180 Hz body 유지, 250~350 Hz muddiness 점검
   - tail: 180 Hz high-pass, 7~9 kHz low-pass, wet layer로 취급
6. 기본 배치:
   - charge start: 0.00 s
   - cast start: 0.45~0.70 s
   - impact start: 0.80~1.05 s
   - tail start: impact start + 0.03~0.10 s
7. 합성 gain staging:
   - charge: -10~-7 dB
   - cast: -7~-4 dB
   - impact: -4~-1 dB
   - tail: -16~-10 dB
8. 최종 파일은 `preview`와 runtime hook 후보를 분리한다.
   - `preview.sfx.skill.<skill_id>.full.wav`: 네 layer 검토용
   - `sfx.skill.<skill_id>.cast.wav`: charge + cast 중심
   - `sfx.skill.<skill_id>.impact.wav`: impact + tail 중심
9. QC를 통과한 후에만 Unity import 후보 폴더로 이동한다.

## 후처리 레시피

| 단계 | 처리 | 기준 |
| --- | --- | --- |
| trim | leading silence 제거 | one-shot은 20 ms 이하, magic charge는 자연 attack 유지 |
| fade | click 방지 | in 3~10 ms, out 20~120 ms. tail은 300 ms 이상 가능 |
| EQ | 카테고리별 대역 정리 | UI는 180 Hz 이하 제거, impact는 80~180 Hz 유지 |
| transient | impact/cast attack 보존 | 과하면 3~5 kHz narrow cut |
| reverb | layer로만 적용 | tail/spatial layer에만 wet 부여 |
| normalize | final peak 제한 | 검증 샘플은 -3 dBFS 이하 |
| export | WAV PCM 16-bit 48 kHz | metadata JSON과 함께 보관 |

## 배치 생성 운영 절차

배치 생성은 단발(unload) 모드가 아니라 keep-loaded 모드로만 돌린다. 단발 모드는 클립당 cold load ~20 s를 다시 내고, 무거운 데스크톱 세션에서는 load preflight(기본 free 12 GB)에 걸려 재로드가 거부될 수 있다.

```powershell
pwsh -File C:\projects\ai-infra\scripts\serve-sfx.ps1 -KeepLoaded -IdleUnloadSeconds 1800 `
    -MinLoadFreeVramMB 9000 -MinGenerateFreeVramMB 0
```

- MOSS는 어떤 설정에서도 VRAM peak가 풀(16.3 GB)에 닿는다. SFX 배치 중에는 다른 GPU 생성 작업(보이스/BGM/영상/이미지)을 돌리지 않는다. Unity 에디터는 켜져 있어도 동작하지만 데스크톱 GPU 점유만큼 느려진다.
- 데스크톱 점유가 커서 preflight가 막히면 Unity AssetImportWorker 프로세스(커맨드라인에 `AssetImportWorker` 포함)만 종료해 VRAM을 회수한다. 메인 에디터는 건드리지 않는다.
- 서버가 unload된 상태에서 free가 load 임계 아래면 재로드가 영구 거부된다. keep-loaded 세션 유지가 자산이다.

도구 체인은 다음 순서로 고정한다. raw WAV는 어느 단계에서도 수정하지 않는다.

| 단계 | 도구 | 산출 |
| --- | --- | --- |
| 생성 | `art-pipeline/scripts/generate_sfx_batch.py --manifest art-pipeline/sfx/manifest/<category>.json` | ai-infra `data/sfx/outputs/`에 wav + sidecar(매니페스트 메타 머지) |
| 자동 QC | `art-pipeline/scripts/qc_sfx.py <files> --write` | sidecar에 `qc_status`(red/yellow/green) + 측정값 기록 |
| 검수 사본 | `art-pipeline/scripts/reviewnorm_sfx.py <files>` | `<id>-reviewnorm.wav` 청취용 peak normalize 사본 |
| 사람 검수 | Asset Studio SFX 탭 | hook별 후보 청취, 합격 판정 |
| 승격 | `art-pipeline/scripts/promote_sfx.py <wav> [--note ...]` | `art-pipeline/sfx/approved/<hook_id>.wav` + sidecar (trim/fade/-3 dBFS 마스터) |

검수 노트: MOSS raw 출력은 극도로 작을 수 있다(-45 dBFS peak 실측). 검수는 반드시 reviewnorm 사본으로 듣는다. +40 dB대 게인은 noise floor도 같이 올리므로, 사본에서 들리는 hiss는 생성 결함이 아니라 normalize 부작용일 수 있다 — hiss 판정은 promote 후 -3 dBFS 마스터본 기준으로 한다.

## 검증 샘플 계획

대량 생성 전 검증 샘플은 아래 2개로 제한한다.

| sample | hook/work id | 목적 | 기준 |
| --- | --- | --- | --- |
| combat impact variant | `sfx.combat.impact_damage.leather.light` | 소재가 좁혀진 피격 variant 기준 확인 | raw에 가까움, 0.7 s 내외. 다른 소재를 대체하지 않음 |
| layered skill | `preview.sfx.skill.skill_prism_lance.full` | charge/cast/impact/tail 합성 기준 확인 | 4 raw layer + final preview |

선택 샘플이 더 필요할 때만 UI confirm one-shot을 추가한다. 이 경우도 0.3 s 이하, dry, 저역 없는 tactile click으로 제한한다.

## 평가 기준

| 항목 | 통과 기준 |
| --- | --- |
| 스타일 | FF10~12식 오케스트라 판타지 질감에 가깝고 레트로/비프가 없음 |
| 판독성 | cast, impact, tail의 시작점이 구분됨 |
| 믹스 | peak -3 dBFS 이하, clipping 없음, UI/전투 대역 분리 가능 |
| 공간계 | magic tail은 공간감을 주지만 impact body를 흐리지 않음 |
| variant 순도 | common cue 후보가 아니라 소재/강도 variant로 들림 |
| 재사용성 | prompt와 settings가 metadata로 남아 같은 hook 후보를 재생성 가능 |

## 보류

- story/dialogue line-level SFX는 MediaCueSheet 설계 이후에 추가한다.
- Unity audio mixer group, volume curve, runtime randomization은 이 문서가 아니라 audio import/runtime binding 단계에서 결정한다.
- 후보 A/B ranking 자동화는 보류. batch naming은 `generate_sfx_batch.py`의 `sm-<batch>-<hook>-s<seed>` 규칙을 쓴다.
