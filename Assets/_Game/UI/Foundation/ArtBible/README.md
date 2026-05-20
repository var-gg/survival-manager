# UI ArtBible Conformance

이 폴더는 Town production UI의 재질 언어를 파일 기준으로 고정한다.

- `artbible-role-registry.json`: 역할별 정식 chrome/button/material 규칙
- `artbible-exceptions.json`: Phase 1에서 실패 대신 경고로 남기는 임시 예외
- `UiArtBibleConformanceFastTests`: registry를 읽어 production Town UI의 오류/경고를 `Library/SM/Reports/ui-artbible-conformance.*`에 기록

운영 규칙은 단순하다. 최상위 modal만 ornate outer chrome을 쓰고, 내부 content/card/icon은 L2/L3/L4의 절제된 선과 면으로 표현한다. CTA 버튼은 `sm-cta` 역할 클래스로 수렴시키며, panel-local slice 복제는 경고로 추적한다.
