# 048 Skill VFX Animation Coverage Audit Report

## TL;DR

보유 에셋의 절대량은 부족하지 않다. 문제는 88개 스킬을 88개 개별 prefab으로 붙일지, 재사용 가능한 presentation family와 skin/accent 규칙으로 묶을지의 설계다.

현재 raw VFX와 애니메이션은 첫 vertical slice의 스킬 표현을 감당할 수 있다. 다만 지금 C# 런타임 catalog는 generic cue fallback 중심이라, 이대로 도감 preview에 붙이면 많은 스킬이 비슷하게 보인다.

## Skill demand

현재 스킬 정의는 총 88개다.

| 구분 | 수량 |
| --- | ---: |
| CoreActive | 22 |
| UtilityActive | 22 |
| Passive | 22 |
| Support | 22 |

| 축 | 분포 |
| --- | --- |
| Kind | Buff 29, Utility 26, Strike 20, Debuff 6, Heal 5, Shield 2 |
| DamageType | Physical 56, Magical 23, Healing 9 |
| Delivery | Aura 40, Melee 19, Ranged 10, Projectile 8, Zone 8, Nova 2, Trap 1 |
| Active delivery | Melee 15, Aura 8, Projectile 7, Ranged 7, Zone 4, Nova 2, Trap 1 |
| Status applier | 35 |
| Unique VFX hook | 88 |

이 수치는 액티브 전투 연출보다 passive/support/aura/status 표현의 차별화가 더 큰 리스크임을 보여준다.

## Asset supply

Epic Toon FX는 combat VFX의 대부분을 감당할 수 있다.

| Family | 보유 상태 | 메모 |
| --- | --- | --- |
| melee slash / hit | Green | sword, brawling, hit, explosion 조합 가능 |
| projectile / missile | Green | bullet, fireball, lightning, magic, mystic, sharp, soul |
| magic cast / charge | Green | aura, charge, circle, enchant, field, pillar, sphere |
| heal / cleanse | Green | healing, hearts, sparkle, soft magic 조합 가능 |
| shield / guard | Green | combat shield, magic shield, guard enter cue 가능 |
| nova / burst | Green | nova, explosion, sparkle, lightning burst |
| dash / reposition | Green | dust, smoke, trail, speed accent |
| zone / linger | Yellow | zone prefab은 있으나 스킬 정체성별 tint/shape 규칙 필요 |
| debuff / status | Yellow | shadow, soul, poison-like green, lightning은 있으나 상태별 iconography 필요 |
| passive/support proc | Yellow | aura만 쓰면 밋밋하므로 subtle pulse와 glyph 규칙 필요 |
| named signature | Red candidate | mirror, echo, prism, sentence, lattice, phase 계열은 bespoke composition 필요 |

TriForge particles는 core combat VFX보다 environment/arena ambience로 쓰는 편이 안전하다.

## Animation supply

Kevin Iglesias humanoid animation은 첫 구현에 충분하다.

| Gesture | 보유 상태 | 메모 |
| --- | --- | --- |
| idle / move / hit / death | Green | 이미 fallback set에 연결된 기본 축 |
| melee 1H / 2H / shield | Green | 근접 strike와 guard skill에 충분 |
| bow / ranged release | Green | marksman, arrow, projectile release에 적합 |
| thrown / grenade | Green | trap, toss, bomb형 스킬에 사용 가능 |
| spell direct / omni / special | Green | magical projectile, nova, heal, buff에 적합 |
| dodge / roll / dash | Green | reposition utility에 적합 |
| class-specific signature acting | Yellow | 캐릭터 고유 연기감은 blend/override 설계 필요 |

## Runtime reality

현재 C# 런타임은 raw asset 보유량을 그대로 쓰지 않는다. `BattleVfxCatalog` fallback은 windup, action commit, projectile, heal, impact, guard, reposition, death 같은 generic cue를 몇 개의 prefab으로만 연결한다.

따라서 다음 구현의 핵심은 에셋 확보가 아니라 resolver 설계다.

## Proposed taxonomy

다음 C# 구현은 최소한 아래 축을 가져야 한다.

| 축 | 책임 |
| --- | --- |
| `SkillPresentationFamily` | 스킬이 어떤 연출 문법을 쓰는지: melee, projectile, nova, zone, aura, heal, shield, debuff, dash |
| `SkillPresentationSkin` | 색감/질감/속성: fire, lightning, frost/glass, shadow/soul, echo/arcane, heal/gold, blood, guard/steel |
| `BattleAnimationSemantic` | actor gesture: melee attack, bow shot, spell direct, spell omni, guard, dodge, throw |
| `BattlePresentationCueSequence` | windup, release, travel, impact, linger, proc pulse의 순서 |
| `VfxHookId` | 스킬별 override와 도감 lookup anchor |

`VfxHookId`는 그대로 유지하되, 1차 bulk mapping은 hook id마다 prefab을 하나씩 찾는 방식이 아니라 family/skin을 통해 fallback을 공유해야 한다.

## Coverage grades

| 영역 | 판정 | 이유 |
| --- | --- | --- |
| 근접 물리 액티브 | Green | sword/brawling/hit와 1H/2H gesture가 충분 |
| 원거리 물리 액티브 | Green | bow/sharp missile/bullet류와 bow release가 충분 |
| 마법 projectile | Green | magic missile, charge, direct spell gesture가 충분 |
| 치유/보호막 | Green | healing/shield prefab과 omni/direct cast가 충분 |
| 이동/회피 utility | Green | dust/trail/dodge/roll/dash gesture가 충분 |
| aura/passive/support | Yellow | 수량이 많아 subtle variation 규칙 없으면 반복감이 큼 |
| 상태이상 | Yellow | 35개 스킬이 개입하므로 상태별 색/shape/glyph 규칙 필요 |
| zone/trap/linger | Yellow | 타일/범위 readability와 duration cleanup 규칙 필요 |
| signature fantasy | Red candidate | mirror, echo, prism, lattice 같은 이름은 prefab composition 후보 |

## Pilot skills

다음 구현은 아래 6개를 먼저 real prefab catalog에 연결해 검수하는 것이 좋다.

| 스킬 | 목표 |
| --- | --- |
| `skill_aegis_linebreaker` | 근접 물리 slash + impact |
| `skill_ember_arrow` | 물리 projectile + fire/ember accent |
| `skill_echo_resonance` | 마법 projectile + debuff/status accent |
| `skill_memory_tuning` | heal/support aura |
| `skill_aegis_sentinel_oath` | shield/nova guard signature |
| `skill_fracture_step` | zone 또는 reposition utility |

이 6개가 도감에서 명확히 구분되어 보이면 bulk mapping의 기준점이 생긴다.

## Next implementation order

1. C# content layer에 presentation family/skin/animation semantic을 추가할 위치를 결정한다.
2. 모든 스킬이 family, skin, animation semantic, hook id를 갖는지 검증하는 validator를 추가한다.
3. `BattleVfxCatalog`에 family/skin fallback resolver를 추가한다.
4. pilot 스킬 6개를 real prefab으로 연결한다.
5. 도감 VFX preview가 generic UITK 연출 대신 prefab playback을 선택적으로 호출하게 한다.
6. Green family를 bulk mapping하고 Yellow/Red 후보는 별도 art pass로 남긴다.

## Decision

이번 평가의 결론은 “에셋을 더 사야 한다”가 아니다. 지금 필요한 것은 스킬 presentation taxonomy와 검증 가능한 mapping table이다. 이 구조를 먼저 닫으면 이후 스킬 아이콘, VFX, 도감 검수, 상태이상 연출이 같은 언어로 이어진다.
