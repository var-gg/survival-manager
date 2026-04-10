# 캐릭터 열전 레지스트리

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/deck/character-lore-registry.md`
- 관련문서:
  - `docs/02_design/deck/hero-expansion-roadmap.md`
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`

## 목적

모든 영웅의 canonical short bio, tier, beat budget, unresolved hook를 한곳에 정리한다. 장문 열전 문서가 생기더라도 이 문서가 registry SoT다.

## 레지스트리 규칙

- 영웅 1명당 1행을 유지한다.
- 장문 biography는 `docs/02_design/deck/heroes/hero-*.md`에 두고, 본 문서에서는 요약과 참조만 남긴다.
- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.

## 영웅 표

| hero_id | display_name | race | class | gender | tier | beat_budget | canon_status | narrative_role | unresolved_hook |
|---|---|---|---|---|---|---:|---|---|---|
| `hero_iron_warden` | Iron Warden | Human | Vanguard | M | `support` | 3 | `launch` | 질서의 얼굴, 초반 안전판 | — |
| `hero_crypt_guardian` | Crypt Guardian | Undead | Vanguard | F | `support` | 3 | `launch` | 죽은 문명의 죄를 증언 | — |
| `hero_fang_bulwark` | Fang Bulwark | Beastkin | Vanguard | M | `background` | 1 | `launch` | 야수족 현장성의 상징 | — |
| `hero_oath_slayer` | Oath Slayer | Human | Duelist | F | `support` | 3 | `launch` | 왕국의 폭력성과 명분을 드러내는 칼 | — |
| `hero_pack_raider` | Pack Raider | Beastkin | Duelist | M | `lead` | 6 | `launch` | 야수족 측 주연 시점 | `hook_pack_treaty` |
| `hero_grave_reaver` | Grave Reaver | Undead | Duelist | M | `background` | 1 | `launch` | 파괴된 충성의 잔재 | — |
| `hero_longshot_hunter` | Longshot Hunter | Human | Ranger | M | `support` | 3 | `launch` | 원정 실무자의 시선 | — |
| `hero_trail_scout` | Trail Scout | Beastkin | Ranger | F | `support` | 3 | `launch` | 유머와 경계의 완충재 | — |
| `hero_dread_marksman` | Dread Marksman | Undead | Ranger | M | `background` | 1 | `launch` | 언데드의 냉혹한 효율 | — |
| `hero_dawn_priest` | Dawn Priest | Human | Mystic | F | `lead` | 6 | `launch` | 인간 진영 주연 시점 | `hook_faith_crack` |
| `hero_grave_hexer` | Grave Hexer | Undead | Mystic | F | `lead` | 6 | `launch` | 언데드 측 주연 시점 | `hook_memory_debt` |
| `hero_storm_shaman` | Storm Shaman | Beastkin | Mystic | F | `support` | 3 | `launch` | 야수족 공동체의 의례 | — |
| `hero_rift_stalker` | Rift Stalker | Beastkin | Duelist | M | `background` | 1 | `launch` | 적-동맹 전환의 첫 사례 | — |
| `hero_bastion_penitent` | Bastion Penitent | Human | Vanguard | F | `background` | 1 | `launch` | 왕국 내부 균열의 증거 | — |
| `hero_pale_executor` | Pale Executor | Undead | Ranger | M | `background` | 1 | `launch` | 언데드 실용주의의 흑백 | — |
| `hero_aegis_sentinel` | Aegis Sentinel | Relicborn | Vanguard | M | `support` | 3 | `launch` | Relicborn의 문지기 | — |
| `hero_echo_savant` | Echo Savant | Relicborn | Mystic | M | `lead` | 6 | `launch` | Relicborn 측 주연 시점 | `hook_memory_custody` |
| `hero_shardblade` | Shardblade | Relicborn | Duelist | F | `background` | 1 | `launch` | 중후반 전열 돌파 장치 | — |
| `hero_prism_seeker` | Prism Seeker | Relicborn | Ranger | F | `background` | 1 | `launch` | 진실 추적자 | — |
| `hero_mirror_cantor` | Mirror Cantor | Relicborn | Mystic | M | `support` | 3 | `launch` | 최종부 정합성 봉합자 | — |

## 핵심 NPC

| npc_id | display_name | faction | status | narrative_role | first_appearance |
|---|---|---|---|---|---|
| `npc_aldric` | Aldric Sternholt | `faction_kingdom_remnant` (사망) | Heartforge 내 잔류 의지 | 적대자. 왕국 초대 학자장, 격자 오용 주도자, Eternal Order 교리 설계자 | ch2 기록 → ch3 기억 투사 → ch4 직접 간섭 → ch5 최종 적대자 |
| `npc_grey_fang` | Grey Fang | `faction_beastkin_clans` | 생존 | Pack Raider의 피형제이자 장로. ch1 보스 → ch4 분열 시 반대편 | ch1 wolfpine_trail boss |
| `npc_ember_runner` | Ember Runner | `faction_beastkin_clans` | 생존 | Pack Raider를 따르는 젊은 사냥꾼. 분열 소식 전달자 | ch4 starved_menagerie |

### Aldric Sternholt 상세

80년 전 왕국 초대 학자장(Scholar-Chief). 격자 파편을 최초로 발견하고 체계적 연구를 시작한 천재 연구자였다. Heartforge의 존재를 처음으로 인지한 인물이며, 격자를 인류의 이익을 위해 제어할 수 있다고 진심으로 믿었다. 악을 위한 악이 아니라, 혼돈에서 질서를 만들겠다는 확신이 세계를 파괴한 경우다. 그의 오만은 선의에서 출발했고, 그래서 더 위험했다.

그가 설계한 교리는 영원한 질서(Eternal Order)의 기반이 되었다. Dawn Priest가 평생 섬긴 신앙 체계, 집정관 회의의 권위, 정화 재판의 정당성 — 모든 것이 Aldric의 이론에서 파생되었다. 왕국이 격자 파편을 성물로 위장하고 무기화한 구조도 그의 설계에 뿌리를 두고 있다.

40년 전, 격자를 직접 조작하려다 Heartforge 심층부에서 사망했다. 그러나 Heartforge가 그의 기억과 의지를 흡수했으며, 격자가 풍화될수록 그의 인격은 Heartforge 내부에서 점점 강해진다. 처음에는 파편적 기록으로만 존재하지만(ch2), 기억 투사로 형태를 갖추고(ch3), 오염된 피조물을 통해 말을 걸기 시작하며(ch4), ch5에서는 원정대와 직접 대면한다.

적대자로서의 Aldric은 단순한 악당이 아니다. 그의 주장 — "나는 혼돈에서 질서를 만들었다. 너희는 내가 세운 모든 것을 파괴하고 있다" — 은 질서에 대한 열망 자체로는 틀리지 않았다. 방법이 틀렸을 뿐이다. 그는 세계를 구하려 했고, 그 시도가 세계를 망가뜨렸으며, 죽어서도 자신의 실패를 인정하지 못한다. 최종 전투에서 원정대가 맞서는 것은 힘이 아니라, 자기 확신에 갇힌 한 인간의 잔류 의지다.

## 미해결 개인 갈등 표

campaign ending 이후 남아도 되는 personal hook만 여기에 둔다.

| hero_id | hook_id | status_at_launch_end | allowed_followup |
|---|---|---|---|
| `hero_dawn_priest` | `hook_faith_crack` | resolved — 사제 인장 반납, 새 정체성 모색 시작 | DLC |
| `hero_pack_raider` | `hook_pack_treaty` | resolved — 씨족 분열 수용, 남은 무리와 진실 공유 | DLC |
| `hero_grave_hexer` | `hook_memory_debt` | resolved — 가장 오래된 기억 봉헌, 남은 기억으로 새 시작 | sequel |
| `hero_echo_savant` | `hook_memory_custody` | resolved — 격자 영구 잔류 선택, 동료들은 귀환 | sequel |

## Lead 영웅 상세 열전

beat budget 6인 주연 영웅의 출신, 동기, 핵심 갈등, 대표 대사를 정리한다.

---

### Dawn Priest (`hero_dawn_priest`)

**출신 (Origin)**

영원한 질서(Eternal Order)의 고위 수련사제. 왕국 중앙 대성당에서 정화 의례와 성물 관리를 맡았으며, 변경 지대 원정에 자원한 최초의 사제 계급 출신이다. 남성 중심의 집정관 회의(Steward Council)에 맞서 원정 참여를 쟁취한 여성 사제로, 본인은 이것을 신앙의 확장이라 믿었으나, 실제로는 집정관 회의가 변경 정보 수집을 위해 보낸 관찰자에 가깝다.

**동기 (Motivation)**

초기에는 변경 지대의 오염을 정화하고 영원한 질서의 빛을 전파하겠다는 순수한 사명감으로 원정에 임한다. 그러나 chapter 2에서 왕국이 격자 파편을 성물로 위장해 무기화한 사실, 정화 재판이 이견 탄압의 도구였던 진실을 발견하며 신앙의 근거 자체가 흔들린다. 그녀가 맞서 싸웠던 집정관 회의의 가부장적 권위가 단순한 보수성이 아니라 은폐 구조의 일부였음을 깨달으면서, 동기는 '신앙의 수호'에서 '진실의 추적'으로 전환된다.

**핵심 갈등 (`hook_faith_crack`)**

자신이 평생 섬긴 교단의 기반이 약탈한 유물과 은폐 위에 세워졌다는 사실을 감당해야 한다. 신앙을 버리면 정체성이 무너지고, 유지하면 거짓의 공범이 된다. 더 깊은 문제는, 그녀가 섬긴 교리를 설계한 자가 Aldric Sternholt이라는 사실이다. Aldric과 맞서 싸운다는 것은 자신의 신앙의 근원 자체를 부정하는 것이며, 그 위에 세운 정체성 전부가 무너진다는 뜻이다. ch5에서 그녀는 자발적으로 사제 인장을 반납한다 — 더 이상 영원한 질서의 사제가 아닌 채로, 새로운 의미를 찾아야 한다.

잃는 것: 사제 직위와 정체성. 더 이상 영원한 질서의 사제가 아닌 채로 새 의미를 찾아야 한다.

**대표 대사 (Signature Quotes)**

> "질서란 세계가 우리에게 허락한 자리를 지키는 것입니다. …적어도, 저는 그렇게 배웠습니다."

> "성물함 안에 든 것이 신의 축복이 아니라 남의 뼈였다면, 우리가 올린 기도는 대체 누구에게 닿은 겁니까."

> "신앙이 무너진 자리에 남는 건 공허가 아니라 질문입니다. 저는 그 질문을 안고 걸어가겠습니다."

---

### Pack Raider (`hero_pack_raider`)

**출신 (Origin)**

야수족 남부 씨족의 젊은 무리 지도자. Gate 붕괴로 사냥터가 초토화되면서 무리 전체가 유랑민이 되었고, 생존을 위해 인간 원정대와의 합류를 결정한 장본인이다. 씨족 회의에서 이 결정은 만장일치가 아니었으며, 일부 장로는 배신이라 보았다.

- **Grey Fang** (`npc_grey_fang`): 씨족의 장로이자 Pack Raider의 피형제. ch1에서 보스로 등장했던 인물. 원정대 합류 결정에 처음엔 분노했지만 "이 분노가 내 것이 아닐 수 있다"고 인정한 뒤 일시적 동맹을 허용했다. 그러나 ch4에서 씨족 분열이 일어나면 반대편에 선다. Pack Raider에게는 가장 아픈 관계 — 적이 아니라 다른 길을 택한 형제.
- **Ember Runner** (`npc_ember_runner`): Pack Raider를 따르는 젊은 여성 사냥꾼. 빠르고 용감하지만 아직 첫 사냥을 마치지 못한 나이. Pack Raider의 결정을 지지하며 원정대와의 연락책 역할을 했다. ch4에서 씨족 분열 소식을 직접 가져오는 인물. "장로들이 형제를 버렸어요"라고 전한다. ch5 엔딩에서 Ember Runner가 Pack Raider 쪽을 택했는지, Grey Fang 쪽을 택했는지는 열려 있다.

**동기 (Motivation)**

처음에는 잃어버린 사냥터를 되찾는 것이 전부다. 영역이 돌아오면 인간과의 동맹은 끝이라 생각한다. 그러나 원정을 통해 Heartforge의 존재와 모든 종족이 공명의 영향 아래 있다는 사실을 알게 되면서, 영역 탈환만으로는 순환을 끊을 수 없음을 깨닫는다. 동기는 '씨족의 땅'에서 '모든 종의 생존권'으로 확장된다.

**핵심 갈등 (`hook_pack_treaty`)**

인간과의 동맹은 맺었으나 씨족 내부 합의가 완성되지 않았다. 장로 일부는 여전히 인간을 적으로 보며, 동맹이 깊어질수록 무리 내 분열도 깊어진다. ch4에서 씨족 장로들은 그의 동맹을 배신으로 선언하고, 씨족의 일부가 영구적으로 이탈한다. 그는 무리보다 세계를 택했고, 그 대가는 실재한다. 돌아가도 전과 같은 무리가 아니며, 분열은 돌이킬 수 없다.

잃는 것: 씨족의 일부. 돌아가도 전과 같은 무리가 아니다. 분열은 돌이킬 수 없다.

**대표 대사 (Signature Quotes)**

> "바람 냄새로 안다. 이 땅은 아직 피를 기억하고 있어."

> "이빨을 세우는 건 쉽지. 어려운 건 이빨을 거두고도 무리를 지킬 수 있다고 믿는 거야."

> "너희 왕이 세운 울타리 안에서 우리 새끼들이 굶어 죽었다. 동맹? 좋아. 하지만 잊지는 마."

---

### Grave Hexer (`hero_grave_hexer`)

**출신 (Origin)**

언데드 잔존세력의 기억 관리관(Memory Keeper) 중 가장 오래된 축에 속하는 여성 존재. 첫 전쟁 시대의 기억 파편을 직접 보유하고 있으며, 대부분의 동시대 동료는 이미 기억을 잃고 공동(Hollow)이 되었다. 깨어난 지 수백 년이 지났지만, 그녀에게 어제와 천 년 전의 차이는 크지 않다.

**동기 (Motivation)**

잃어버린 기억들을 회수하고, 공동이 된 동료들에게 존재를 되돌려주는 것이 일차적 목적이다. 그러나 원정이 진행되면서 자신이 보유한 기억 속에 모든 세력의 과오가 기록되어 있음을 발견한다. 동기는 '동족의 기억 복원'에서 '모든 세력이 공유하는 죄의 이해'로 진화한다.

**핵심 갈등 (`hook_memory_debt`)**

그녀가 지닌 기억은 과거의 증거이자 무기다. 공개하면 모든 세력의 정당성이 흔들리고, 은폐하면 순환이 반복된다. 기억의 빚을 청산하려면 진실을 드러내야 하지만, 그것이 취약한 동맹을 파괴할 수도 있다. 정화 의례에서 격자를 정상화하려면 그녀의 가장 오래된 기억이 열쇠로 필요하다. 이 기억은 그녀의 존재의 출발점이다. 봉헌한 뒤 그녀는 자신이 깨어나기 전에 누구였는지, 왜 깨어났는지를 잊는다 — 정체성의 핵심이 사라진다.

잃는 것: 가장 오래된 기억. 자신이 누구였는지, 왜 깨어났는지를 잊는다. 정체성의 핵심이 사라진다.

**대표 대사 (Signature Quotes)**

> "그 전쟁 말인가. 아, 그래… 이천 년쯤 됐지. 어제 같다는 말은 하지 않겠네. 진짜로 어제 같으니까."

> "기억이란 친절한 것이 아니야. 네가 잊고 싶은 것도, 네 적이 숨기고 싶은 것도 전부 남아 있거든."

> "죽은 자의 장점이 하나 있다면, 서두를 이유가 없다는 거지. 진실은 재촉한다고 빨리 오지 않아."

---

### Echo Savant (`hero_echo_savant`)

**출신 (Origin)**

Relicborn 격자 기록관(Lattice Archivist). 봉인기에 다른 Relicborn과 함께 자발적 휴면에 들어갔으나, 격자 풍화가 임계점을 넘으면서 캠페인 중반에 각성한다. Heartforge 봉인의 설계 기억과 격자 복원 지식을 단독으로 보유하고 있으며, 이것이 원정대 전체의 전략적 핵심이 된다.

**동기 (Motivation)**

격자를 수복하고 Heartforge를 다시 봉인하는 것이 각성 직후의 유일한 목적이다. 그러나 네 세력이 서로를 불신하고 각자의 정의를 주장하는 현실 앞에서, 봉인의 기술적 성공만으로는 순환이 끊어지지 않음을 깨닫는다. 동기는 '격자 수복'에서 '세력 간 중재와 공존의 설계'로 확장된다. 원정대 합류 후 가장 깊은 대화를 나누는 상대는 Grave Hexer다. 기억을 지키는 자와 격자를 듣는 자 — 둘 다 수천 년의 무게를 지고 있으면서, 한쪽은 유머로, 한쪽은 침묵으로 견딘다. 이 관계가 ch5에서 Echo의 잔류 결정에 무게를 더한다.

**핵심 갈등 (`hook_memory_custody`)**

봉인 기억의 유일한 관리자라는 무게가 짓누른다. 기억을 공유하면 악용될 수 있고, 독점하면 독재가 된다. 또한 Relicborn 동료들이 아직 휴면 중이므로 합의를 구할 대상조차 없다. 최종부에서 격자 안정화를 위해 영구 격자 수호자를 자원한다. 격자 안에 영원히 갇히는 것이다. 의무가 아니라 선택이다 — 동료들이 밖으로 걸어 나갈 수 있도록, 자신이 안에 남는다. 다른 이들은 반대하지만, 그는 끝까지 고집한다.

잃는 것: 자유. 격자 안에 영구 잔류. 밖으로 나올 수 없다. 동료들의 자유를 위한 자발적 선택.

**대표 대사 (Signature Quotes)**

> "격자가 울린다. 낮은 주파수. 너희가 듣지 못하는 것을 나는 뼈로 느낀다."

> "봉인은 자물쇠가 아니다. 합의다. 열쇠가 아니라 의지가 필요하다."

> "내가 가진 기억은 무기가 될 수도, 다리가 될 수도 있다. 어느 쪽이 될지는 너희가 결정한다."

---

## Support 영웅 핵심 동기

beat budget 3인 조연 영웅의 핵심 동기를 정리한다.

---

### Iron Warden (`hero_iron_warden`)

**핵심 동기**

왕국 정규군의 마지막 구세대 방패병. 동료들은 Gate 붕괴 때 전사했고, 본인만 살아남았다. 충성의 대상이었던 왕도, 지켜야 할 전선도 사라진 지금, 남은 것은 몸에 새겨진 습관뿐이다. 누군가를 지키는 것 외에는 자신의 존재 이유를 설명할 방법이 없기에, 원정대의 전열에 선다. 신념이 아니라 관성에 가까운 의무감이지만, 그 관성이 전선을 지탱하는 가장 단단한 축이 된다.

---

### Crypt Guardian (`hero_crypt_guardian`)

**핵심 동기**

언데드 묘역의 여성 수호자. 석관 속에 잠든 것은 뼈가 아니라 사랑했던 이들의 기억이며, 그 기억이 소실되면 진정한 죽음이 찾아온다. 묘역을 지키는 행위는 곧 죽은 문명의 마지막 증인으로 서는 것이다. 원정에 합류한 것은 외부 세력이 묘역을 훼손하는 것을 막기 위해서였으나, 여정이 깊어지면서 그녀가 지키는 기억 속에 모든 세력의 죄가 기록되어 있음을 증언해야 하는 역할을 떠안게 된다.

---

### Oath Slayer (`hero_oath_slayer`)

**핵심 동기**

왕국의 맹세검(Oath Blade). 집정관 회의의 명령을 물리적으로 집행하는 도구로 훈련받은 여성 처형자이며, 스스로도 그렇게 정체성을 정의해 왔다. 그녀의 존재는 칼 그 자체로 규정되었고, 칼이 향하는 곳에 의문을 품는 것은 맹세의 위반이라 배웠다. 그러나 원정 중 정화 재판의 실상과 격자 오용의 진실을 목격하면서, 자신이 휘둘러 온 칼이 정의가 아니라 은폐를 위한 것이었을 가능성에 직면한다. 무기로서의 정체성 밖에서 자신이 누구인지를 처음으로 묻기 시작하며, 칼을 드는 손이 처음으로 망설인다.

---

### Longshot Hunter (`hero_longshot_hunter`)

**핵심 동기**

원정대의 실무 보급 담당이자 장거리 정찰병. 정치적 명분이나 신앙적 사명에는 관심이 없고, 원정대가 내일도 먹고 살아남을 수 있는가에만 집중한다. 세력 간 갈등을 냉정한 실무자의 눈으로 관찰하며, 대의보다 보급선이 끊기는 것을 더 두려워한다. 정치를 꿰뚫어 보는 눈이 있지만, 그 통찰을 굳이 공유하려 들지는 않는다. 살아 돌아가는 것이 유일한 목표이며, 그 목표가 원정대 전체의 생존과 겹치는 한에서만 협력한다.

---

### Trail Scout (`hero_trail_scout`)

**핵심 동기**

야수족과 인간 영역의 경계에서 자란 혼혈 여성 정찰병. 어느 쪽에도 완전히 속하지 못한 경험이 오히려 양쪽 사이의 완충재 역할을 가능하게 만들었다. 긴장이 극에 달할 때 건조한 농담으로 분위기를 꺾는 능력이 있으며, 본인은 이것을 생존 기술이라 부른다. 유머 아래에는 어디에도 속하지 못한 자의 외로움이 있지만, 그녀가 그것을 드러내는 일은 거의 없다. 두 세계 사이의 다리가 되는 것이 소명이라기보다는, 그 위치 말고는 설 곳이 없기 때문이다.

---

### Dread Marksman (`hero_dread_marksman`)

**핵심 동기**

언데드 원거리 처형자. 감정을 소실한 채 깨어났으며, 기억은 남아 있으나 그 기억에 붙어 있어야 할 감정이 없다. 표적을 지정하면 실행하고, 실행이 끝나면 다음 표적을 기다린다. 이 냉혹한 효율성이 원정대에서는 전술적 자산이지만, 동료들에게는 불안의 원천이기도 하다. 본인에게 '왜 싸우느냐'는 질문은 의미가 없다. 싸우는 것만 남았기에 싸울 뿐이다.

---

### Storm Shaman (`hero_storm_shaman`)

**핵심 동기**

야수족 씨족의 여성 의례 관장자이자 공동체 치유사. 순환의 숨결(Breath of the Cycle) 의례를 통해 씨족의 기억과 유대를 유지하는 역할을 맡고 있다. 원정에 합류한 것은 무리에서 떨어져 나온 전사들의 영혼을 돌보기 위해서다. 그녀는 Heartforge의 공명을 본능적으로 감지하며, 이것이 씨족 의례의 뿌리와 연결되어 있다는 직감을 품고 있다. 전투보다 치유를, 파괴보다 복원을 우선하며, 씨족의 의례가 끊어지지 않는 한 희망은 남아 있다고 믿는다.

---

### Aegis Sentinel (`hero_aegis_sentinel`)

**핵심 동기**

원정대가 처음으로 조우한 각성 Relicborn. 격자 노드 하나가 완전히 파괴되면서 강제 각성했으며, 격자 복원이라는 대의보다는 눈앞의 노드를 지키겠다는 본능에 가깝게 움직인다. 언어 소통이 극히 제한적이고, 의사 표현은 거의 행동으로만 이루어진다. 원정대를 보호하는 이유를 말로 설명한 적은 없지만, 위기 상황에서 반복적으로 전열에 서는 행위 자체가 신뢰의 증명이 된다. 말이 아니라 몸으로 증명하는 존재이며, Relicborn이 적이 아님을 처음으로 보여주는 사례다.

---

## 열전 상세 문서 규칙

상세 문서를 만들 경우:
- `docs/02_design/deck/heroes/hero-{id}.md` 경로를 사용한다.
- 필수 섹션: 출신, 동기, 핵심 갈등, quote bank, 개인 비트 상세
- 금지 중복: registry에 이미 있는 tier/beat_budget을 상세 문서에서 다시 정의하지 않는다.
- registry와 상세 문서의 충돌 시 registry가 우선한다.

## 작성 지침

- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.
