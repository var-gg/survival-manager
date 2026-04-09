# 세력 충돌 행렬

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/faction-conflict-matrix.md`
- 관련문서:
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/deck/hero-expansion-roadmap.md`

## 목적

세력 쌍별 오해, 실제 진실, 충돌축, encounter link를 한눈에 정리한다. 이 문서는 세력 갈등의 관계형 source of truth다.

## 왜 별도 문서가 필요한가

world bible은 각 세력의 단독 설명을 소유하고, campaign arc는 chapter-level 진행을 소유한다. faction pair의 충돌 관계는 별도 매트릭스로 분리해야 중복이 줄어든다.

## 충돌 행렬 상세

### Kingdom Remnant vs Beastkin Clans

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 국경 약탈과 보복 |
| 숨겨진 진실 | 둘 다 Heartforge 파편에 반응해 오판하고 있다 |
| encounter_link | `weakside_dive`, `protect_carry` |
| first_resolution_window | `chapter_1_site_2` |

**구체적 오해 에피소드**: 왕국 초소가 야수족 사냥대를 약탈자로 오인해 선제 사격한다. 야수족은 팩 토템이 짓밟힌 것에 대한 보복으로 초소를 습격한다. 양측 모두 '먼저 공격한 건 상대편'이라고 믿는다. 실제로는 Heartforge 파편이 인접한 곳에서 양측의 경계 본능을 동시에 증폭시켰고, 평소라면 조우만 하고 지나갔을 상황이 전투로 비화했다.

**해소 계기**: site_wolfpine_trail에서 원정대가 야수족 팩 우두머리와 교전한 뒤, Pack Raider가 왕국측 Dawn Priest에게 토템의 의미를 설명한다. 동시에 근처 Heartforge 파편이 양측의 적대감을 자극하고 있었음을 원정대가 발견한다. 첫 번째 '오해의 증거'가 확보된다.

### Kingdom Remnant vs Undead Remnant

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 이단과 정화 |
| 숨겨진 진실 | 왕국이 과거 봉인 오용의 주범이다 |
| encounter_link | `bastion_front`, `mark_execute` |
| first_resolution_window | `chapter_2_site_1` |

**구체적 오해 에피소드**: 왕국 사제 계급은 언데드의 존재 자체를 '영원한 질서'에 대한 모독으로 간주한다. 정화 재판에서 언데드 기억 용기(memory vessel)를 악마의 유물로 판정하고 파괴한다. 언데드 입장에서는 이것이 동료의 기억을 살해하는 것과 같다. 양측의 적대감은 신앙 vs 존재 보존이라는 근본적 가치 충돌에 기반한다.

**해소 계기**: site_sunken_bastion에서 원정대가 요새 지하의 기록 보관소를 발견한다. 왕국 학자들이 80년 전 봉인 격자 파편을 방어 장치로 전용한 기록이 나온다. Dawn Priest가 자신의 신앙이 은폐의 도구였음을 처음 인지하는 순간이다. 언데드의 Grave Hexer는 이 기록을 왕국의 죄를 묻는 게 아니라, 모두가 Heartforge에 속았음을 증명하는 증거로 사용한다.

### Kingdom Remnant vs Relicborn

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 유적 침입자 탄압 |
| 숨겨진 진실 | Relicborn는 왕국보다 먼저 이 땅을 지키고 있었다 |
| encounter_link | `protect_carry`, `control_cleanse` |
| first_resolution_window | `chapter_4_site_1` |

**구체적 오해 에피소드**: Relicborn이 각성하면서 격자 복원을 시도하자, 왕국 군은 이를 유적에서 나온 적대적 존재의 침공으로 판단한다. 특히 Relicborn의 장벽/정화 능력이 왕국의 방어 진지를 무력화하는 것처럼 보이기 때문에 위협 판정이 올라간다. 왕국은 자신들이 전용한 격자 파편이 Relicborn의 것이었다는 사실을 모른다.

**해소 계기**: site_glass_forest에서 왕국 군이 사용하는 방어 장치가 Relicborn 격자 파편임이 물리적으로 증명된다. Aegis Sentinel이 장치를 만지자 격자 패턴이 반응하고, Dawn Priest는 자신이 숭배하던 '신성 유물'이 약탈품이었음을 받아들여야 한다. 왕국의 오용이 격자 풍화의 주범이었다는 마지막 퍼즐이 맞춰진다.

### Beastkin Clans vs Undead Remnant

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 사냥터 오염과 영역 분쟁 |
| 숨겨진 진실 | 둘 다 Heartforge 공명에 다른 방식으로 반응할 뿐이다 |
| encounter_link | `tempo_swarm`, `sustain_grind` |
| first_resolution_window | `chapter_3_site_1` |

**구체적 오해 에피소드**: 야수족은 묘역 주변에서 사냥감이 변이하거나 사라지는 것을 언데드의 오염 탓으로 본다. 언데드는 야수족의 사냥이 묘역 주변의 기억 잔향을 교란한다고 본다. 양측 모두 상대가 자신의 생존 기반을 훼손한다고 믿는다.

**해소 계기**: site_ruined_crypts에서 기억 용기(memory vessel)가 깨지면서 고대의 기억이 투사된다. 그 기억 속에서 야수족과 언데드의 조상이 같은 공간에서 평화롭게 공존하던 장면이 나온다. Heartforge 가동 이전의 세계. Pack Raider와 Grave Hexer가 동시에 그 기억을 목격하면서, 적대감이 인위적으로 만들어진 것임을 처음 의심한다.

### Beastkin Clans vs Relicborn

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 본능적 적대 (이질적 존재) |
| 숨겨진 진실 | Relicborn 격자가 야수족 본능 공명을 완화할 수 있다 |
| encounter_link | `weakside_dive`, `control_cleanse` |
| first_resolution_window | `chapter_4_site_2` |

**구체적 오해 에피소드**: Relicborn가 각성하면서 방출하는 격자 공명이 야수족의 본능을 자극한다. 야수족은 이것을 포식자의 위협으로 해석하고 공격적으로 반응한다. Relicborn는 격자 복원에 집중하느라 야수족의 반응을 이해하지 못한다.

**해소 계기**: site_starved_menagerie에서 Prism Seeker가 격자 공명의 주파수를 조절해 야수족의 본능 반응을 완화하는 것에 성공한다. Trail Scout가 이를 목격하고, Relicborn의 격자가 적대의 도구가 아니라 평화의 도구일 수 있음을 씨족에 보고한다. 야수족과 Relicborn 사이에 최초의 협력이 시작된다.

### Undead Remnant vs Relicborn

| 항목 | 내용 |
|---|---|
| 표면 갈등 | 유적 소유권 분쟁 |
| 숨겨진 진실 | 둘 다 과거 봉인 실패의 후손이다 |
| encounter_link | `summon_pressure`, `control_cleanse` |
| first_resolution_window | `chapter_3_site_2` |

**구체적 오해 에피소드**: 언데드는 묘역과 유적이 자신들의 기억 저장소라고 주장한다. Relicborn가 각성하면서 같은 유적의 격자를 복원하려 하자, 이를 기억 저장소에 대한 침탈로 간주한다. Relicborn는 격자 복원이 우선이고, 언데드의 기억 보관은 격자 운영에 부수적이라고 본다.

**해소 계기**: site_bone_orchard에서 Echo Savant가 각성하면서 Relicborn의 기록 속에 저장된 고대 기억을 공유한다. 그 기억 속에서 최초의 봉인 작업에 언데드의 조상이 자발적으로 참여했음이 드러난다. 언데드와 Relicborn는 적이 아니라 같은 봉인 프로젝트의 서로 다른 후손이었다. Grave Hexer와 Echo Savant의 공감이 이 관계의 전환점이 된다.

## chapter별 해소 상태 상세

| conflict_pair | chapter_1 | chapter_2 | chapter_3 | chapter_4 | chapter_5 |
|---|---|---|---|---|---|
| kingdom-beastkin | seed: 국경 교전 목격, 첫 오해 인지 | escalate: 왕국 정화 재판의 여파로 야수족 영역 압박 심화 | partial resolve: 고대 기억에서 공존 발견, 적대감 원인 의심 시작 | method debate: 왕국은 정화(속죄), 야수족은 저항(동료를 가두는 것) — Pack Raider가 개인적 대가를 수용하며 합의 | sacrifice accepted: Pack Raider가 분열된 씨족으로 귀환, 영역 존중은 합의되었으나 개인 비용은 미해결 |
| kingdom-undead | hidden: 원정대 내부에서 불신만 존재 | seed+escalate: 요새 기록에서 오용 증거 발견, Dawn Priest 균열 | partial resolve: 기억 투사에서 왕국 책임 명확화, 상호 비난에서 공동 피해자 인식으로 | aligned: 양측 모두 정화 지지 (기억 보존 + 죄의식 해소), Aldric 간섭에 공동 대응 | resolved with cost: 왕국이 과거 오용 인정, Dawn Priest는 사제 정체성을 상실 |
| kingdom-relicborn | hidden | hidden | teaser: Relicborn 각성 목격, 왕국은 적대적 존재로 분류 | confrontation->negotiate: 방어 장치가 격자 파편임 증명, 일부 Relicborn은 동족 희생에 저항 — Echo Savant의 자원으로 논쟁 종결 | stabilized with absence: 왕국이 파편 반환과 격자 복원 협력, Echo Savant는 심부에 잔류 |
| beastkin-undead | seed: 사냥터 변이 발견, 상대 탓 | hidden: 양측 모두 왕국과의 갈등에 집중 | partial resolve: 고대 공존 기억 공유 목격 | method debate: 야수족은 봉인(즉각 안전), 언데드는 정화(재발 방지) — Grave Hexer의 희생이 야수족을 설득 | resolved with loss: Grave Hexer가 기원 기억을 상실, 양측 기억 보호 협력 합의 |
| beastkin-relicborn | hidden | hidden | teaser: Relicborn 각성 시 본능 반응 경험 | method debate->ally: 야수족은 희생 비용에 불신, Prism Seeker가 격자 복원의 실질적 이득을 증명하며 첫 협력 | stabilized: 격자가 본능 완화에 도움됨 확인, 신뢰 형성 |
| undead-relicborn | hidden | hidden | reveal: 같은 봉인 프로젝트의 후손임이 밝혀짐 | negotiate: 양측 정화 수용하나 정화 이후 기억 관리권 논쟁 — 공동 관리 제안으로 수렴 | stabilized: 공동 관리 체계 합의, 잔류 기억의 공동 수호 |

### Post-Midpoint Method Debate (ch4~5)

중반 이후 정화/봉인/파괴 논쟁이 각 세력 쌍의 갈등에 어떻게 매핑되는지를 정리한다.

| faction pair | purify position | seal position | destroy position | resolution |
|---|---|---|---|---|
| kingdom-beastkin | 왕국은 정화 지지 (속죄의 의미), 야수족은 저항 (동료를 가두는 셈) | — | — | Pack Raider가 개인적 대가를 수용하며 정화에 합의 |
| kingdom-undead | 양측 정화 지지 (기억 보존 + 죄의식 해소) | — | — | 이해가 일치하여 자연 합의 |
| kingdom-relicborn | 왕국은 정화 지지, 일부 Relicborn은 저항 (동족 희생이 필요하므로) | — | — | Echo Savant가 자원하면서 논쟁 종결 |
| beastkin-undead | — | 야수족은 봉인 선호 (즉각 안전 확보) | — | Grave Hexer의 기원 기억 희생이 야수족을 설득하여 정화로 수렴 |
| beastkin-relicborn | — | — | 야수족은 희생 비용 자체에 불신 | Prism Seeker가 격자 복원의 실질적 이득(본능 완화)을 증명하여 협력 시작 |
| undead-relicborn | 양측 정화 수용하나 정화 이후 기억 관리권에서 대립 | — | — | 공동 관리 체계 제안으로 수렴 |

## 작성 지침

- 세력 단독 lore는 `docs/02_design/narrative/world-building-bible.md`를 복제하지 않는다.
- gameplay link는 encounter family 혹은 answer lane ID로 연결한다.
- chapter resolution 표현은 동일한 어휘 세트를 유지한다: `hidden`, `seed`, `escalate`, `teaser`, `partial resolve`, `confrontation`, `negotiate`, `mutual understanding`, `deepen empathy`, `mostly resolved`, `stabilized`.
