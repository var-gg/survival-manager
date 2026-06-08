# Survival Asset Studio

- 상태: draft
- 소유자: repository
- 관련 Pindoc: `pindoc://feature-asset-studio`

## 목적

Unity Editor 밖에서 `survival-manager`의 생성 미디어 자산을 읽기 전용으로 브라우징하고, `C:\projects\ai-infra` 계열 공용 HTTP 엔진의 상태를 확인하는 로컬 개발 앱이다.

이 앱은 모델을 포함하지 않는다. Chatterbox, ACE-Step, MOSS-SoundEffect, Wan, external image API wrapper는 ai-infra가 소유하고, 이 앱은 로컬 파일 scan과 HTTP health/start command만 담당한다.

## 실행

```powershell
cd tools/asset-studio
pnpm install
pnpm tauri dev
```

검증:

```powershell
pnpm build
cd src-tauri
cargo check
```

## 설정

기본값은 repo 위치와 `AI_INFRA_ROOT` 환경변수를 조합한다.

- `SURVIVAL_MANAGER_ROOT`: survival-manager repo root override
- `AI_INFRA_ROOT`: ai-infra root override
- `ASSET_STUDIO_CACHE_ROOT`: SQLite index와 썸네일 cache root override
- `ASSET_STUDIO_CONFIG`: JSON 설정 파일 경로
- `ASSET_STUDIO_CHATTERBOX_URL`: Chatterbox health/generate base URL override
- `ASSET_STUDIO_ACESTEP_URL`: ACE-Step base URL override
- `ASSET_STUDIO_SFX_URL`: MOSS-SoundEffect base URL override
- `ASSET_STUDIO_WAN_URL`: Wan base URL override

`ASSET_STUDIO_CONFIG`를 쓰는 경우:

```json
{
  "projectRoot": "A:\\projects\\game\\survival-manager",
  "aiInfraRoot": "C:\\projects\\ai-infra"
}
```

## 현재 범위

- 구현됨: 파일 scan, 타입 분류, gallery preview, JSON sidecar 표시, 엔진 health polling, start script trigger, Voice 탭 캐릭터 보이스 오디션(Chatterbox `/generate` POST + 인라인 재생)
- 성능 정책: 기본 scope는 selected/runtime/AI output만 스캔한다. raw output, reference, captures는 앱 sidebar의 `Include raw output and captures`를 켰을 때 포함된다.
- 성능 정책: 자산 목록은 `.cache/asset-index.sqlite`에 증분 인덱싱하고, 이미지 썸네일은 화면에 보이는 카드에서만 `.cache/thumbs/`로 지연 생성한다.
- 성능 정책: gallery는 virtual grid라서 필터 결과가 많아도 화면 근처 카드만 렌더링한다. audio/video element는 gallery 카드가 아니라 inspector에서만 로드한다.
- Unity 적용 구분: 이미지 asset의 `.meta` GUID와 `_Game`/`Resources/_Game` 직렬화 파일 참조를 읽어 `In Game`, `Unity Import`, `Import Queue`, `Raw/Reference`로 분류한다.
- 아직 아님: 이미지/BGM/SFX 생성 POST, ai-infra output import, selected/runtime promotion, Unity catalog 갱신
