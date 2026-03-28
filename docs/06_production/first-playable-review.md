# First Playable Review

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 지금 직접 볼 수 있는 화면

- Boot scene 시작
- Town debug UI
- Expedition debug UI
- Battle debug UI
- Reward debug UI

단, Battle/Reward 포함 전체는 코드/adapter 기준으로 구성되어 있으며, Unity scene wiring 최종 확인은 별도로 필요하다.

## 실제로 동작하는 루프 구간

현재 기준으로 가장 보수적으로 확인 가능한 자동 구간은 다음이다.

- Boot scene load
- GameSessionRoot 생성
- Boot -> Town 자동 진입

수동 경로 기준 의도된 첫 playable 루프는 다음이다.

- Town -> Expedition -> Battle -> Reward -> Town

## 아직 placeholder인 구간

- Town UI는 debug placeholder UGUI
- Expedition 맵은 텍스트 기반 placeholder
- Battle은 primitive / 텍스트 로그 중심 placeholder
- Reward는 debug 3지선다 카드 placeholder
- 실제 씬 Canvas/Text/Button 참조 연결은 최종 확인 필요

## 다음 수정 우선순위 3개

1. Town / Expedition / Battle / Reward 씬 내부 Canvas와 버튼 참조를 실제로 연결하고 PlayMode에서 확인
2. Battle 결과 replay와 Reward 선택 반영을 PlayMode integration test 범위까지 확대
3. recruit / squad 편성 / deploy preview를 실제 상호작용 가능한 버튼 목록 UI로 정리
