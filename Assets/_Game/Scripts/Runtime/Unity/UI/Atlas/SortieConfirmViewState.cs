using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 편성 확인 overlay의 view-state. 전투에 나가는 6 anchor 배치와 활성 시너지를 한눈에 보여주고,
/// 슬롯을 클릭하면 그 anchor의 hero를 바로 바꿀 수 있다(자동배치를 "확인만" 하지 않고 즉시 조정).
/// 빈 배치(CanLaunch=false)면 출격을 막아 빈 파티 dead-end를 차단한다.
/// </summary>
public sealed record SortieConfirmViewState(
    string Title,
    string DeployHeader,
    string DeployHint,
    IReadOnlyList<SortieDeploySlotViewState> DeploySlots,
    int DeployCount,
    int DeployCap,
    IReadOnlyList<SortieSynergyChipViewState> SynergyChips,
    string SynergyEmptyText,
    bool CanLaunch,
    string LaunchLabel,
    string EditLabel,
    string CancelLabel);

/// <summary>배치 1슬롯 — anchor 위치 + hero 이름 + archetype 라벨 + 흉상. 클릭하면 이 anchor를 cycle한다.
/// PortraitSprite는 Presenter가 pre-resolve(View는 리소스 로딩 안 함). null이면 빈 슬롯 또는 미해상.</summary>
public sealed record SortieDeploySlotViewState(
    DeploymentAnchorId Anchor,
    string AnchorLabel,
    string HeroName,
    string ArchetypeName,
    bool IsEmpty,
    UnityEngine.Texture2D? PortraitSprite = null);

/// <summary>시너지 칩 — family 이름 + 현재 인원수 + minor/major breakpoint + major 도달 여부 + 활성 여부.
/// IsActive=false(count&lt;minor)는 회색 칩으로 "한 명만 더 모으면 켜진다"를 보여주는 교육용 노출이다.</summary>
public sealed record SortieSynergyChipViewState(
    string FamilyName,
    int Count,
    int Minor,
    int Major,
    bool MajorReached,
    bool IsActive);
