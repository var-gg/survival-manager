# 캠페인 스토리 아크

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/campaign-story-arc.md`
- 관련문서:
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/02_design/deck/hero-expansion-roadmap.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`

## 목적

캠페인 전체의 로그라인, chapter 목적, 갈등 해소율, 엔딩과 후속작 hook를 고정한다. 이 문서는 chapter-level story truth의 기준이다.

## 로그라인

Ashen Gate 붕괴 이후 파견된 혼성 원정대가 전선 아래의 Worldscar를 따라 하강하면서, 인간/야수족/언데드의 전쟁이 매장된 Heartforge에 의해 재증폭되고 있음을 발견한다. 중반에 각성한 Relicborn는 침략자가 아니라 오래된 봉인의 수문장으로 드러나며, 최종 목표는 지배가 아니라 순환의 종결로 전환된다.

## 핵심 갈등과 결말 규칙

| 갈등 유형 | 내용 | 런치 해소율 |
|---|---|---:|
| **main conflict** | Heartforge의 기억 증폭 순환 | `100%` — 봉인/정화/정지로 완결 |
| **surface faction wars** | 인간-야수족-언데드 전선 | `>= 80%` — 대부분 정리, 정치 재편 1개 남김 |
| **setting mystery** | Heartforge 기원, Relicborn 존재 이유 | `~70%` — 핵심 해명, 외부 대륙 신호 미해명 |

금지: "진짜 최종보스는 DLC/후속작"형 엔딩.

## 챕터 구조표

| chapter_id | dramatic_function | gameplay_function | key_reveal | resolved_hook | open_hook |
|---|---|---|---|---|---|
| `chapter_ashen_gate` | Exposition | squad loop/site rhythm onboarding | 위협의 실재 | 첫 단서 확보, 정찰 동맹 | 야수족의 진짜 동기 |
| `chapter_sunken_bastion` | Rising Action I | protect/mark pressure 학습, answer lane 구별 | 인간 진영 culpability | 요새 배신 증거 | 신앙 균열의 폭 |
| `chapter_ruined_crypts` | Midpoint Reversal | summon pressure + truth pivot | Relicborn reveal, 언데드 기억의 진실 | 언데드 오해 일부 해소 | Heartforge 전체 목적 |
| `chapter_glass_forest` | Crisis | mixed encounter mastery, full comp stress test | 모든 세력이 같은 상처를 다른 언어로 말함 | 4세력 공감 기반 형성 | 최종 적대체의 정체 |
| `chapter_heartforge_descent` | Climax + Denouement | 신규 시스템 금지, 결산과 payoff 극대화 | 4세력 이해관계 수렴 | main conflict closure | 정치 재편, Relicborn 분열, 외부 신호 |

## 챕터별 서사 목적 / 게임플레이 목적 매핑

| chapter_id | 서사적 목적 | 게임플레이 목적 | 신규 도입 | 챕터 종료 보상 |
|---|---|---|---|---|
| `chapter_ashen_gate` | 위협의 실재와 원정의 명분 제시. 인간/야수의 첫 오해를 완충 | 기본 squad synergy, site rhythm, reward cadence onboarding | 첫 specialist 전환 사례 | `hero_rift_stalker` |
| `chapter_sunken_bastion` | 인간 진영 culpability와 신앙/통제의 폭력을 드러냄 | protect/mark pressure 학습, answer lane 구별 강화 | specialist 2종 추가 | `hero_bastion_penitent`, `hero_pale_executor` |
| `chapter_ruined_crypts` | 언데드를 단순 악으로 보던 시각을 뒤집고 Relicborn 진실을 공개 | summon pressure, midpoint reversal, race 4 teaser | Relicborn 방어/증언 축 도입 | `hero_aegis_sentinel`, `hero_echo_savant` |
| `chapter_glass_forest` | 모든 세력이 같은 상처를 다른 언어로 말한다는 점을 보여줌 | mixed encounter mastery, full comp stress test | Relicborn 전열돌파/추적 축 완성 | `hero_shardblade`, `hero_prism_seeker` |
| `chapter_heartforge_descent` | 4세력의 이해관계를 수렴시켜 main conflict를 닫음 | 신규 시스템 금지, 결산과 payoff 극대화 | 최종 specialist와 endless gate | `hero_mirror_cantor`, `mode_endless_cycle` |

## 엔딩 / 에필로그 / endless

- **ending beat**: Heartforge 정화/봉인 후 4세력이 각자의 방식으로 세계를 수습하는 장면
- **credits 직전 card**: 원정대의 귀환과 남은 과제 암시
- **endless framing**: main conflict 종료 후 남은 파편/왜곡/기억 수습 임무. `mode_endless_cycle`로 3-site heat loop 운영

### 엔드게임 전환 구조

| 항목 | 설계 |
|---|---|
| 모드 ID | `mode_endless_cycle` |
| 해금 시점 | `site_worldscar_depths` extract 직후 |
| 구조 | 3-site cycle, site당 5노드, 총 15노드 1사이클 |
| narrative framing | main conflict 종료 후 남은 파편/왜곡/기억 수습 임무 |
| rule set | 기존 site kit 재사용 + heat mutator + mixed encounter families |
| canonical status | 에필로그적 containment loop. main ending을 다시 열지 않음 |
| reward focus | 후반 augment, recruit conversion residue, codex completion, cosmetics |
| 금지 | main conflict 재개방, 진엔딩 분리 판매 |

## 후속작 / DLC hook 정책

| hook_id | category | status_at_launch_end | followup_media |
|---|---|---|---|
| `hook_surface_realignment` | secondary conflict | open — 정치 재편 미완 | DLC or sequel |
| `hook_relicborn_schism` | secondary conflict | open — Relicborn 내부 분열 | DLC or sequel |
| `hook_far_signal` | external hook | open — 외부 대륙/원거리 신호 | sequel |

## 작성 지침

- node 단위 비트는 여기서 쓰지 말고 `chapter-beat-sheet.md`에 둔다.
- 세계관 원문 설명은 여기서 반복하지 말고 `world-building-bible.md`를 참조한다.
- resolved/open hook 표는 narrative release scope 판단의 source of truth다.
