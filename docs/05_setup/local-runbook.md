# Local Runbook

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 현재 로컬 Unity truth를 기준으로 프로젝트를 실행/검증하는 절차를 정리한다.

## 현재 Unity truth

- 에디터 버전: `6000.4.0f1`
- 시작 씬: `Boot`
- 자동 검증 가능 흐름: `Boot -> Town`
- 수동 playable 목표 흐름: `Town -> Expedition -> Battle -> Reward -> Town`
- 빌드 등록 씬 순서:
  1. `Boot`
  2. `Town`
  3. `Expedition`
  4. `Battle`
  5. `Reward`

## 초기 실행 순서

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 패키지 import와 스크립트 컴파일이 끝날 때까지 기다린다.
3. `SM/Seed/Generate Sample Content`를 실행한다.
4. `SM/Validation/Validate Content Definitions`를 실행한다.
5. `Assets/_Game/Scenes/Boot.unity`를 연다.
6. Play를 누른다.
7. 정상이라면 `GameBootstrap`이 session root를 만들고 Town으로 진입한다.

## 첫 전투 화면까지 가는 수동 경로

1. `Boot.unity`를 연다.
2. Play를 누른다.
3. Town debug UI가 보이면 `Debug Start`를 누른다.
4. Expedition debug UI가 보이면 `Next Battle`을 누른다.
5. Battle debug UI 진입을 확인한다.

## PlayMode 테스트

현재 PlayMode smoke는 실제 씬 진입 기준으로 다음을 본다.

- Boot scene load
- `GameSessionRoot` 생성
- 가능 시 Boot -> Town 자동 진입

Battle/Reward 전체 자동화보다 먼저 Boot -> Town 안정화를 우선한다.

## 샘플 콘텐츠가 없을 때

- 에디터: 안내 로그를 띄우고 샘플 콘텐츠 생성을 유도한다.
- 런타임: 명확한 blocking error를 기록한다.

## 현재 상태 구분

### 실제 구현됨
- Boot scene load 기반 PlayMode smoke
- Boot -> Town 자동 진입 검증 시도
- session root / scene flow adapter
- Town/Expedition/Battle/Reward debug adapter 코드

### 아직 부분 구현/placeholder
- 전체 씬 wiring의 에디터 내 최종 확인
- Battle/Reward PlayMode integration test
- polished presentation/UI
