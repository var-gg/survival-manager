# 배치와 앵커

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/deployment-and-anchors.md`
- 관련문서:
  - `docs/01_product/vision.md`
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/combat-state-and-event-model.md`

## 목적

이 문서는 `battle deployment size = 4`를 유지하면서도, 플레이어가 "어디에 배치했는가"를 분명하게 느끼도록 앵커 규칙을 고정한다.

## 앵커 구조

- side마다 아래 6개 앵커를 가진다.
  - `FrontTop`
  - `FrontCenter`
  - `FrontBottom`
  - `BackTop`
  - `BackCenter`
  - `BackBottom`
- 실제 전투 투입 인원은 4명이므로 6칸 중 일부는 비어 있을 수 있다.
- 한 앵커에는 한 유닛만 둔다.

## 배치 페이즈 규칙

- Town과 Expedition 화면에서 배치 편집이 가능해야 한다.
- 현재 prototype은 drag-and-drop 대신 버튼 기반 cycle UI를 사용한다.
- 배치 편집은 session-scoped 상태로 유지한다.
- `BattleDeployHeroIds`는 compatibility용 파생 뷰로 남기고, 실제 truth는 앵커별 assignment다.

## 기본 배치 규칙

- hero가 명시 배치되지 않았으면 session이 기본 assignment를 채운다.
- archetype authored data는 preferred anchor를 가질 수 있다.
- Battle scene은 현재 session의 앵커 배치를 그대로 받아 전투를 시작한다.

## 적 배치 규칙

- 적도 같은 6개 앵커 체계를 사용한다.
- 현재 prototype에서는 적 배치가 encounter stub에 가깝지만, 이후 authored encounter 정의도 같은 앵커 모델을 따라야 한다.

## 설계 이유

- 완전 자유 배치보다 읽기 쉽다.
- 2-row 전투보다 배치 선택의 의미가 크다.
- continuous movement와 함께 써도 초기 formation 의미가 남는다.
