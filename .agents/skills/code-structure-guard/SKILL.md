---
name: code-structure-guard
description: 구조 경계, asmdef 의존, 파일 책임을 점검하고 god file, 무분별한 interface, 상위 레이어로 새는 구현 세부를 막아야 할 때 사용한다. 구조 변경 리뷰, 코딩 거버넌스 점검, 새 파일/새 계층 추가 전 검토에 사용한다.
---

# Code Structure Guard

## 사용 시점

- 새 asmdef, 새 계층, 새 주요 타입을 추가할 때 사용한다.
- 큰 파일을 확장하거나 `Manager`/`Helper`가 생기기 시작할 때 사용한다.
- 구현 리뷰에서 구조 퇴행 가능성을 먼저 걸러야 할 때 사용한다.

## 반드시 막을 것

- 거대한 god file 생성
- 이유 없는 interface/abstract class 추가
- 상위 레이어 public API로 하위 구현 세부가 새는 구조
- `MonoBehaviour`가 battle truth나 save truth를 만드는 구조
- 문서 미갱신 상태로 구조 변경을 끝내는 흐름

## 실행 규칙

- 먼저 변경이 `SM.Core`, `SM.Content`, `SM.Combat`, `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor`, `SM.Tests` 중 어디에 속하는지 분류한다.
- 분류가 둘 이상이면 책임 분리를 먼저 제안한다.
- interface는 외부 경계, 대체 구현, 테스트 이점이 없으면 거부한다.
- `Manager`, `Helper`, `Util`, `Common` 이름이 나오면 파일 분리나 명명 재설계를 요구한다.
- 구조 정책이 바뀌면 관련 `docs/03_architecture/**`, `docs/00_governance/**`, ADR 갱신 여부를 같이 확인한다.

## 기대 출력

- 위반 여부와 근거가 한 줄씩 정리된 구조 검토 결과
- 필요한 파일 분리/의존 방향 수정 제안
- 문서와 ADR 갱신 필요 여부
