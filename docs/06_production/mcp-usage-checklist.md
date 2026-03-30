# MCP 사용 체크리스트

- 상태: draft
- 최종수정일: 2026-03-29
- 소유자: repository

## 사용 전

- [ ] 설치 대상이 로컬 개발환경 전용인지 확인
- [ ] 런타임 코드와 MCP가 섞이지 않도록 경계 확인
- [ ] repo에 비밀값/개인 설정을 커밋하지 않는지 확인
- [ ] Unity 프로젝트 백업 또는 clean commit 상태 확보
- [ ] working tree clean 확인
- [ ] sandbox 브랜치 사용 확인
- [ ] 승인된 작업 범위 확인

## 첫 연결 검증

- [ ] 콘솔 읽기 성공
- [ ] scene hierarchy 조회 성공
- [ ] menu item 목록 확인 성공
- [ ] 승인된 menu item 1회 실행 성공
- [ ] 특정 scene load 성공
- [ ] 필요 시 해당 scene save 성공
- [ ] 특정 controller 존재 확인 성공

## 금지 항목 재확인

- [ ] `Assets/ThirdParty` 수정 안 함
- [ ] 대량 asset 삭제 안 함
- [ ] 무승인 import 안 함
- [ ] broad scene rewrite 안 함
- [ ] 승인 없는 package 변경 안 함

## 실패 시 복구

- [ ] MCP write 중단
- [ ] `git status` 확인
- [ ] 필요 파일 `git restore`
- [ ] 필요 시 branch reset
- [ ] 필요 시 scene reinstall bootstrap 재실행
- [ ] 재시도 전 원인 메모

## 작업 종료 전

- [ ] 변경 diff 검토
- [ ] sandbox 브랜치에만 변경이 있는지 확인
- [ ] 문서/노트에 검증 결과 기록
- [ ] 비밀값/개인 설정 유출 없는지 재확인

## MCP가 없어도 되는 작업

- 코드 편집
- 문서 작성
- ADR 업데이트
- 테스트 작성
- 텍스트 기반 검색

## MCP가 있으면 압도적으로 빨라지는 작업

- Unity console 에러 확인
- scene hierarchy 조사
- menu item 탐색/실행
- 특정 scene load/save 검증
- 특정 controller/object 존재 확인
