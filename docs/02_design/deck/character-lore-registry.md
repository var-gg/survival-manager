# 캐릭터 열전 레지스트리

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-04
- 소스오브트루스: `docs/02_design/deck/character-lore-registry.md`
- 관련문서:
  - `docs/02_design/deck/hero-expansion-roadmap.md`
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/04_decisions/adr-0024-narrative-human-centric-reskin.md`

## 목적

모든 영웅의 canonical short bio, tier, beat budget, unresolved hook를 한곳에 정리한다. 장문 열전 문서가 생기더라도 본 문서가 registry SoT다. ADR-0024 reskin baseline 위에서 4 인간 세력(솔라룸/이리솔 부족/회상 결사/그물 결사)을 축으로 명명하며, 모든 캐릭터는 한국어 표시명 + 영문 별칭(legacy)을 병기한다.

## 레지스트리 규칙

- 영웅 1명당 1행을 유지한다.
- 장문 biography는 `docs/02_design/deck/heroes/hero-*.md`에 두고, 본 문서에서는 요약과 참조만 남긴다.
- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.
- 한국어 표시명을 본명으로, 영문 별칭은 ID와 legacy reference에만 사용한다.

## 영웅 표

| hero_id | 한국어 표시명 | 영문 별칭 | 세력 | class | gender | tier | beat_budget | canon_status | narrative_role | unresolved_hook |
| --- | --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `hero_iron_warden` | 철위 (鐵衛) | Iron Warden | 솔라룸 | Vanguard | M | `support` | 3 | `launch` | 질서의 얼굴, 초반 안전판 | — |
| `hero_crypt_guardian` | 묘직 (墓直) | Crypt Guardian | 회상 결사 | Vanguard | F | `support` | 3 | `launch` | 죽은 문명의 죄를 증언 | — |
| `hero_fang_bulwark` | 송곳벽 | Fang Bulwark | 이리솔 부족 | Vanguard | M | `background` | 1 | `launch` | 이리솔 부족 현장성의 상징 | `hook_fang_first_hunt` |
| `hero_oath_slayer` | 서검 (誓劍) | Oath Slayer | 솔라룸 | Duelist | F | `support` | 3 | `launch` | 솔라룸의 폭력성과 명분을 드러내는 칼 | — |
| `hero_pack_raider` | 이빨바람 | Pack Raider | 이리솔 부족 | Duelist | M | `lead` | 6 | `launch` | 이리솔 부족 측 주연 시점 | `hook_pack_treaty` |
| `hero_grave_reaver` | 묵괴 (墨壞) | Grave Reaver | 회상 결사 | Duelist | M | `background` | 1 | `launch` | 파괴된 충성의 잔재 | `hook_reaver_lost_friend` |
| `hero_longshot_hunter` | 원시 (遠矢) | Longshot Hunter | 솔라룸 | Ranger | M | `support` | 3 | `launch` | 원정 실무자의 시선 | — |
| `hero_trail_scout` | 숲살이 | Trail Scout | 이리솔 부족 | Ranger | F | `support` | 3 | `launch` | 유머와 경계의 완충재 | — |
| `hero_dread_marksman` | 냉시 (冷矢) | Dread Marksman | 회상 결사 | Ranger | M | `background` | 1 | `launch` | 회상 결사 실용주의의 흑백 | `hook_marksman_last_feeling` |
| `hero_dawn_priest` | 단린 (丹麟) | Dawn Priest | 솔라룸 | Mystic | F | `lead` | 6 | `launch` | 솔라룸 측 주연 시점 | `hook_faith_crack` |
| `hero_grave_hexer` | 묵향 (墨香) | Grave Hexer | 회상 결사 | Mystic | F | `lead` | 6 | `launch` | 회상 결사 측 주연 시점 | `hook_memory_debt` |
| `hero_storm_shaman` | 풍의 (風儀) | Storm Shaman | 이리솔 부족 | Mystic | F | `support` | 3 | `launch` | 이리솔 부족 공동체의 의례 | — |
| `hero_rift_stalker` | 틈사냥꾼 | Rift Stalker | 이리솔 부족 | Duelist | M | `background` | 1 | `launch` | 적-동맹 전환의 첫 사례 | `hook_rift_apprentice` |
| `hero_bastion_penitent` | 참회벽 | Bastion Penitent | 솔라룸 | Vanguard | F | `background` | 1 | `launch` | 솔라룸 내부 균열의 증거 | `hook_penitent_family` |
| `hero_pale_executor` | 백집행 (白執行) | Pale Executor | 회상 결사 | Ranger | M | `background` | 1 | `launch` | 회상 결사 실용주의의 흑백 | `hook_executor_dual_loyalty` |
| `hero_aegis_sentinel` | 방진 (方陣) | Aegis Sentinel | 그물 결사 | Vanguard | M | `support` | 3 | `launch` | 그물 결사의 문지기 | — |
| `hero_echo_savant` | 공한 (空閑) | Echo Savant | 그물 결사 | Ranger | M | `lead` | 6 | `launch` | 그물 결사 측 주연 시점 | `hook_memory_custody` |
| `hero_shardblade` | 편검 (片劍) | Shardblade | 그물 결사 | Duelist | F | `background` | 1 | `launch` | 중후반 전열 돌파 장치 | `hook_shardblade_secret_oath` |
| `hero_prism_seeker` | 광로 (光路) | Prism Seeker | 그물 결사 | Ranger | F | `background` | 1 | `launch` | 진실 추적자 | `hook_prism_outside_curiosity` |
| `hero_mirror_cantor` | 명음 (明音) | Mirror Cantor | 그물 결사 | Mystic | M | `support` | 3 | `launch` | 최종부 정합성 봉합자 | — |

## 실행 CharacterDefinition coverage

`HeroLoreDefinition`은 서사 registry이므로 20명 전원을 가진다. `CharacterDefinition`은 현재 실행 roster asset이므로 16개만 닫는다. `runtime_decision`이 `deferred-runtime`인 영웅은 lore/story registry에는 launch 후보로 남기되, `faction_lattice_order` enum과 대응 그물 결사 4종 archetype이 authoring되기 전에는 playable/recruitable 캐릭터로 승격하지 않는다.

현재 검증 기준은 아래와 같다.

- `HeroLoreDefinition.HeroId`: 위 20개 `hero_id`와 1:1로 맞춘다.
- `CharacterDefinition.Id`: `warden / guardian / bulwark / slayer / raider / reaver / hunter / scout / marksman / priest / hexer / shaman / rift_stalker / bastion_penitent / pale_executor / mirror_cantor` 16개 exact set으로 고정한다.
- `hero_mirror_cantor`(명음)는 lore registry에서는 그물 결사 인물이지만, 현재 실행 카탈로그에서는 그물 결사 종족 asset 없이 `mirror_cantor` specialist를 3-faction safe target에 고정한다. 그물 결사 runtime 승격 시 faction/archetype/character를 함께 migration한다.

| hero_id | HeroLoreDefinition asset | CharacterDefinition.Id | CharacterDefinition asset | runtime_decision |
| --- | --- | --- | --- | --- |
| `hero_iron_warden` | `hero_lore_iron_warden.asset` | `warden` | `character_warden.asset` | `runtime-core` |
| `hero_crypt_guardian` | `hero_lore_crypt_guardian.asset` | `guardian` | `character_guardian.asset` | `runtime-core` |
| `hero_fang_bulwark` | `hero_lore_fang_bulwark.asset` | `bulwark` | `character_bulwark.asset` | `runtime-core` |
| `hero_oath_slayer` | `hero_lore_oath_slayer.asset` | `slayer` | `character_slayer.asset` | `runtime-core` |
| `hero_pack_raider` | `hero_lore_pack_raider.asset` | `raider` | `character_raider.asset` | `runtime-core` |
| `hero_grave_reaver` | `hero_lore_grave_reaver.asset` | `reaver` | `character_reaver.asset` | `runtime-core` |
| `hero_longshot_hunter` | `hero_lore_longshot_hunter.asset` | `hunter` | `character_hunter.asset` | `runtime-core` |
| `hero_trail_scout` | `hero_lore_trail_scout.asset` | `scout` | `character_scout.asset` | `runtime-core` |
| `hero_dread_marksman` | `hero_lore_dread_marksman.asset` | `marksman` | `character_marksman.asset` | `runtime-core` |
| `hero_dawn_priest` | `hero_lore_dawn_priest.asset` | `priest` | `character_priest.asset` | `runtime-core` |
| `hero_grave_hexer` | `hero_lore_grave_hexer.asset` | `hexer` | `character_hexer.asset` | `runtime-core` |
| `hero_storm_shaman` | `hero_lore_storm_shaman.asset` | `shaman` | `character_shaman.asset` | `runtime-core` |
| `hero_rift_stalker` | `hero_lore_rift_stalker.asset` | `rift_stalker` | `character_rift_stalker.asset` | `runtime-specialist` |
| `hero_bastion_penitent` | `hero_lore_bastion_penitent.asset` | `bastion_penitent` | `character_bastion_penitent.asset` | `runtime-specialist` |
| `hero_pale_executor` | `hero_lore_pale_executor.asset` | `pale_executor` | `character_pale_executor.asset` | `runtime-specialist` |
| `hero_aegis_sentinel` | `hero_lore_aegis_sentinel.asset` | `aegis_sentinel` | 없음 | `deferred-runtime` |
| `hero_echo_savant` | `hero_lore_echo_savant.asset` | `echo_savant` | 없음 | `deferred-runtime` |
| `hero_shardblade` | `hero_lore_shardblade.asset` | `shardblade` | 없음 | `deferred-runtime` |
| `hero_prism_seeker` | `hero_lore_prism_seeker.asset` | `prism_seeker` | 없음 | `deferred-runtime` |
| `hero_mirror_cantor` | `hero_lore_mirror_cantor.asset` | `mirror_cantor` | `character_mirror_cantor.asset` | `runtime-specialist` |

## 핵심 NPC

| npc_id | 한국어 표시명 | 영문 별칭 | 소속 | status | narrative_role | first_appearance |
| --- | --- | --- | --- | --- | --- | --- |
| `npc_aldric` | 단현 스턴홀트 (丹玄) | 단현 스턴홀트 | 솔라룸 (사망, 심장로 잔류) | 심장로 내 잔류 의지 | 적대자. 솔라룸 초대 학자장, 격자 오용 주도자, 영원한 질서 교리 설계자 | ch2 기록 → ch3 기억 투사 → ch4 직접 간섭 → ch5 최종 적대자 |
| `npc_grey_fang` | 회조 (灰爪) | Grey Fang | 이리솔 부족 분리파 | 생존 | 이빨바람의 피형제이자 장로. ch1 보스 → ch4 분열 시 반대편(의구심 분리주의) | ch1 wolfpine_trail boss |
| `npc_ember_runner` | 연주 (燕走) | Ember Runner | 이리솔 부족 | 생존 | 이빨바람을 따르는 젊은 사냥꾼. 분열 소식 전달자. ch5 엔딩에서 어느 쪽을 택했는지 미정 — 외전 hook | ch4 starved_menagerie |

### 단현 스턴홀트 (`npc_aldric`) 상세

80년 전 솔라룸 초대 학자장(Scholar-Chief). 격자 파편을 최초로 발견하고 체계적 연구를 시작한 천재 연구자였다. 심장로의 존재를 처음으로 인지한 인물이며, 격자를 인류의 이익을 위해 제어할 수 있다고 진심으로 믿었다. 악을 위한 악이 아니라, 혼돈에서 질서를 만들겠다는 확신이 세계를 파괴한 경우다. 그의 오만은 선의에서 출발했고, 그래서 더 위험했다.

그가 설계한 교리는 영원한 질서(영원한 질서)의 기반이 되었다. 단린(丹麟, Dawn Priest)이 평생 섬긴 신앙 체계, 집정관 회의의 권위, 정화 재판의 정당성 — 모든 것이 단현의 이론에서 파생되었다. 솔라룸이 격자 파편을 성물로 위장하고 무기화한 구조도 그의 설계에 뿌리를 두고 있다.

40년 전, 격자를 직접 조작하려다 심장로 심층부에서 사망했다. 그러나 심장로가 그의 기억과 의지를 흡수했으며, 격자가 풍화될수록 그의 인격은 심장로 내부에서 점점 강해진다. 처음에는 파편적 기록으로만 존재하지만(ch2), 기억 투사로 형태를 갖추고(ch3), 오염된 피조물을 통해 말을 걸기 시작하며(ch4), ch5에서는 원정대와 직접 대면한다.

적대자로서의 단현은 단순한 악당이 아니다. 그의 주장 — "나는 혼돈에서 질서를 만들었다. 너희는 내가 세운 모든 것을 파괴하고 있다" — 은 질서에 대한 열망 자체로는 틀리지 않았다. 방법이 틀렸을 뿐이다. 그는 세계를 구하려 했고, 그 시도가 세계를 망가뜨렸으며, 죽어서도 자신의 실패를 인정하지 못한다. 단 그의 신학적 설계는 심장로가 없었어도 권력 구조의 폭력으로 기능했을 수 있다는 점이 ADR-0024 결정 5번 가드레일대로 명시된다 — 단현이 인간 어둠의 외부 메커니즘이 아니라, 인간 안의 질서 욕망의 극단적 표현이라는 점.

## Sub-antagonist 캐릭터

운영형 캐릭터 게임의 적대 진영 표면. 단일 적대자(단현)만 두면 캐릭터 가챠 banner 자리가 부족하므로 sub-antagonist 4명을 캐릭터 layer에 추가한다. 회조는 기존 NPC가 캐릭터로 승격된 케이스, 나머지 셋은 신규.

### 회조 (灰爪) — `npc_grey_fang`

이리솔 부족 분리파의 의구심 노선 장로. 이빨바람의 피형제로 같은 날 피의 서약을 나눈 사이. ch1에서 보스로 등장한 뒤 "이 분노가 내 것이 아닐 수 있다"고 인정하며 일시적 동맹을 허용했지만, ch4에서 씨족 장로 회의가 외부 결사 비밀 누설을 배신으로 규정하자 분리파의 중심에 선다. 이빨바람과는 적이 아니라 다른 길을 택한 형제 — 외전 화해 hook이 명시적으로 보존된다. 외양은 서리가 내린 회색 갈기와 짙은 흉터, 분리파 토템 휘장.

### 선영 (宣英) — `npc_lyra_sternfeld` (신규)

솔라룸 사제단 내부의 단현 신학 광신도. 단린(丹麟)의 사제 동료였으나, ch3 이후 심장로에서 단현의 잔류 의지가 부활하기 시작하자 그를 신적 존재로 숭배한다. 단린이 "스승의 스승과 싸운다"면, 선영은 "스승의 스승의 부활을 환영하는 자"다. 그녀는 심장로의 적대감 증폭을 신적 시험으로 해석하며, 정화 재판을 더 엄격히 집행해야 한다고 주장한다. 단현 직속 추종자이므로 ch4~5에서 단현의 의지를 매개하는 역할도 수행. 외양은 백금 사제복에 단현의 연구 문장을 추가한 재구성복, 격자 가루 화장으로 광신적 분위기 강조.

### 철피 (鐵皮) — `npc_iron_pelt` (신규)

이리솔 부족 강경 분리파의 노선 장로. 회조가 의구심 분리라면 철피는 "솔라룸은 영원한 적이며 동맹은 모욕이다"라는 강경 노선. ch4 분열 시 회조와도 노선 충돌하며, 철피는 회조를 "약한 분리주의자"라 비난한다. 이빨바람의 누설 행위를 결사 비밀 위반의 결정적 증거로 활용해 분리파 내부 권력을 장악한다. 외양은 솔라룸 갑옷 파편을 강제로 가공한 흉터 장식 — 적의 무기를 자기 의지로 변형한 상징성. 가챠 banner상으로는 이리솔 부족 적대 진영의 첫 번째 자리.

### 흑지 (黑紙) — `npc_black_vellum` (신규)

회상 결사 강경파 장로. 묵향(墨香, Grave Hexer)과 동시대 동료이며 Hollow가 되지 않은 가장 오래된 기억 보유자 중 하나. 솔라룸의 정화 재판이 회상 결사 조상을 처형한 사실을 절대 잊지 않으며, 솔라룸과의 화해 자체에 반대한다. 묵향이 "기억은 지키되 화해 가능"이라면 흑지는 "기억이 정의의 영원한 단죄"라는 노선. ch3~5에서 회상 결사 내부 분열을 일으키며, 정화 의식 자체에도 의구심을 표한다. 외양은 검은 양피지에 정화 재판 처형자들의 이름을 새긴 두루마리를 등에 메고 다님 — 망각을 거부하는 의지의 상징.

## 미해결 개인 갈등 표

campaign ending 이후 남아도 되는 personal hook만 여기에 둔다. ADR-0024 결정 2번에 따라 모든 lead의 비용은 "잠정 + 회수 hook" 형식으로 표기.

| hero_id | 한국어 표시명 | hook_id | status_at_launch_end | recovery_hook | allowed_followup |
| --- | --- | --- | --- | --- | --- |
| `hero_dawn_priest` | 단린 (丹麟) | `hook_faith_crack` | resolved with cost — 사제 인장 잠정 반납, 새 신앙 형태 모색 시작 | 새 신앙·새 정체성으로 외전 등장 가능 | DLC |
| `hero_pack_raider` | 이빨바람 | `hook_pack_treaty` | resolved with cost — 분열된 씨족 일부와 일시 단절, 누설 행위가 분열의 결정적 계기였음 인정 | 외전 화해 (회조와의 피형제 관계 회복 가능성) | DLC |
| `hero_grave_hexer` | 묵향 (墨香) | `hook_memory_debt` | resolved with loss-and-recovery — 가장 오래된 기억 잠정 봉헌, 동료들이 단편 보유 | 외전에서 동료 기억으로 단편적 회수 | sequel |
| `hero_echo_savant` | 공한 (空閑) | `hook_memory_custody` | stabilized with absence-and-recovery — 격자 잠정 잔류, 동료들 귀환 | 외전 회신 또는 다른 시간대 투사 | sequel |

## Lead 영웅 상세 열전

beat budget 6인 주연 영웅의 출신, 동기, 핵심 갈등(인생관 차이 = 정치 노선), 대표 대사를 정리한다.

---

### 단린 (丹麟) — `hero_dawn_priest`

#### 출신 (Origin)

영원한 질서(영원한 질서)의 고위 수련사제. 솔라룸 옛 왕도 대성당에서 정화 의례와 성물 관리를 맡았으며, 변경 지대 원정에 자원한 최초의 사제 계급 출신이다. 남성 중심의 집정관 회의(집정관 회의)에 맞서 원정 참여를 쟁취한 여성 사제로, 본인은 이것을 신앙의 확장이라 믿었으나 실제로는 집정관 회의가 변경 정보 수집을 위해 보낸 관찰자에 가깝다.

#### 동기 (Motivation) 와 인생관

초기에는 변경 지대의 오염을 정화하고 영원한 질서의 빛을 전파하겠다는 순수한 사명감으로 원정에 임한다. 그러나 ch2에서 솔라룸이 격자 파편을 성물로 위장해 무기화한 사실, 정화 재판이 이견 탄압의 도구였던 진실을 발견하며 신앙의 근거 자체가 흔들린다. **인생관: 신앙 vs 진실의 자기 분열** — 단린은 자기 자신과의 싸움이 본업인 캐릭터다. 외부 조건이 아니라 내면의 신앙적 확신과 발견된 진실 사이의 균열이 그녀의 정치 노선(정화 지지)을 결정한다.

#### 핵심 갈등 (`hook_faith_crack`)

자신이 평생 섬긴 교단의 기반이 약탈한 유물과 은폐 위에 세워졌다는 사실을 감당해야 한다. 신앙을 버리면 정체성이 무너지고, 유지하면 거짓의 공범이 된다. 더 깊은 문제는, 그녀가 섬긴 교리를 설계한 자가 단현(丹玄, 단현 스턴홀트)이라는 사실이다. 단현과 맞서 싸운다는 것은 자신의 신앙의 근원 자체를 부정하는 것이며, 그 위에 세운 정체성 전부가 흔들린다는 뜻이다. ch5에서 그녀는 자발적으로 사제 인장을 잠정 반납한다 — 더 이상 영원한 질서의 사제가 아닌 채로, 새로운 의미를 찾아야 한다. 회수 hook: 새 신앙·새 정체성으로 외전 등장 가능.

잃는 것: 사제 직위와 정체성. 단 잠정 반납이며 새 신앙 형태 모색의 시작점이다.

#### 대표 대사 (Signature Quotes)

> "질서란 세계가 우리에게 허락한 자리를 지키는 것입니다. …적어도, 저는 그렇게 배웠습니다."
>
> "성물함 안에 든 것이 신의 축복이 아니라 남의 뼈였다면, 우리가 올린 기도는 대체 누구에게 닿은 겁니까."
>
> "신앙이 무너진 자리에 남는 건 공허가 아니라 질문입니다. 저는 그 질문을 안고 걸어가겠습니다."

---

### 이빨바람 — `hero_pack_raider`

#### 출신 (Origin)

이리솔 부족 남부 씨족의 젊은 무리 지도자. 재의 문 붕괴로 사냥터가 초토화되면서 무리 전체가 유랑민이 되었고, 생존을 위해 솔라룸 원정대와의 합류를 결정한 장본인이다. 씨족 회의에서 이 결정은 만장일치가 아니었으며, 일부 장로는 배신이라 보았다.

- **회조 (灰爪, `npc_grey_fang`)**: 씨족의 장로이자 이빨바람의 피형제. ch1에서 보스로 등장했던 인물. 원정대 합류 결정에 처음엔 분노했지만 "이 분노가 내 것이 아닐 수 있다"고 인정한 뒤 일시적 동맹을 허용했다. 그러나 ch4에서 씨족 분열이 일어나면 분리파(의구심 노선)에 선다. 이빨바람에게는 가장 아픈 관계 — 적이 아니라 다른 길을 택한 형제.
- **연주 (燕走, `npc_ember_runner`)**: 이빨바람을 따르는 젊은 여성 사냥꾼. 빠르고 용감하지만 아직 첫 사냥을 마치지 못한 나이. 이빨바람의 결정을 지지하며 원정대와의 연락책 역할을 했다. ch4에서 씨족 분열 소식을 직접 가져오는 인물. "장로들이 형제를 버렸어요"라고 전한다. ch5 엔딩에서 연주가 이빨바람 쪽을 택했는지, 회조 쪽을 택했는지는 외전 hook으로 열려 있다.

#### 동기 (Motivation) 와 인생관

처음에는 잃어버린 사냥터를 되찾는 것이 전부다. 영역이 돌아오면 솔라룸과의 동맹은 끝이라 생각한다. 그러나 원정을 통해 심장로의 존재와 모든 인간 갈래가 공명의 영향 아래 있다는 사실을 알게 되면서, 영역 탈환만으로는 순환을 끊을 수 없음을 깨닫는다. **인생관: 현재주의 / 당대주의** — 추상적 미래 정의보다 손에 닿는 현재의 가족·씨족·이웃을 우선한다. 이것이 그가 ch4에서 봉인을 주장하는 이유다 — "지금 가까이 있는 자에게 진다"는 신념. 단린의 정화 주장(과거 죄 속죄 + 미래 세대 보호)과 정면으로 충돌하는 정치 노선.

#### 핵심 갈등 (`hook_pack_treaty`)

솔라룸과의 동맹은 맺었으나 씨족 내부 합의가 완성되지 않았다. 장로 일부는 여전히 솔라룸을 적으로 보며, 동맹이 깊어질수록 무리 내 분열도 깊어진다. **판단 실수의 비용 (ADR-0024 결정 2번)**: ch1 site_wolfpine_trail에서 이빨바람은 단린에게 토템의 의미를 설명한다. 이리솔 부족 비밀 의례 어휘를 외부 세력에게 누설한 행위 — 본인은 신뢰 구축을 위한 옳은 선택이라 믿었지만, 씨족 장로들에게는 결사 비밀 위반의 결정적 증거가 된다. 철피(鐵皮, `npc_iron_pelt`) 같은 강경 분리파가 이 누설을 분리주의 정당화에 활용한다. 즉 이빨바람은 옳은 일을 했지만, 그 결과를 충분히 미리 못 본 책임이 있다 — 자발적 희생이 아니라 잘못 판단한 비용. ch4에서 씨족 장로들은 그의 동맹을 배신으로 선언하고, 씨족의 일부가 분리한다. 그는 무리보다 세계를 택했고, 그 비용은 실재한다.

잃는 것: 분열된 씨족의 일부. 외전 화해 hook 보유 (회조와의 피형제 관계 회복 가능성, 연주의 선택 미정).

#### 대표 대사 (Signature Quotes)

> "바람 냄새로 안다. 이 땅은 아직 피를 기억하고 있어."
>
> "이빨을 세우는 건 쉽지. 어려운 건 이빨을 거두고도 무리를 지킬 수 있다고 믿는 거야."
>
> "200년 뒤? 그건 200년 뒤 사람의 일이야. 나는 지금 내 옆에 있는 사람을 못 본 척할 수 없어."
>
> "내가 너에게 토템 이야기를 한 것 — 그게 옳은 선택이었는지 아직도 모르겠어. 다만 그 선택이 회조를 분리파로 밀어 넣었다는 건 안다."

---

### 묵향 (墨香) — `hero_grave_hexer`

#### 출신 (Origin)

회상 결사의 기억 관리관(Memory Keeper) 중 가장 오래된 축에 속하는 여성 인물. 첫 전쟁 시대의 기억 파편을 직접 보유하고 있으며, 대부분의 동시대 동료는 이미 기억을 잃고 공동(Hollow)이 되었다. 깨어난 지 수백 년이 지났지만, 그녀에게 어제와 천 년 전의 차이는 크지 않다. 외양은 격자 부산물 노출이 누적된 결과(ADR-0024 baseline) — 창백한 피부, 정화 재판 시대 옛 의복, 의식적 자해 흉터·문신.

#### 동기 (Motivation) 와 인생관

잃어버린 기억들을 회수하고, 공동이 된 동료들에게 존재를 되돌려주는 것이 일차적 목적이다. 그러나 원정이 진행되면서 자신이 보유한 기억 속에 모든 세력의 과오가 기록되어 있음을 발견한다. **인생관: 증인주의 / 진실 양도** — 모든 잘못을 기억하는 자만이 같은 일을 반복하지 않게 만든다는 신념. 그녀가 정화 의식에서 가장 오래된 기억을 봉헌하는 것은 외부 조건의 강요가 아니라, 본인의 인생관에서 직접 도출되는 의식적 결정이다 — "기억을 지키는 것이 우리의 존재 이유인데, 지키기 위해 내 것 하나를 내놓지 못하겠느냐". 이것이 그녀의 정치 노선(정화 지지) 근거다.

#### 핵심 갈등 (`hook_memory_debt`)

그녀가 지닌 기억은 과거의 증거이자 무기다. 공개하면 모든 세력의 정당성이 흔들리고, 은폐하면 순환이 반복된다. 정화 의례에서 격자를 정상화하려면 그녀의 가장 오래된 기억이 열쇠로 필요하다. 이 기억은 그녀의 존재의 출발점이다. 잠정 봉헌한 뒤 그녀는 자신이 깨어나기 전에 누구였는지, 왜 깨어났는지를 잊는다. 다만 동료들이 그 기억의 단편을 보유하므로 외전에서 단편적 회수 가능 — 회수 hook 명시. 또한 회상 결사 강경파 흑지(黑紙, `npc_black_vellum`)는 그녀의 봉헌 자체를 "정화 재판의 죄를 잊는 것"이라 비판하며, 묵향은 결사 내부에서도 흑지와의 노선 갈등을 안고 가야 한다.

잃는 것: 가장 오래된 기억의 단독 소유권. 단 잠정 봉헌이며 동료들이 단편을 보유한다.

#### 대표 대사 (Signature Quotes)

> "그 전쟁 말인가. 아, 그래… 이천 년쯤 됐지. 어제 같다는 말은 하지 않겠네. 진짜로 어제 같으니까."
>
> "기억이란 친절한 것이 아니야. 네가 잊고 싶은 것도, 네 적이 숨기고 싶은 것도 전부 남아 있거든."
>
> "내가 봉헌하는 건 잊는 것이 아니야. 다음 사람에게 양도하는 거지. 죽은 자의 장점이 하나 있다면, 무게를 옮길 수 있다는 거야."

---

### 공한 (空閑) — `hero_echo_savant`

#### 출신 (Origin)

그물 결사의 격자 기록관(Lattice Archivist). 1800년 봉인 작업에 참여한 결사 시조의 직계 후예이며, 결사 내부에서 의례·문양·기록을 전수받아 격자 신호에 반응할 수 있다. 본인은 자기 결사 정체성을 알고 있었으며, 외부 세계에는 1800년 동안 한 번도 결사가 자기 정체를 보인 적 없었다. 격자 풍화가 임계점을 넘으면서 캠페인 중반에 결사적 각성을 시작한다. 심장로 봉인의 설계 기억과 격자 복원 지식을 단독으로 보유하고 있으며, 이것이 원정대 전체의 전략적 핵심이 된다.

#### 동기 (Motivation) 와 인생관

격자를 수복하고 심장로를 다시 봉인하는 것이 각성 직후의 유일한 목적이다. 그러나 네 인간 갈래가 서로를 불신하고 각자의 정의를 주장하는 현실 앞에서, 봉인의 기술적 성공만으로는 순환이 끊어지지 않음을 깨닫는다. **인생관: 1800년 결사 약속 vs 현재 자유의 자기 분열** — 결사 시조의 약속과 현재 자기 의지 사이의 균열이 그의 내면 갈등의 핵심. 원정대 합류 후 가장 깊은 대화를 나누는 상대는 묵향이다. 기억을 지키는 자와 격자를 듣는 자 — 둘 다 수천 년의 무게를 지고 있으면서, 한쪽은 유머로, 한쪽은 절제로 견딘다. 이 관계가 ch5에서 공한의 잠정 잔류 결정에 무게를 더한다.

#### 핵심 갈등 (`hook_memory_custody`)

봉인 기억의 유일한 관리자라는 무게가 짓누른다. 기억을 공유하면 악용될 수 있고, 독점하면 독재가 된다. 또한 그물 결사 동료들이 외부에 한 번도 자기 정체를 보인 적 없으므로 합의를 구할 대상이 거의 없다. 최종부에서 격자 안정화를 위해 잠정 격자 수문장을 자원한다. 격자 안에 잠정 잔류하는 것이다 — 의무가 아니라 선택이다. 동료들이 밖으로 걸어 나갈 수 있도록, 자신이 안에 남는다. 다른 이들은 반대하지만, 그는 끝까지 고집한다. 회수 hook: 외전 회신 또는 다른 시간대 투사. "1800년 전에도 봉인을 선택했고, 이번에는 눈을 뜨고 선택한다."

잃는 것: 외부 활동의 자유. 단 잠정 잔류이며 외전 회신 hook 보유.

#### 대표 대사 (Signature Quotes)

> "격자가 울린다. 낮은 주파수. 너희가 듣지 못하는 것을 나는 뼈로 느낀다."
>
> "봉인은 자물쇠가 아니다. 합의다. 열쇠가 아니라 의지가 필요하다."
>
> "내가 가진 기억은 무기가 될 수도, 다리가 될 수도 있다. 어느 쪽이 될지는 너희가 결정한다."
>
> "안은 너무 조용하다. …밖이 시끄러울 테니, 누군가는 안에 남아야 한다."

---

## Support 영웅 핵심 동기

beat budget 3인 조연 영웅의 핵심 동기를 정리한다.

---

### 철위 (鐵衛) — `hero_iron_warden`

솔라룸 정규군의 마지막 구세대 방패병. 동료들은 재의 문 붕괴 때 전사했고, 본인만 살아남았다. 충성의 대상이었던 왕도, 지켜야 할 전선도 사라진 지금, 남은 것은 몸에 새겨진 습관뿐이다. 누군가를 지키는 것 외에는 자신의 존재 이유를 설명할 방법이 없기에, 원정대의 전열에 선다. 신념이 아니라 관성에 가까운 의무감이지만, 그 관성이 전선을 지탱하는 가장 단단한 축이 된다.

### 묘직 (墓直) — `hero_crypt_guardian`

회상 결사 묘역의 여성 수호자. 석관 속에 잠든 것은 뼈가 아니라 사랑했던 이들의 기억이며, 그 기억이 소실되면 진정한 죽음이 찾아온다. 묘역을 지키는 행위는 곧 죽은 문명의 마지막 증인으로 서는 것이다. 원정에 합류한 것은 외부 세력이 묘역을 훼손하는 것을 막기 위해서였으나, 여정이 깊어지면서 그녀가 지키는 기억 속에 모든 세력의 죄가 기록되어 있음을 증언해야 하는 역할을 떠안게 된다.

### 서검 (誓劍) — `hero_oath_slayer`

솔라룸의 맹세검(Oath Blade). 집정관 회의의 명령을 물리적으로 집행하는 도구로 훈련받은 여성 처형자이며, 스스로도 그렇게 정체성을 정의해 왔다. 그녀의 존재는 칼 그 자체로 규정되었고, 칼이 향하는 곳에 의문을 품는 것은 맹세의 위반이라 배웠다. 그러나 원정 중 정화 재판의 실상과 격자 오용의 진실을 목격하면서, 자신이 휘둘러 온 칼이 정의가 아니라 은폐를 위한 것이었을 가능성에 직면한다. 무기로서의 정체성 밖에서 자신이 누구인지를 처음으로 묻기 시작하며, 칼을 드는 손이 처음으로 망설인다.

### 원시 (遠矢) — `hero_longshot_hunter`

원정대의 실무 보급 담당이자 장거리 정찰병. 정치적 명분이나 신앙적 사명에는 관심이 없고, 원정대가 내일도 먹고 살아남을 수 있는가에만 집중한다. 세력 간 갈등을 냉정한 실무자의 눈으로 관찰하며, 대의보다 보급선이 끊기는 것을 더 두려워한다. 정치를 꿰뚫어 보는 눈이 있지만, 그 통찰을 굳이 공유하려 들지는 않는다. 살아 돌아가는 것이 유일한 목표이며, 그 목표가 원정대 전체의 생존과 겹치는 한에서만 협력한다.

### 숲살이 — `hero_trail_scout`

이리솔 부족과 솔라룸 영역의 경계에서 자란 혼혈 출신 여성 정찰병. 어느 쪽에도 완전히 속하지 못한 경험이 오히려 양쪽 사이의 완충재 역할을 가능하게 만들었다. 긴장이 극에 달할 때 건조한 농담으로 분위기를 꺾는 능력이 있으며, 본인은 이것을 생존 기술이라 부른다. 유머 아래에는 어디에도 속하지 못한 자의 외로움이 있지만, 그녀가 그것을 드러내는 일은 거의 없다. 두 세계 사이의 다리가 되는 것이 소명이라기보다는, 그 위치 말고는 설 곳이 없기 때문이다.

### 풍의 (風儀) — `hero_storm_shaman`

이리솔 부족 씨족의 여성 의례 관장자이자 공동체 치유사. 순환의 숨결(순환의 숨결) 의례를 통해 씨족의 기억과 유대를 유지하는 역할을 맡고 있다. 원정에 합류한 것은 무리에서 떨어져 나온 전사들의 영혼을 돌보기 위해서다. 그녀는 심장로의 공명을 본능적으로 감지하며, 이것이 씨족 의례의 뿌리와 연결되어 있다는 직감을 품고 있다. 전투보다 치유를, 파괴보다 복원을 우선하며, 씨족의 의례가 끊어지지 않는 한 희망은 남아 있다고 믿는다.

### 방진 (方陣) — `hero_aegis_sentinel`

원정대가 처음으로 조우한 각성 그물 결사 인물. 격자 노드 하나가 완전히 파괴되면서 결사 내부의 비밀 지식을 단독으로 사용해 강제 각성했으며, 격자 복원이라는 대의보다는 눈앞의 노드를 지키겠다는 본능에 가깝게 움직인다. 결사 비밀 유지 의무 때문에 언어 소통이 극히 제한적이고, 의사 표현은 거의 행동으로만 이루어진다. 원정대를 보호하는 이유를 말로 설명한 적은 없지만, 위기 상황에서 반복적으로 전열에 서는 행위 자체가 신뢰의 증명이 된다. 말이 아니라 몸으로 증명하는 존재이며, 그물 결사가 적이 아님을 처음으로 보여주는 사례다.

### 명음 (明音) — `hero_mirror_cantor`

그물 결사 기록관. 결사 내부에서 정화 의식의 전체 절차를 기억하고 있는 유일한 인물. ch5 후반에 합류해 최종부 정합성 봉합자 역할을 맡는다. 음성 의례와 격자 공명을 결합한 기록 방식의 전문가이며, 외부에 결사 비밀을 처음 공식적으로 노출하는 결정을 내리는 인물이다 — 1800년 침묵을 깨고 외부와 정화 의식을 공유하는 첫 결사 인물.

---

## Background 영웅 character event hook

beat budget 1인 background 영웅에게도 character event 표면을 보장한다. 본 섹션은 launch 후 character event 1개 분량으로 elaborate 가능한 hook을 1줄씩 명시한다. 운영형 캐릭터 게임의 매트릭스 깊이 확보 — 모든 영웅에게 자기 화가 보장된다.

| hero_id | 한국어 표시명 | hook_id | 한 줄 hook |
| --- | --- | --- | --- |
| `hero_fang_bulwark` | 송곳벽 | `hook_fang_first_hunt` | 첫 사냥에서 동료를 잃은 트라우마 — 방패병이 된 이유와 다시 사냥대장 자리에 설 수 있는가의 질문 |
| `hero_grave_reaver` | 묵괴 (墨壞) | `hook_reaver_lost_friend` | 기억 누락된 옛 친구를 한 번이라도 다시 알아보기 위해 깨어 있는 자 — 친구를 찾아 결사 외부로 나갈 것인가 |
| `hero_dread_marksman` | 냉시 (冷矢) | `hook_marksman_last_feeling` | 감정이 사라진 자가 마지막으로 느꼈던 한 순간을 찾는 여정 — 감정의 회복은 가능한가 |
| `hero_rift_stalker` | 틈사냥꾼 | `hook_rift_apprentice` | 회조의 정찰 견습이었던 과거 — 피형제 분열 후 어디에 설지 결정 |
| `hero_bastion_penitent` | 참회벽 | `hook_penitent_family` | 정화 재판으로 가족을 잃은 후 권력 내부 고발자가 된 사연 — 솔라룸을 떠나야 하는가, 안에서 바꿔야 하는가 |
| `hero_pale_executor` | 백집행 (白執行) | `hook_executor_dual_loyalty` | 솔라룸 처형 집행자였다가 회상 결사로 망명한 이중 정체성 — 어느 쪽 죄가 더 무거운가 |
| `hero_shardblade` | 편검 (片劍) | `hook_shardblade_secret_oath` | 1800년 봉인 결사 비밀을 가족에게도 숨겨야 했던 결사 의무 — 외부 동료에게 정체를 처음 밝히는 순간의 무게 |
| `hero_prism_seeker` | 광로 (光路) | `hook_prism_outside_curiosity` | 외부 세계 호기심 — 결사 비밀 유지 의무와 외부 동료들과의 관계 사이 긴장 |

각 hook은 launch 이후 character event 1개 분량으로 elaborate 가능하며, 새 4 인간 세력 baseline에 정합한다.

## 열전 상세 문서 규칙

상세 문서를 만들 경우:
- `docs/02_design/deck/heroes/hero-{id}.md` 경로를 사용한다.
- 필수 섹션: 출신, 동기·인생관, 핵심 갈등, quote bank, 개인 비트 상세
- 금지 중복: registry에 이미 있는 tier/beat_budget을 상세 문서에서 다시 정의하지 않는다.
- registry와 상세 문서의 충돌 시 registry가 우선한다.

## 작성 지침

- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.
- 한국어 표시명을 본명으로, 영문 별칭(legacy)은 ID와 reference에만 사용한다 (ADR-0024 + `project_character_naming_korean` memory).
- lead 4 inner struggle은 인생관 차이 = 정치 노선 형식으로 명시 (ADR-0024 결정 3).
- 모든 lead의 비용은 "잠정 + 회수 hook" 형식으로 표기 (ADR-0024 결정 2). "되돌릴 수 없는" 표현 회피.
- sub-antagonist 4명은 단일 적대자(단현)만으로는 부족한 운영형 캐릭터 게임 적대 진영 표면을 채운다.
- background 8명에도 character event hook 1줄씩 보장 — 매트릭스 깊이.
