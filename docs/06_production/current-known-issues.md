# Current Known Issues

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## Scene Loop

- Town -> Expedition -> Battle -> Reward -> Town의 1회 루프 adapter는 구현됐지만, Unity 씬 wiring 최종 확인은 아직 필요하다.
- 현재 Battle/Reward는 debug placeholder 표현이다.

## Battle

- primitive actor와 텍스트 로그 중심 표현이다.
- race/class는 단색 머티리얼 규칙으로 단순 표시한다.
- battle event를 실제 애니메이션 타임라인으로 재생하지는 않는다.

## Reward

- reward card는 3지선다 debug UI다.
- gold/item/temporary augment 중 최소 2종 이상 지원한다.
- permanent augment 실획득 연출은 아직 없다.

## Persistence

- JSON fallback이 기본 경로다.
- reward 선택 결과는 session state와 save에 반영된다.
- Postgres adapter는 여전히 placeholder다.

## Presentation

- Town/Expedition/Battle/Reward 모두 placeholder UGUI 기준이다.
- 최종 HUD, 카드 연출, 맵 카메라, 배틀 VFX는 아직 없다.
