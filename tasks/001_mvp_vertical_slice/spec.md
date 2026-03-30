# 작업 명세: 001 MVP Vertical Slice

- 상태: draft
- 최종수정일: 2026-03-31
- 단계: prototype
- 작업 ID: 001

## 목적

이 문서는 prototype 단계의 첫 playable vertical slice 범위를 정의한다.
전체 production feature list로 확장하지 않기 위해 의도적으로 좁게 유지한다.

## 작업 목표

관리 중심 auto-battle 루프의 핵심 감각을 증명하는 **playable vertical slice at wooden-dummy fidelity**를 만든다.

## 성공 형태

이 slice는 최소한 아래를 보여 줄 수 있어야 한다.

- 허용된 구현 구역 안에서 조작 가능한 prototype path
- 전투 전 또는 전투 사이에 의미 있는 setup 결정
- 눈에 보이는 auto-battle resolution 또는 battle observation payoff
- 루프의 가능성을 판단할 수 있을 정도의 피드백

## 제약

- 구현 범위는 `Assets/_Game/**`, `Assets/Tests/**`로 제한한다.
- `Assets/ThirdParty/**` 원본은 직접 수정하지 않는다.
- 이 작업을 broad production architecture 확장으로 바꾸지 않는다.
- 문서와 구현 변경은 같은 기준으로 동기화한다.

## 이 작업 트랙의 범위 밖

- large-scale content production
- 광범위한 polish 작업
- production-quality balance
- slice 필요를 넘는 무제한 시스템 확장

## 열린 질문

- 이 slice를 가장 빨리 증명하는 최소 battle scenario는 무엇인가?
- fantasy를 증명하려면 어떤 setup 결정이 반드시 있어야 하는가?
- 목각인형 수준 fidelity에서 무엇을 가짜로 두거나 단순화할 수 있는가?
