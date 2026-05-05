# ADR-0024 내러티브 인간 중심 reskin과 캐릭터 IP 운영

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-04
- 소스오브트루스: `docs/04_decisions/adr-0024-narrative-human-centric-reskin.md`
- 관련문서:
  - `docs/04_decisions/adr-0007-thirdparty-asset-policy.md`
  - `docs/04_decisions/adr-0022-narrative-architecture.md`
  - `docs/02_design/narrative/index.md`
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/narrative/faction-conflict-matrix.md`
  - `docs/02_design/deck/character-lore-registry.md`

## 문맥

현재 narrative SoT는 4종족(Human/Beastkin/Undead/Relicborn) × 4클래스 매트릭스, 심장로 단일 메커니즘, lead 4명 영구 상실 결말, 5챕터 grim 일관 톤으로 짜여 있다. 단일 캠페인 RPG 시각으로는 단단하지만, 다음 세 가지 새 컨텍스트와 충돌이 드러났다.

- **3D 에셋 결정이 `P09_Modular_Humanoid`로 정렬됨**. humanoid baseline mesh + 의상·머리·prop swap 구조이므로, Beastkin(귀·꼬리·손발 형태 차이)과 Relicborn(격자/수정 비인간형 시각) 종족의 in-engine 표현이 어색한 우회 없이는 불가능하다.
- **게임 정체성이 캐릭터 수집·성장 중심 서브컬쳐 친화 운영형 게임으로 확정됨** (`memory/project_genre_subculture.md`). 원신·명일방주·블루아카이브·스타레일·페그오 어디도 메인 lead를 영구 상실로 닫지 않는다. 가챠 운영에서 SS rarity 캐릭터를 영구 빼는 결말은 IP 수명을 끝낸다.
- **세계관 톤 가드레일이 인간 본성의 어둠을 외부 메커니즘 탓으로 회피하는 구조**. 심장로가 모든 적대감을 증폭한다는 설정은 정화 재판·종족 학살 같은 인간이 인간에게 가한 폭력을 외부 누출 탓으로 만든다. 도덕적으로 깨끗하지만 정직하지 않다.

이와 별개로 race × class 매트릭스 자체는 가챠 banner 운영에 깔끔한 collection 자리수이며, lead 4 시점 캐릭터 구조와 6 세력쌍 충돌 매트릭스는 보존 가치가 높다. reskin은 이 자산을 깨지 않고 위 충돌만 해결해야 한다.

## 결정

다음 다섯 가지를 한 reskin 단위로 묶어 적용한다.

1. **4 race를 4 인간 세력으로 재정의한다.** 모든 playable·recruitable 캐릭터는 인간이며, 종족 구분이 아니라 출신 세력·문화·신앙으로 정체성을 가진다. race × class 매트릭스 16+4 cell 자리수와 ID 명명 패턴(`hero_*`)은 보존한다. `faction_*` ID는 일부 변경한다. `race_*` 개념은 폐기하고 `culture_*` 또는 `affiliation_*` 같은 신원축으로 재정의한다.

2. **lead 4명의 영구 상실 결말을 회수 가능 결말로 전환한다.** Echo Savant의 격자 영구 잔류, Grave Hexer의 가장 오래된 기억 헌납, Dawn Priest의 사제 휘장 반납, Pack Raider의 씨족 분열 — 메인 캠페인 결말의 무게는 유지하되, post-launch character event에서 회수 가능한 형태(은퇴·유배·잠적·봉인 + 회신 hook)로 표현한다. "되돌릴 수 없는 상실"의 자발성·고결성 일변도를 깨고, 억울한 상실·잘못된 판단·개인 약점에서 비롯된 비용을 최소 1명에게 부여한다.

3. **lead 4명의 inner struggle을 외부 조건이 아니라 인생관 차이로 재정의한다.** 현재 Pack Raider(씨족 분열)와 Grave Hexer(기억 헌납)는 외부에서 일어나는 일을 받아들이는 캐릭터로, Dawn(신앙 vs 진실)·Echo(과거 약속 vs 현재 자유)에 비해 자기 자신과의 싸움이 약하다. 4 lead 모두 자기 인생관이 곧 정치 노선이 되도록 재정의하여, 같은 시대 같은 사건을 다르게 보는 다층성을 확보한다.

4. **삼국지의 구조적 패턴만 흡수하고 표면 어휘는 차용하지 않는다.** 흡수 항목: (a) 같은 사건 다른 시점의 다층 평가, (b) 동맹의 합종연횡, (c) 인물 결함이 곧 매력, (d) 후일담의 길이가 곧 운영 수명, (e) 인생관 차이 = 정치 노선. 차용 금지: 위·촉·오의 직접 명명, 유비/조조/손권 등 실명 모티프 직역, 진삼국무쌍 계열 시각 어휘.

5. **인간 본성의 어둠을 외부 메커니즘 탓으로 회피하지 않는 비트를 최소 1곳 추가한다.** 심장로가 적대감을 증폭한다는 메커니즘은 보존하되, 정화 재판이나 종족 학살 같은 행위가 "심장로가 없었어도 일어났을 것"임을 인정하는 명시적 비트(대사·기록·캐릭터 회상 중 하나)를 ch2~ch3 어딘가에 박는다. 이 비트는 retcon 금지 규칙으로 격상한다.

## 검토한 대안

| option | description | pros | cons | verdict |
| --- | --- | --- | --- | --- |
| `option_a_keep_races_with_p09` | 종족 narrative 유지 + P09 humanoid로 Beastkin/Relicborn을 우회 표현 | 종족 다양성 한 컷 매력 유지, narrative rewrite 불필요 | 어색한 우회(Beastkin은 마스크·전통복 인간, Relicborn은 보디페인트), 시각 식별성 약함, 캐릭터 IP 운영 표면 좁음, 인간 어둠 회피 약점 미해결 | reject |
| `option_b_drop_p09_for_race_assets` | P09 포기, 4종족 별도 에셋팩 | 종족 다양성 유지 | 에셋 비용 폭증, 일러스트 작가 다양성 확보 어려움, prototype 스코프 초과, 가챠 카드 양식 표준에서 이탈 | reject |
| `option_c_human_centric_reskin` | 4 race → 4 인간 세력 reskin + 삼국지 구조 흡수 + 회수 가능 결말 + 인간 어둠 정직 비트 | 에셋 정합, 캐릭터 운영 IP 보존, 가챠 일러스트 친화, lead inner struggle 강화, world bible 약점(인간 어둠 회피·자발적 희생 일변도) 자동 해소 | world bible / conflict matrix / campaign arc / lore registry 거의 전체 rewrite, 종족 다양성 한 컷 매력 상실, ch3 midpoint reveal 시각 충격 약화 | **accept** |
| `option_d_direct_three_kingdoms` | option_c + 위·촉·오 등 삼국지 표면 어휘 직접 차용 | 즉각 인지도, 마케팅 hook 명료 | 진삼국무쌍·코이히메·신장의 야망 등 기존 IP와 차별화 어려움, 한국 시장 IP로서 정체성 약함, 모티프 라이선스 회색 지대 | reject |
| `option_e_partial_reskin_only_race` | race만 reskin, 결말·톤·인생관은 원안 유지 | rewrite 범위 작음 | 영구 상실 결말 + 가챠 운영 충돌 미해결, lead inner struggle 약점 미해결, 향후 또 다른 reskin ADR 필요 | reject (분리하면 후속 reskin 시 narrative가 또 흔들려 두 번 비용) |

## 결과

채택 구조의 장점:

- P09 모듈러가 의상·머리·prop swap이 곧 세력 정체성이 되어, 16+4 cell 시각 표현이 일관된다.
- 캐릭터 운영 IP가 보존되어 launch 이후 character event·외전·후일담 운영의 자리수가 확보된다.
- "심장로가 모든 적대감 원인"이라는 도덕적으로 깨끗한 회피가 깨지면서, 인간이 인간에게 가한 폭력을 정직히 다룰 표면이 생긴다.
- lead 4 inner struggle이 모두 인생관 차이 = 정치 노선이 되어, 같은 사건을 다층 평가하는 character event가 자연스럽게 가능해진다.
- 가챠 banner 자연 확장 자리수가 살아 있다 (16 + Relicborn 후속 4 + post-launch 추가).
- ADR-0022(narrative-architecture)의 코드 layer 결정과 충돌하지 않는다. 0022는 "어디에 어떤 타입을 둔다"는 코드 결정, 본 ADR은 "누가 무엇을 한다"는 콘텐츠 결정.
- ADR-0007(thirdparty-asset-policy)와 정렬된다. P09를 first-class 에셋으로 받기로 한 결정의 자연 후속.

감수할 비용:

- `docs/02_design/narrative/world-building-bible.md`, `faction-conflict-matrix.md`, `campaign-story-arc.md`, `chapter-beat-sheet.md`, `master-script.md`, `docs/02_design/deck/character-lore-registry.md`가 거의 전체 rewrite다.
- `faction_*` ID 일부 변경. 단 `CharacterDefinition.Id`는 race-neutral 명명(`warden`, `priest`, `raider` 등)이라 보존 가능. `HeroLoreDefinition.HeroId`는 `hero_*` 패턴 보존하되 일부 의미·소속을 갱신.
- `race_relicborn`과 4 Relicborn `UnitArchetypeDefinition` 후보는 폐기하고 인간 4번째 세력의 `culture_*` 정의로 대체. `runtime_decision: deferred-runtime`인 4 캐릭터 자산이 아직 만들어지지 않은 점이 비용 회피에 다행이다 (`character-lore-registry.md`).
- ch3 midpoint reveal의 시각적 충격(Relicborn 각성)이 약해진다. 충격을 다른 축(정치적 폭로, 가문 비밀, 신앙의 거짓)으로 이전해야 한다.
- 종족별 어조 가드레일(공명·진동·파장 / 바람·피·이빨 등)을 폐기하고, 출신 지역·계급·신앙 기반 어조 가드레일을 새로 정의해야 한다.
- 톤 가드레일과 거점/일상 무대 추가는 본 ADR 범위 밖으로 분리하지만, reskin 결과로 자연스럽게 후속 ADR 필요성이 가시화된다.

## retcon 금지 규칙 갱신

`world-building-bible.md`의 retcon 금지 규칙은 본 ADR 적용 후 다음으로 대체한다.

- 심장로는 기억을 에너지로 변환하는 장치이며 적대감을 증폭하는 부작용이 있다. 이 기능은 변경하지 않는다.
- 4 세력 간 적대는 심장로 누출이 가속할 뿐, 인간 본성의 어둠 그 자체는 외부 메커니즘 탓이 아니다. 정화 재판·학살·계급 폭력은 심장로가 없었어도 발생했을 사건으로 다룬다.
- main conflict는 순환의 종결로 닫되, lead 4명은 회수 가능한 결말(은퇴·유배·잠적·봉인+회신 hook)로 운영 가능 상태로 남긴다.
- 모든 playable·recruitable 캐릭터는 인간이다. 비인간형 적대 생물(괴수·언데드 변종·격자 부산물)은 적대 mob으로만 등장하고 캐릭터로 승격하지 않는다.
- 세계는 단일 대륙이며, 외부 대륙은 후속작 hook으로만 암시한다.

## 후속 작업

본 ADR을 baseline으로 다음 순서로 narrative 디렉터리 rewrite를 진행한다.

1. **4 인간 세력의 명명·어조·문화 정의 (root 레이어)** — `faction_*` ID, 출신 지역, 계급 구조, 신앙, 어조 가드레일, 색채. 이게 모든 하위 문서의 기반이라 가장 먼저 확정.
2. `world-building-bible.md` rewrite — 4 세력 정의, 연대기, 지역, 유물 taxonomy, retcon 금지 규칙 갱신.
3. `faction-conflict-matrix.md` rewrite — 6 세력쌍을 인간 정치 갈등으로 재정의, 합종연횡 패턴 강화.
4. `campaign-story-arc.md` rewrite — lead 4 인생관 차이, 회수 가능 결말, 인간 어둠 정직 비트 추가, ch3 midpoint reveal 축 이전.
5. `character-lore-registry.md` rewrite — lead 4 + sub-antagonist 캐릭터화 + background 캐릭터에 character event hook 1줄씩.
6. `chapter-beat-sheet.md`, `master-script.md`는 위 5단계 정합 후 갱신.

본 ADR 범위 밖으로 분리한 후속 ADR 후보:

- 거점/일상 무대 region 추가 (`region_*` 신규, character event 무대)
- 톤 가드레일 layer 분리 (main campaign grim / character event 자유 톤 허용)
- sub-antagonist 캐릭터 운영 정책 (적대자도 collection 자리)

## 작성 지침

- 본 ADR은 콘텐츠 방향 결정이며, ADR-0022의 코드 layer 결정을 변경하지 않는다.
- narrative 문서 rewrite 시 본 ADR을 관련문서에 명시한다.
- "삼국지 표면 어휘 차용 금지"는 강한 가드레일이다. 명명·어조·인물 모티프에서 직역이 보이면 reject한다.
- 회수 가능 결말은 자발적 희생 일변도를 피한다. 4 lead 중 최소 1명은 억울하거나 잘못된 판단의 비용을 진다.
