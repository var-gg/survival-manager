using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Passive Board V1 surface ViewState — pindoc V1 wiki SoT.
/// passive-board-node-catalog.md V1 floor: 4 board × 18 node (12 small + 5 notable + 1 keystone).
/// per-hero toggle 모델 (point-budget 없음). NODE POINTS 개념 제거.
/// </summary>
/// <summary>
/// Passive Board 헤더 — per-hero 컨텍스트. 보드는 hero의 클래스로 고정 (자유 탭 전환 아님).
/// PassiveBoardDefinition.ClassId = 클래스 단위 트리, HeroLoadoutRecord.SelectedPassiveNodeIds = hero 단위 선택.
/// </summary>
public sealed record PassiveBoardHeaderViewState(
    string HeroId,
    string HeroDisplayName,      // "팩 레이더 / Pack Raider" 등
    string ClassKey,             // vanguard / duelist / ranger / mystic
    string ClassLabel,           // "결투가 보드" — 어떤 클래스 보드인지 (고정 인디케이터)
    string BoardId,              // board_duelist — hero 클래스로 고정
    Texture2D? HeroPortrait,
    Texture2D? ClassIconSprite
);

public sealed record PassiveBoardNodeViewState(
    string NodeId,               // passive_duelist_keystone_01 등
    string KindKey,              // small / notable / keystone
    float Left,                  // 0.0..1.0 (percent position)
    float Top,                   // 0.0..1.0
    string IconKey,              // affix sprite proxy
    Texture2D? IconSprite,
    string RuleSummary,          // "phys_power +1.5, attack_speed +0.12" 등
    string Tags,                 // "frontline · burst" 등
    bool IsActive                // per-hero toggle state
);

public sealed record PassiveBoardDetailViewState(
    string SelectedNodeId,
    string KindLabel,            // "KEYSTONE" / "NOTABLE" / "SMALL"
    string TitleText,            // "KILLING INTENT" 등 (선택 노드의 신호명)
    string RuleSummary,          // "phys_power +1.5, attack_speed +0.12" — 선택 노드 효과
    string Tags,                 // "frontline · burst"
    string AvailableLabel,       // "ACTIVE" / "INACTIVE" / "LOCKED"
    string ButtonLabel,          // "ACTIVATE" / "DEACTIVATE"
    Texture2D? IconSprite
);

public sealed record PassiveBoardFooterViewState(
    string BreakdownText         // "DUELIST · SMALL 5/12 · NOTABLE 2/5 · KEYSTONE 1/1"
);

public sealed record PassiveBoardViewState(
    PassiveBoardHeaderViewState Header,
    IReadOnlyList<PassiveBoardNodeViewState> Nodes,
    PassiveBoardDetailViewState Detail,
    PassiveBoardFooterViewState Footer
);
