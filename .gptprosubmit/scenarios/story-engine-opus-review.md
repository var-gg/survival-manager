---
name: story-engine-opus-review
default_scope:
  slugs:
    - wiki-narrative-script-prologue
    - wiki-narrative-script-ch1
    - wiki-narrative-script-ch2
    - wiki-narrative-script-ch3
    - wiki-narrative-script-ch4
    - wiki-narrative-script-ch5
    - wiki-campaign-story-arc-mirror
    - analysis-narrative-tone-direction-v1
    - narrative-faction-conflict-driver-v1
    - wiki-chapter-beat-sheet-mirror
    - analysis-narrative-engine-retrofit-jrpg
    - wiki-character-hero-dawn-priest
    - wiki-character-hero-pack-raider
    - wiki-character-hero-grave-hexer
    - wiki-character-hero-echo-savant
  include_superseded: false
  exclude_templates: true
output_format: _output_format.md
---

# Scenario: story-engine-opus-review

작가·비평가 opus가 survival-manager 캠페인 스토리를 자체 검수한 결과(비평 기준 + 진단 + 개정 방향)를 GPT Pro로 냉정하게 종합 검증한다. 칭찬이 아니라 약점·반례·대안을 받는 게 목적이다.

## Prompt body

```text
[CONTEXT]
survival-manager는 다키스트 던전식 직군 고용 + 자동전투 + 짧은 호흡의 게임이고, 캠페인 스토리는 무겁고 비장한 정치극이다. 무대는 변방 한 지역과 1800년 전 '첫 도시'의 학살 비밀, 네 인간 세력(솔라룸·이리솔·회상 결사·그물 결사). 4 주역(단린=사제, 이빨바람=부족 지도자, 묵향=회상 결사, 공한=그물 결사 시조)이 변방문 붕괴를 계기로 동행하며 네 세력의 공동 죄와 마주한다. 적대자는 악당이 아니라 죽은 제도·맹세·노선에 묶인 사람들이다.

첨부 wiki dump: {{bundle_summary}}

[작가 opus의 자체 검수 — 이것을 검증·반박해 달라]

비평 기준 7: (1)고유함(절제·분산행위성·'분류=칼'테마·캐릭터 voice)을 흐리지 않는가 (2)모든 장면이 필연인가(페이싱) (3)갈등이 플레이와 하나인가(ludonarrative/전투 카타르시스) (4)캐릭터가 선명·매력적인가 (5)독자를 끌고 가는가(추진력, 클리셰 없이) (6)고유명사 진입장벽 (7)정서적 완결.

진단 — 강점: 테마 깊이, 캐릭터 voice, 구체적 이미지(한 단어·이름 없는 물건·같은 우물), 적대자 입체성, 결말 절제. 문학적으로 A급.
약점: ① 전투 카타르시스 부재 — 모든 보스가 고진형 '맹세에 묶인 선한 사람'이라, 자동전투를 수십 번 돌리는 게임에서 single note이고 악을 응징하는 통쾌함이 0. ② 페이싱 과중 — 챕터당 scene 8~15개, 무거운 텍스트가 짧은 호흡 게임에 안 맞음. ③ 캐릭터 매력 — 단린 성별이 본문 대사에서 모호(extract에서만 '그녀'), 수집/고용 로스터(12 직군)가 서사에서 엑스트라. ④ 고유명사 밀도 — 세력·인명(흑지·침월·회조·선영·백규)이 ch3~4에 급증. ⑤ ludonarrative 분리 — 수집·전투·플레이어 분신(한새는 거점에만 머묾)이 서사와 따로. ⑥ 회조(이빨바람의 피형제)의 실타래가 캠페인 내 미해소.

개정 방향 — 디렉터는 '영웅전설식 밝은 대모험 톤 전환(스케일 확대·페이지터너)'을 선호했으나, opus 판단으로는 그것이 이 작품의 고유성(절제·비장)을 깎으므로 철회. 대신 무겁고 비장한 정치극 톤(다키스트적 분위기)을 보존하면서 위 약점만 정밀 수술한다. 추진력은 클리셰('이건 시작이에요'류)가 아니라 미스터리의 절제된 당김 + 내레이터의 힘으로 푼다.

[TASK] 칭찬은 최소화하고 약점·반례·대안 위주로, dump의 실제 장면·대사를 직접 인용하며 냉정하게:
1. 이 비평 기준·진단·개정 방향이 이 작품을 '이 인물·이 무대 규모에서 도달 가능한 최고'로 만드는가? 동의/반대를 근거와 함께.
2. opus가 놓친 약점, 또는 과대평가한 강점은 무엇인가? 구체 장면·대사 단위로.
3. 무거운 정치극을 자동전투+직군고용 게임에 얹는 ludonarrative 해법으로 더 나은 길이 있는가?
4. 전투 카타르시스를 톤(비장·절제)을 훼손하지 않고 넣는 구체적 방법. opus의 '응징 가능한 적 섞기' 안의 위험과 더 나은 대안.
5. 6개 약점의 수술 우선순위 — 비용 대비 체감 개선이 큰 순서로.
6. 이 스토리가 정말 뛰어난 작품이 되려면 반드시 풀어야 할 '단 하나의 핵심 문제'를 꼽는다면?

{{output_format_block}}
```
