using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using SM.Unity;
using SM.Content.Definitions;

namespace SM.Editor.Bootstrap;

public static class LocalizationFoundationBootstrap
{
    public const string LocalizationRoot = "Assets/Localization";
    public const string SettingsPath = "Assets/Localization/Localization Settings.asset";
    public const string LocaleRoot = "Assets/Localization/Locales";
    public const string StringTableRoot = "Assets/Localization/StringTables";
    public const string AssetTableRoot = "Assets/Localization/AssetTables";
    public const string SharedFontSourcePath = "Assets/ThirdParty/Fonts/NotoSansCJK/NotoSansCJKkr-Regular.otf";
    public const string SharedFontCatalogPath = "Assets/Resources/_Game/Fonts/GameFontCatalog.asset";

    private static readonly string[] RequiredStringTables =
    {
        GameLocalizationTables.UICommon,
        GameLocalizationTables.UITown,
        GameLocalizationTables.UIExpedition,
        GameLocalizationTables.UIBattle,
        GameLocalizationTables.UIReward,
        GameLocalizationTables.CombatLog,
        GameLocalizationTables.SystemMessages,
        ContentLocalizationTables.Items,
        ContentLocalizationTables.Affixes,
        ContentLocalizationTables.Skills,
        ContentLocalizationTables.Augments,
        ContentLocalizationTables.Synergies,
        ContentLocalizationTables.Races,
        ContentLocalizationTables.Classes,
        ContentLocalizationTables.Characters,
        ContentLocalizationTables.Traits,
        ContentLocalizationTables.Archetypes,
        ContentLocalizationTables.Passives,
        ContentLocalizationTables.TeamTactics,
        ContentLocalizationTables.Roles,
        ContentLocalizationTables.Tags,
        ContentLocalizationTables.Stats,
        ContentLocalizationTables.Rewards,
        ContentLocalizationTables.Expedition,
    };

    private static readonly Dictionary<string, (string ko, string en, bool smart)> SharedEntries = new(StringComparer.Ordinal)
    {
        ["ui.common.language"] = ("언어", "Language", false),
        ["ui.common.current_language"] = ("현재 언어", "Current", false),
        ["ui.common.locale.korean"] = ("한국어", "Korean", false),
        ["ui.common.locale.english"] = ("영어", "English", false),
        ["ui.common.confirm"] = ("확인", "Confirm", false),
        ["ui.common.cancel"] = ("취소", "Cancel", false),
        ["ui.common.save"] = ("저장", "Save", false),
        ["ui.common.load"] = ("불러오기", "Load", false),
        ["ui.common.return_town"] = ("Town 복귀", "Return Town", false),
        ["ui.common.return_start"] = ("시작으로 돌아가기", "Return to Start", false),
        ["ui.common.settings"] = ("설정", "Settings", false),
        ["ui.common.pause"] = ("일시정지", "Pause", false),
        ["ui.common.resume"] = ("재개", "Resume", false),
        ["ui.common.continue"] = ("계속", "Continue", false),
        ["ui.common.start_screen.title"] = ("시작", "Start", false),
        ["ui.common.start_screen.status"] = ("이 빌드는 Town에서 준비하고 Expedition/Battle/Reward를 거치는 로컬 플레이 루프입니다.", "This build runs a local loop from Town through Expedition, Battle, and Reward.", false),
        ["ui.common.start_screen.hint"] = ("", "", false),
        ["ui.common.start_local_run"] = ("로컬 실행 시작", "Start Local Run", false),
        ["ui.common.on"] = ("ON", "ON", false),
        ["ui.common.off"] = ("OFF", "OFF", false),
        ["ui.common.empty"] = ("비어 있음", "Empty", false),
        ["ui.common.none"] = ("없음", "None", false),
        ["ui.common.selected"] = ("선택됨", "Selected", false),
        ["ui.common.here"] = ("현재", "Here", false),
        ["ui.common.locked"] = ("잠김", "Locked", false),
        ["ui.common.posture"] = ("전열 태세", "Posture", false),
        ["ui.common.deployment_setup"] = ("배치 설정", "Deployment Setup", false),
        ["ui.common.deploy_summary"] = ("배치 {0}/4", "Deploy {0}/4", true),
        ["ui.common.anchor.front_top"] = ("전열 상단", "Front Top", false),
        ["ui.common.anchor.front_center"] = ("전열 중앙", "Front Center", false),
        ["ui.common.anchor.front_bottom"] = ("전열 하단", "Front Bottom", false),
        ["ui.common.anchor.back_top"] = ("후열 상단", "Back Top", false),
        ["ui.common.anchor.back_center"] = ("후열 중앙", "Back Center", false),
        ["ui.common.anchor.back_bottom"] = ("후열 하단", "Back Bottom", false),
        ["ui.common.anchor.unknown"] = ("알 수 없는 배치", "Unknown Anchor", false),
        ["ui.town.title"] = ("Town 운영 화면", "Town Operator UI", false),
        ["ui.town.currency.summary"] = ("골드: {0} | Echo: {1} | 영구 슬롯: {2}", "Gold: {0} | Echo: {1} | Perm Slots: {2}", true),
        ["ui.town.roster.header"] = ("로스터", "Roster", false),
        ["ui.town.roster.tag.expedition"] = ("[원정]", "[Expedition]", false),
        ["ui.town.roster.tag.reserve"] = ("[대기]", "[Reserve]", false),
        ["ui.town.recruit.header"] = ("영입 후보", "Recruit Offers", false),
        ["ui.town.recruit.current_count"] = ("현재 후보 수: {0}", "Current offers: {0}", true),
        ["ui.town.recruit.cost"] = ("영입 비용: {0} Gold", "Recruit cost: {0} Gold", true),
        ["ui.town.recruit.reroll_cost"] = ("리롤 비용: {0} Gold", "Reroll cost: {0} Gold", true),
        ["ui.town.recruit.roster_count"] = ("Town 로스터: {0}/{1}", "Town roster: {0}/{1}", true),
        ["ui.town.recruit.empty"] = ("빈 슬롯", "Empty Slot", false),
        ["ui.town.recruit.none"] = ("영입 후보가 없습니다.", "No recruit offer is available.", false),
        ["ui.town.squad.header"] = ("원정 스쿼드 ({0}/8)", "Expedition Squad ({0}/8)", true),
        ["ui.town.deploy.header"] = ("배치 미리보기 ({0}/4)", "Deploy Preview ({0}/4)", true),
            ["ui.town.deploy.posture"] = ("팀 태세: {0}", "Team Posture: {0}", true),
            ["ui.town.deploy.quick_battle_ready"] = ("Quick Battle 스모크가 준비되었습니다.", "Quick battle smoke is ready.", false),
            ["ui.town.deploy.quick_battle_safe"] = ("Quick Battle은 원정 진행도를 소모하지 않습니다.", "Quick battle does not consume expedition progress.", false),
            ["ui.town.deploy.start"] = ("원정 시작: authored 경로를 야영지에서 시작합니다.", "Start Expedition: begin the authored route from camp.", false),
            ["ui.town.deploy.resume"] = ("원정 재개: {0}", "Resume Expedition: {0}", true),
        ["ui.town.deploy.last_reward"] = ("직전 보상: {0}", "Last Reward: {0}", true),
        ["ui.town.deploy.last_effect"] = ("직전 노드 효과: {0}", "Last Node Effect: {0}", true),
        ["ui.town.deploy.route_needed"] = ("경로 선택 필요", "Route selection required", false),
        ["ui.town.status.default.resume"] = ("영입, 저장, 원정 재개를 진행하세요.", "Recruit, save, or resume the expedition.", false),
        ["ui.town.status.default.start"] = ("영입, 저장, 원정 시작을 진행하세요.", "Recruit, save, or start the expedition.", false),
        ["ui.town.status.profile_loaded"] = ("프로필을 다시 불러왔습니다.", "Profile reloaded.", false),
        ["ui.town.status.profile_saved"] = ("프로필을 저장했습니다.", "Profile saved.", false),
        ["ui.town.status.recruit_success"] = ("후보 {0}을 영입했습니다. (-{1} Gold)", "Recruited offer {0}. (-{1} Gold)", true),
        ["ui.town.status.reroll_success"] = ("영입 후보를 리롤했습니다. (-{0} Gold)", "Recruit offers rerolled. (-{0} Gold)", true),
        ["ui.town.status.team_posture"] = ("팀 태세: {0}", "Team posture: {0}", true),
        ["ui.town.status.anchor_cycled"] = ("{0} 배치를 갱신했습니다.", "{0} deployment updated.", true),
        ["ui.town.error.recruit_failed"] = ("영입에 실패했습니다.", "Failed to recruit the selected offer.", false),
        ["ui.town.error.reroll_failed"] = ("영입 후보 리롤에 실패했습니다.", "Failed to reroll recruit offers.", false),
            ["ui.town.error.return_start_locked"] = ("진행 중인 런이 있어 지금은 시작 화면으로 돌아갈 수 없습니다.", "An active run is blocking Return to Start.", false),
            ["ui.town.action.debug_start"] = ("원정 시작", "Start Expedition", false),
            ["ui.town.action.recruit"] = ("영입", "Recruit", false),
            ["ui.town.action.reroll"] = ("리롤", "Reroll", false),
            ["ui.town.action.start_expedition"] = ("원정 시작", "Start Expedition", false),
            ["ui.town.action.quick_battle"] = ("Quick Battle", "Quick Battle", false),
            ["ui.town.action.resume_expedition"] = ("원정 재개", "Resume Expedition", false),
        ["ui.expedition.title"] = ("원정 운영 화면", "Expedition Operator UI", false),
        ["ui.expedition.position.summary"] = ("위치: {0}/{1} | 현재: {2} | 선택: {3}", "Position: {0}/{1} | Current: {2} | Selected: {3}", true),
        ["ui.expedition.position.none"] = ("선택 필요", "Selection Needed", false),
        ["ui.expedition.map.header"] = ("5노드 운영자 맵", "Five-node operator map", false),
        ["ui.expedition.map.marker.current"] = ("[현재]", "[Current]", false),
        ["ui.expedition.map.marker.selected"] = ("[선택]", "[Selected]", false),
        ["ui.expedition.map.marker.candidate"] = ("[후보]", "[Candidate]", false),
        ["ui.expedition.map.marker.completed"] = ("[완료]", "[Done]", false),
        ["ui.expedition.map.marker.upcoming"] = ("[예정]", "[Upcoming]", false),
        ["ui.expedition.map.battle"] = ("전투", "Battle", false),
        ["ui.expedition.map.travel"] = ("이동", "Travel", false),
        ["ui.expedition.reward.header"] = ("선택 경로 / 노드 효과", "Selected Route / Node Effect", false),
        ["ui.expedition.reward.planned"] = ("- 예정 보상: {0}", "- Planned Reward: {0}", true),
        ["ui.expedition.reward.effect"] = ("- 노드 효과: {0}", "- Node Effect: {0}", true),
        ["ui.expedition.reward.description"] = ("- 설명: {0}", "- Description: {0}", true),
        ["ui.expedition.reward.none"] = ("아직 선택된 분기가 없습니다.", "No branch is selected yet.", false),
        ["ui.expedition.reward.last_effect"] = ("직전 적용 효과: {0}", "Last Applied Effect: {0}", true),
        ["ui.expedition.squad.header"] = ("현재 원정 스쿼드", "Current Expedition Squad", false),
        ["ui.expedition.squad.posture"] = ("팀 태세", "Team Posture", false),
        ["ui.expedition.squad.deployment"] = ("배치", "Deployment", false),
        ["ui.expedition.squad.temp_augments"] = ("임시 증강", "Temp Augments", false),
        ["ui.expedition.status.select_route_first"] = ("다음 경로를 먼저 선택하세요.", "Select the next route first.", false),
        ["ui.expedition.status.team_posture"] = ("팀 태세: {0}", "Team posture: {0}", true),
        ["ui.expedition.status.route_selected"] = ("경로 {0}을 선택했습니다.", "Route {0} selected.", true),
        ["ui.expedition.status.node_cleared"] = ("{0} 경로를 전투 없이 정리했습니다.", "{0} was resolved without a battle.", true),
        ["ui.expedition.status.default"] = ("분기를 고른 뒤 다음 전투로 진행하세요.", "Choose a branch, then continue to the next battle.", false),
        ["ui.expedition.status.ready_battle"] = ("{0} 전투에 진입할 준비가 됐습니다.", "{0} is ready for battle.", true),
        ["ui.expedition.status.safe_node"] = ("{0}은 전투 없이 정리 가능한 안전 노드입니다.", "{0} can be cleared without battle.", true),
        ["ui.expedition.status.anchor_cycled"] = ("{0} 배치를 갱신했습니다.", "{0} deployment updated.", true),
        ["ui.expedition.error.no_deployable_heroes"] = ("배치 가능한 영웅이 없습니다.", "No hero is available for deployment.", false),
        ["ui.expedition.error.advance_failed"] = ("다음 노드 진행에 실패했습니다.", "Failed to advance the selected node.", false),
        ["ui.expedition.error.invalid_route"] = ("현재 위치에서 선택할 수 없는 노드입니다.", "That node cannot be selected from the current position.", false),
        ["ui.expedition.action.route"] = ("경로", "Route", false),
        ["ui.expedition.action.next_battle"] = ("다음 전투", "Next Battle", false),
        ["ui.expedition.effect.none"] = ("효과 없음", "No effect", false),
        ["ui.expedition.effect.gold"] = ("골드 +{0}", "+{0} Gold", true),
        ["ui.expedition.effect.reroll"] = ("에코 +{0}", "Echo +{0}", true),
        ["ui.expedition.effect.echo"] = ("에코 +{0}", "Echo +{0}", true),
        ["ui.expedition.effect.temp_augment"] = ("임시 증강: {0}", "Temp Augment: {0}", true),
        ["ui.expedition.effect.permanent_slot"] = ("영구 슬롯 +{0}", "Permanent Slot +{0}", true),
        ["ui.expedition.effect.return_town"] = ("원정을 종료하고 Town으로 복귀합니다.", "Expedition ended. Returning to Town.", false),
        ["ui.expedition.route.camp.label"] = ("야영지", "Camp", false),
        ["ui.expedition.route.camp.reward"] = ("정비 / 안전 이동", "Recovery / Safe Travel", false),
        ["ui.expedition.route.camp.desc"] = ("전투 없이 다음 분기를 고를 수 있는 시작 지점입니다.", "Opening node that lets you choose the next branch without battle.", false),
        ["ui.expedition.route.ambush.label"] = ("매복 경로", "Ambush Route", false),
        ["ui.expedition.route.ambush.reward"] = ("추가 골드 보상", "Extra Gold Reward", false),
        ["ui.expedition.route.ambush.desc"] = ("짧은 전투를 치르고 추가 골드를 확보합니다.", "Fight a short battle to secure extra gold.", false),
        ["ui.expedition.route.relay.label"] = ("보급 경로", "Relay Route", false),
        ["ui.expedition.route.relay.reward"] = ("Echo 회수 보상", "Echo Recovery Reward", false),
        ["ui.expedition.route.relay.desc"] = ("전투 후 Echo를 회수해 모집 RNG를 복구하는 경로입니다.", "Battle for Echo that recovers recruit RNG.", false),
        ["ui.expedition.route.shrine.label"] = ("제단 경로", "Shrine Route", false),
        ["ui.expedition.route.shrine.reward"] = ("임시 증강 확보", "Temporary Augment", false),
        ["ui.expedition.route.shrine.desc"] = ("강한 전투 후 임시 증강을 챙길 수 있습니다.", "Win a harder battle to claim a temporary augment.", false),
        ["ui.expedition.route.extract.label"] = ("철수 경로", "Extract Route", false),
        ["ui.expedition.route.extract.reward"] = ("귀환 보상", "Extraction Reward", false),
        ["ui.expedition.route.extract.desc"] = ("안전하게 귀환하며 마지막 보상을 챙깁니다.", "Safely return and claim the final reward.", false),
        ["ui.reward.title"] = ("보상 운영 화면", "Reward Operator UI", false),
        ["ui.reward.summary.battle_result"] = ("전투 결과: {0}", "Battle Result: {0}", true),
        ["ui.reward.summary.gold"] = ("골드: {0}", "Gold: {0}", true),
        ["ui.reward.summary.reroll"] = ("에코: {0}", "Echo: {0}", true),
        ["ui.reward.summary.echo"] = ("에코: {0}", "Echo: {0}", true),
        ["ui.reward.summary.slots"] = ("영구 슬롯: {0}", "Perm Slots: {0}", true),
        ["ui.reward.summary.inventory"] = ("인벤토리: {0}", "Inventory: {0}", true),
        ["ui.reward.summary.temp_augments"] = ("임시 증강: {0}", "Temp Augments: {0}", true),
        ["ui.reward.battle_summary.base"] = ("{0} / {1} 스텝 / 이벤트 {2}개", "{0} / {1} steps / {2} events", true),
        ["ui.reward.battle_summary.route"] = ("{0} / {1} steps / {2} events\n경로: {3}", "{0} / {1} steps / {2} events\nRoute: {3}", true),
        ["ui.reward.battle_summary.route_effect"] = ("{0} / {1} steps / {2} events\n경로: {3}\n노드 효과: {4}", "{0} / {1} steps / {2} events\nRoute: {3}\nNode Effect: {4}", true),
        ["ui.reward.result.victory"] = ("승리", "Victory", false),
        ["ui.reward.result.defeat"] = ("패배", "Defeat", false),
        ["ui.reward.choices.header"] = ("보상 카드 한 장 선택", "Choose one reward card", false),
        ["ui.reward.status.default"] = ("카드를 하나 고르고 Town으로 돌아가세요.", "Pick one card and return to town.", false),
        ["ui.reward.status.choice_applied"] = ("보상을 적용했습니다.", "Reward applied.", false),
        ["ui.reward.status.choice_applied_named"] = ("{0} 보상을 적용했습니다.", "{0} applied.", true),
        ["ui.reward.error.choice_failed"] = ("보상 선택에 실패했습니다.", "Failed to apply the selected reward.", false),
        ["ui.reward.action.choose"] = ("선택", "Choose", false),
        ["ui.reward.choice.empty"] = ("빈 카드", "Empty Card", false),
        ["ui.reward.choice.none"] = ("선택지가 없습니다.", "No reward choice is available.", false),
        ["ui.reward.kind.gold"] = ("골드 +{0}", "Gold +{0}", true),
        ["ui.reward.kind.item"] = ("아이템 / {0}", "Item / {0}", true),
        ["ui.reward.kind.temp_augment"] = ("임시 증강 / {0}", "Temp / {0}", true),
        ["ui.reward.kind.reroll"] = ("에코 +{0}", "Echo +{0}", true),
        ["ui.reward.kind.echo"] = ("에코 +{0}", "Echo +{0}", true),
        ["ui.reward.kind.permanent_slot"] = ("영구 슬롯 +{0}", "Permanent Slot +{0}", true),
        ["ui.reward.choice.gold_cache.title"] = ("골드 보관함", "Gold Cache", false),
        ["ui.reward.choice.gold_cache.desc"] = ("즉시 골드를 확보합니다.", "Take an immediate gold payout.", false),
        ["ui.reward.choice.iron_blade.title"] = ("철날 보급", "Iron Blade Supply", false),
        ["ui.reward.choice.iron_blade.desc"] = ("기본 무기 아이템을 획득합니다.", "Gain a baseline weapon item.", false),
        ["ui.reward.choice.aggro_spark.title"] = ("공세 점화", "Aggro Spark", false),
        ["ui.reward.choice.aggro_spark.desc"] = ("공격적인 임시 증강을 획득합니다.", "Gain an aggressive temporary augment.", false),
        ["ui.reward.choice.war_chest.title"] = ("전쟁 상자", "War Chest", false),
        ["ui.reward.choice.war_chest.desc"] = ("매복 경로 전용 대형 골드 보상입니다.", "Large gold reward for the ambush route.", false),
        ["ui.reward.choice.hook_spear.title"] = ("갈고리 창", "Hook Spear", false),
        ["ui.reward.choice.hook_spear.desc"] = ("전열 교전용 아이템을 획득합니다.", "Gain a frontline skirmish item.", false),
        ["ui.reward.choice.scout_intel.title"] = ("정찰 보고서", "Scout Intel", false),
        ["ui.reward.choice.scout_intel.desc"] = ("다음 recruit/retrain 복구용 Echo를 추가로 획득합니다.", "Gain extra Echo for recruit and retrain recovery.", false),
        ["ui.reward.choice.field_kit.title"] = ("현장 수리 키트", "Field Kit", false),
        ["ui.reward.choice.field_kit.desc"] = ("안정적인 방어형 아이템을 획득합니다.", "Gain a steady defensive item.", false),
        ["ui.reward.choice.anchor_beat.title"] = ("고정 박동", "Anchor Beat", false),
        ["ui.reward.choice.anchor_beat.desc"] = ("전열 유지용 임시 증강을 획득합니다.", "Gain a frontline stabilizing augment.", false),
        ["ui.reward.choice.relay_pouch.title"] = ("보급 주머니", "Relay Pouch", false),
        ["ui.reward.choice.relay_pouch.desc"] = ("중간 규모의 골드 보상을 획득합니다.", "Gain a medium gold payout.", false),
        ["ui.reward.choice.permanent_socket.title"] = ("영구 소켓", "Permanent Socket", false),
        ["ui.reward.choice.permanent_socket.desc"] = ("영구 증강 슬롯을 확장합니다.", "Increase permanent augment slots.", false),
        ["ui.reward.choice.sigil_core.title"] = ("시질 코어", "Sigil Core", false),
        ["ui.reward.choice.sigil_core.desc"] = ("후반 대비용 아이템을 획득합니다.", "Gain a late-run utility item.", false),
        ["ui.reward.choice.doctrine_cache.title"] = ("교리 보관고", "Doctrine Cache", false),
        ["ui.reward.choice.doctrine_cache.desc"] = ("Echo를 크게 확보합니다.", "Gain a larger Echo payout.", false),
        ["ui.reward.choice.fallback_stash.title"] = ("비상 보급함", "Fallback Stash", false),
        ["ui.reward.choice.fallback_stash.desc"] = ("패배 후 최소 보상을 챙깁니다.", "Collect a minimum fallback payout after defeat.", false),
        ["ui.reward.choice.tactical_notes.title"] = ("전술 메모", "Tactical Notes", false),
        ["ui.reward.choice.tactical_notes.desc"] = ("전술 보정용 Echo를 획득합니다.", "Gain Echo for tactical correction.", false),
        ["ui.reward.choice.guard_spark.title"] = ("수호 점화", "Guard Spark", false),
        ["ui.reward.choice.guard_spark.desc"] = ("안정화용 임시 증강을 획득합니다.", "Gain a stabilizing temporary augment.", false),
        ["ui.battle.title"] = ("전투 관전 화면", "Battle Observer UI", false),
        ["ui.battle.hp.allies"] = ("아군 HP", "Allied HP", false),
        ["ui.battle.hp.enemies"] = ("적군 HP", "Enemy HP", false),
        ["ui.battle.log.preparing"] = ("전투 로그 준비 중", "Preparing battle log", false),
        ["ui.battle.result.in_progress"] = ("전투 진행 중", "Battle in progress", false),
        ["ui.battle.result.victory"] = ("승리", "Victory", false),
        ["ui.battle.result.defeat"] = ("패배", "Defeat", false),
        ["ui.battle.speed.active"] = ("배속 x{0:0}", "Speed x{0:0}", true),
        ["ui.battle.speed.paused"] = ("배속 x{0:0} | 일시정지", "Speed x{0:0} | Paused", true),
        ["ui.battle.status.initializing"] = ("실시간 시뮬레이션 초기화", "Initializing live simulation", false),
        ["ui.battle.status.paused"] = ("전투 일시정지", "Battle paused", false),
        ["ui.battle.status.resumed"] = ("전투 재개", "Battle resumed", false),
        ["ui.battle.status.pause_suffix"] = (" | 일시정지", " | Paused", false),
        ["ui.battle.status.finished"] = ("전투 종료 | {0} steps | {1} events", "Battle finished | {0} steps | {1} events", true),
        ["ui.battle.status.resolved"] = ("Step {0} | 결과 확정{1}", "Step {0} | Result resolved{1}", true),
        ["ui.battle.status.last_event"] = ("스텝 {0} | {1} -> {2} | {3} {4:0}{5}", "Step {0} | {1} -> {2} | {3} {4:0}{5}", true),
        ["ui.battle.status.windup"] = ("Step {0} | {1} 준비 {2}% -> {3}{4}", "Step {0} | {1} windup {2}% -> {3}{4}", true),
        ["ui.battle.status.posture"] = ("스텝 {0} | 태세 {1}{2}", "Step {0} | posture {1}{2}", true),
        ["ui.battle.error.no_allies"] = ("전투에 투입할 아군이 없습니다.", "No allied unit is ready for battle.", false),
        ["ui.battle.settings.title"] = ("전투 표시 옵션", "Battle View Settings", false),
        ["ui.battle.settings.closed"] = ("설정 패널 닫힘", "Settings panel closed", false),
        ["ui.battle.settings.state_changed"] = ("{0}: {1}", "{0}: {1}", true),
        ["ui.battle.settings.world_hp_label"] = ("오버헤드 UI", "Overhead UI", false),
        ["ui.battle.settings.overlay_hp_label"] = ("데미지 숫자", "Damage Text", false),
        ["ui.battle.settings.overhead_ui_label"] = ("오버헤드 UI", "Overhead UI", false),
        ["ui.battle.settings.damage_text_label"] = ("데미지 숫자", "Damage Text", false),
        ["ui.battle.settings.debug_overlay_label"] = ("디버그 오버레이", "Debug Overlay", false),
        ["ui.battle.axis.character"] = ("캐릭터", "Character", false),
        ["ui.battle.axis.archetype"] = ("전투 원형", "Archetype", false),
        ["ui.battle.axis.race"] = ("종족", "Race", false),
        ["ui.battle.axis.class"] = ("직업", "Class", false),
        ["ui.battle.axis.role"] = ("역할", "Role", false),
        ["ui.battle.axis.role_family"] = ("역할군", "Role Family", false),
        ["ui.battle.axis.anchor"] = ("배치", "Anchor", false),
        ["ui.battle.selected.header"] = ("선택 유닛", "Selected Unit", false),
        ["ui.battle.settings.team_summary_label"] = ("팀 요약", "Team Summary", false),
        ["ui.battle.settings.world_hp"] = ("오버헤드 UI {0}", "Overhead UI {0}", true),
        ["ui.battle.settings.overlay_hp"] = ("데미지 숫자 {0}", "Damage Text {0}", true),
        ["ui.battle.settings.overhead_ui"] = ("오버헤드 UI {0}", "Overhead UI {0}", true),
        ["ui.battle.settings.damage_text"] = ("데미지 숫자 {0}", "Damage Text {0}", true),
        ["ui.battle.settings.debug_overlay"] = ("디버그 오버레이 {0}", "Debug Overlay {0}", true),
        ["ui.battle.settings.team_summary"] = ("팀 요약 {0}", "Team Summary {0}", true),
        ["system.bootstrap.missing_sample_content.editor"] = ("샘플 콘텐츠 canonical root가 비어 있습니다. 먼저 SM/Setup/Ensure Sample Content를 실행하고, 복구가 안 되면 SM/Setup/Generate Sample Content를 repair 용도로 실행한 뒤 다시 Play 하세요.", "Sample content canonical root is empty. Run SM/Setup/Ensure Sample Content first, and use SM/Setup/Generate Sample Content only as a repair path before entering Play mode.", false),
        ["system.bootstrap.missing_sample_content.player"] = ("필수 샘플 콘텐츠 canonical root가 비어 있어 시작할 수 없습니다.", "Required sample content canonical root is empty, so boot cannot continue.", false),
        ["system.runtime.missing_root"] = ("GameSessionRoot가 없습니다.", "GameSessionRoot is missing.", false),
        ["combat.log.damage"] = ("S{0} {1}이(가) {2}에게 {3:0} 피해", "S{0} {1} dealt {3:0} damage to {2}", true),
        ["combat.log.heal"] = ("S{0} {1}이(가) {2}를 {3:0} 회복", "S{0} {1} healed {2} for {3:0}", true),
        ["combat.log.skill"] = ("S{0} {1}이(가) {2}에게 스킬 {3:0}", "S{0} {1} used a skill on {2} for {3:0}", true),
        ["combat.log.guard"] = ("S{0} {1}이(가) 방어 자세", "S{0} {1} took a guard stance", true),
        ["combat.log.status_applied"] = ("S{0} {1}이(가) {3}에게 {2} 적용", "S{0} {1} applied {2} to {3}", true),
        ["combat.log.status_removed"] = ("S{0} {1}에서 {2} 해제", "S{0} {1} removed {2}", true),
        ["combat.log.cleanse"] = ("S{0} {1}이(가) {3}에게 {2} 정화", "S{0} {1} cleansed {2} on {3}", true),
        ["combat.log.control_resist"] = ("S{0} {1} control resist 획득", "S{0} {1} gained control resist", true),
        ["combat.log.generic"] = ("S{0} {1} {2}", "S{0} {1} {2}", true),
    };

    [MenuItem("SM/Setup/Ensure Localization Foundation")]
    public static void EnsureFoundationMenu()
    {
        EnsureFoundationAssets();
    }

    public static void EnsureFoundationAssets()
    {
        EnsureClosureEntries();
        EnsureFolders();
        EnsureSharedFontCatalog();

        var settings = EnsureLocalizationSettings();
        var locales = EnsureLocales();
        ConfigureStartupSelectors(settings);
        EnsureStringTables(locales);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureClosureEntries()
    {
        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.common.help"] = ("도움말", "Help", false),
            ["ui.common.hide"] = ("닫기", "Hide", false),
            ["ui.common.unknown"] = ("알 수 없음", "Unknown", false),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.town.panel.campaign"] = ("캠페인", "Campaign", false),
            ["ui.town.panel.economy"] = ("경제", "Economy", false),
            ["ui.town.panel.roster"] = ("로스터", "Roster", false),
            ["ui.town.panel.blueprint"] = ("빌드", "Build", false),
            ["ui.town.panel.recruit"] = ("영입", "Recruit", false),
            ["ui.town.panel.selected_hero"] = ("선택 영웅", "Selected Hero", false),
            ["ui.town.panel.deploy"] = ("스쿼드 / 배치", "Squad / Deploy", false),
            ["ui.town.group.primary"] = ("주요 진행", "Primary", false),
            ["ui.town.group.gameplay"] = ("게임플레이", "Gameplay", false),
            ["ui.town.group.utility"] = ("유틸리티", "Utility", false),
            ["ui.town.group.debug"] = ("디버그 / 스모크", "Debug / Smoke", false),
            ["ui.town.help.body"] = ("1) 챕터/지점 확인 2) 영입/배치 확인 3) 태세 설정 4) 원정 시작", "1) Check chapter/site 2) confirm recruit and deploy 3) set posture 4) start the expedition.", false),
            ["ui.town.action.prev_chapter"] = ("이전 챕터", "Prev Chapter", false),
            ["ui.town.action.next_chapter"] = ("다음 챕터", "Next Chapter", false),
            ["ui.town.action.prev_site"] = ("이전 지점", "Prev Site", false),
            ["ui.town.action.next_site"] = ("다음 지점", "Next Site", false),
            ["ui.town.action.quick_battle_smoke"] = ("Quick Battle (Smoke)", "Quick Battle (Smoke)", false),
            ["ui.town.action.cycle_hero"] = ("영웅 순환", "Cycle Hero", false),
            ["ui.town.action.cycle_item"] = ("아이템 순환", "Cycle Item", false),
            ["ui.town.action.scout"] = ("정찰", "Scout", false),
            ["ui.town.action.retrain_active"] = ("액티브 재훈련", "Retrain Flex Active", false),
            ["ui.town.action.retrain_passive"] = ("패시브 재훈련", "Retrain Flex Passive", false),
            ["ui.town.action.full_retrain"] = ("전체 재훈련", "Full Retrain", false),
            ["ui.town.action.dismiss"] = ("해산", "Dismiss", false),
            ["ui.town.action.refit"] = ("선택 아이템 개조", "Refit Selected Item", false),
            ["ui.town.action.cycle_board"] = ("보드 순환", "Cycle Board", false),
            ["ui.town.action.cycle_node"] = ("노드 순환", "Cycle Node", false),
            ["ui.town.action.toggle_node"] = ("노드 토글", "Toggle Node", false),
            ["ui.town.action.cycle_permanent"] = ("영구 증강 순환", "Cycle Permanent", false),
            ["ui.town.action.equip_permanent"] = ("영구 증강 장착", "Equip Permanent", false),
            ["ui.town.tooltip.team_posture"] = ("현재 팀 태세를 전투 준비도와 배치에 반영합니다.", "Sets the current team posture used for deploy and battle readiness.", false),
            ["ui.town.tooltip.quick_battle"] = ("authored 캠페인 진행을 건드리지 않고 스모크 전투만 엽니다.", "Open the smoke-only debug battle lane without altering authored campaign progress.", false),
            ["ui.town.tooltip.quick_battle_smoke"] = ("현재 Town 빌드 상태로 integration smoke 전투를 열고, Reward 또는 직접 Town 복귀 의미를 유지합니다.", "Open an integration smoke battle using the current Town build, then return through Reward or direct Town restore.", false),
            ["ui.town.tooltip.return_start"] = ("로컬 런 shell을 종료하고 Boot로 돌아갑니다.", "Leave the local run shell and go back to Boot.", false),
            ["ui.town.tooltip.return_start_locked"] = ("authored run 또는 smoke battle이 열려 있으면 막힙니다.", "Blocked while an authored run or smoke battle is still active.", false),
            ["ui.town.tooltip.expedition_start"] = ("현재 준비 상태로 authored 원정 루프를 시작합니다.", "Begin the authored expedition loop with the current preparation.", false),
            ["ui.town.tooltip.expedition_resume"] = ("현재 선택된 authored 경로에서 원정을 재개합니다.", "Resume the authored expedition from the currently selected route.", false),
            ["ui.town.tooltip.expedition_reward"] = ("이전 노드 정산을 위해 Reward를 먼저 엽니다.", "Open Reward to settle the previous node before continuing.", false),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.town.campaign.chapter"] = ("챕터: {0}", "Chapter: {0}", true),
            ["ui.town.campaign.site"] = ("지점: {0}", "Site: {0}", true),
            ["ui.town.campaign.selection_state"] = ("선택 상태: {0}", "Selection: {0}", true),
            ["ui.town.campaign.objective"] = ("목표: {0}", "Objective: {0}", true),
            ["ui.town.campaign.unlocked"] = ("새 authored 경로를 시작할 수 있습니다.", "Unlocked for a new authored route.", false),
            ["ui.town.campaign.locked"] = ("현재 run이 유지되는 동안 잠깁니다.", "Locked while the current run stays active.", false),
            ["ui.town.campaign.pending_reward"] = ("정산 대기: run 재개 전에 Reward를 먼저 여세요.", "Pending settlement: open Reward before resuming the run.", false),
            ["ui.town.campaign.active_run"] = ("활성 런: node {0}/{1} 에서 재개할 수 있습니다.", "Active run: node {0}/{1} is ready for resume.", true),
            ["ui.town.campaign.quick_battle_ready"] = ("Quick Battle (Smoke)는 보조 debug lane으로 사용할 수 있습니다.", "Quick Battle (Smoke) is available as a secondary debug lane.", false),
            ["ui.town.campaign.quick_battle_locked"] = ("현재 expedition run이 닫히기 전까지 Quick Battle (Smoke)는 잠깁니다.", "Quick Battle (Smoke) is locked until the current expedition run is closed.", false),
            ["ui.town.deploy.squad_count"] = ("스쿼드: {0}/{1}", "Squad: {0}/{1}", true),
            ["ui.town.deploy.readiness"] = ("준비 상태: {0}", "Readiness: {0}", true),
            ["ui.town.deploy.ready"] = ("준비 완료", "ready", false),
            ["ui.town.deploy.not_ready"] = ("경로 선택 필요", "route selection needed", false),
            ["ui.town.deploy.primary_start"] = ("주요 행동: 원정 시작", "Primary Action: Start Expedition", false),
            ["ui.town.deploy.primary_resume"] = ("주요 행동: 원정 재개", "Primary Action: Resume Expedition", false),
            ["ui.town.deploy.primary_reward"] = ("주요 행동: 보상 열기", "Primary Action: Open Reward", false),
            ["ui.town.deploy.pending_reward"] = ("대기 중인 보상: 이전 노드 정산이 남아 있습니다.", "Pending Reward: previous node settlement is waiting.", false),
            ["ui.town.deploy.last_permanent_unlock"] = ("마지막 영구 해금: {0}", "Last Permanent Unlock: {0}", true),
            ["ui.town.economy.gold"] = ("골드: {0}", "Gold: {0}", true),
            ["ui.town.economy.echo"] = ("에코: {0}", "Echo: {0}", true),
            ["ui.town.economy.refresh_cost"] = ("갱신 비용: {0}", "Refresh Cost: {0}", true),
            ["ui.town.economy.refresh_state"] = ("갱신 상태: {0}", "Refresh State: {0}", true),
            ["ui.town.economy.refresh_available"] = ("사용 가능", "available", false),
            ["ui.town.economy.refresh_used"] = ("사용 완료", "used", false),
            ["ui.town.economy.scout"] = ("정찰: {0}", "Scout: {0}", true),
            ["ui.town.economy.available"] = ("사용 가능", "available", false),
            ["ui.town.economy.used"] = ("사용 완료", "used", false),
            ["ui.town.economy.rare_pity"] = ("희귀 보정: {0}", "Rare Pity: {0}", true),
            ["ui.town.economy.epic_pity"] = ("에픽 보정: {0}", "Epic Pity: {0}", true),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.town.recruit.badge.on_plan"] = ("[계획 일치]", "[On Plan]", false),
            ["ui.town.recruit.badge.protected"] = ("[보호]", "[Protected]", false),
            ["ui.town.recruit.badge.scout"] = ("[정찰]", "[Scout]", false),
            ["ui.town.recruit.badge.protected_scout"] = ("[보호][정찰]", "[Protected][Scout]", false),
            ["ui.town.recruit.slot.standard_a"] = ("표준 A", "Standard A", false),
            ["ui.town.recruit.slot.standard_b"] = ("표준 B", "Standard B", false),
            ["ui.town.recruit.slot.on_plan"] = ("계획 일치", "On Plan", false),
            ["ui.town.recruit.slot.protected"] = ("보호", "Protected", false),
            ["ui.town.recruit.tier.common"] = ("일반", "Common", false),
            ["ui.town.recruit.tier.rare"] = ("희귀", "Rare", false),
            ["ui.town.recruit.tier.epic"] = ("에픽", "Epic", false),
            ["ui.town.recruit.plan.on_plan"] = ("계획 일치", "On Plan", false),
            ["ui.town.recruit.plan.off_plan"] = ("계획 외", "Off Plan", false),
            ["ui.town.recruit.plan.bridge"] = ("브리지 픽", "Bridge Pick", false),
            ["ui.town.recruit.formation.frontline"] = ("전열", "Frontline", false),
            ["ui.town.recruit.formation.midline"] = ("중열", "Midline", false),
            ["ui.town.recruit.formation.backline"] = ("후열", "Backline", false),
            ["ui.town.recruit.tags"] = ("태그", "Tags", false),
            ["ui.town.recruit.counter"] = ("카운터", "Counter", false),
            ["ui.town.recruit.tooltip.summary"] = ("{0}. 티어 {1}. {2}. {3}.", "{0}. Tier {1}. {2}. {3}.", true),
            ["ui.town.recruit.tooltip.empty"] = ("현재 offer가 없는 빈 슬롯입니다.", "This recruit slot is currently empty.", false),
            ["ui.town.recruit.tooltip.standard"] = ("이번 Town 방문의 기본 영입 슬롯입니다.", "Standard recruit slot for the current Town visit.", false),
            ["ui.town.recruit.tooltip.on_plan"] = ("현재 팀 계획과 잘 맞는 영입입니다.", "Recruit that matches the current team plan.", false),
            ["ui.town.recruit.tooltip.protected"] = ("이번 Town 방문 동안 유지되는 보호 영입입니다.", "Protected recruit that stays available for this Town visit.", false),
            ["ui.town.scout.frontline"] = ("전열", "Frontline", false),
            ["ui.town.scout.backline"] = ("후열", "Backline", false),
            ["ui.town.scout.physical"] = ("물리", "Physical", false),
            ["ui.town.scout.magical"] = ("마법", "Magical", false),
            ["ui.town.scout.support"] = ("지원", "Support", false),
            ["ui.town.selected_hero.retrain_active"] = ("액티브 재훈련: {0}", "Active Retrain: {0}", true),
            ["ui.town.selected_hero.retrain_passive"] = ("패시브 재훈련: {0}", "Passive Retrain: {0}", true),
            ["ui.town.selected_hero.retrain_full"] = ("전체 재훈련: {0}", "Full Retrain: {0}", true),
            ["ui.town.selected_hero.dismiss_refund"] = ("해산 환급: +{0} Gold / +{1} Echo", "Dismiss Refund: +{0} Gold / +{1} Echo", true),
            ["ui.town.selected_hero.refit_preview"] = ("개조 미리보기: {0}", "Refit Preview: {0}", true),
            ["ui.town.selected_hero.passive_nodes"] = ("패시브 노드: {0}", "Passive Nodes: {0}", true),
            ["ui.town.permanent.equipped"] = ("장착 중", "Equipped", false),
            ["ui.town.error.chapter_cycle_failed"] = ("챕터 선택을 바꿀 수 없습니다.", "Chapter selection could not be changed.", false),
            ["ui.town.error.chapter_locked"] = ("원정 런이 활성 상태라 챕터/지점이 잠겨 있습니다.", "Chapter and site are locked while an expedition run is active.", false),
            ["ui.town.error.site_cycle_failed"] = ("지점 선택을 바꿀 수 없습니다.", "Site selection could not be changed.", false),
            ["ui.town.error.site_locked"] = ("원정 런이 활성 상태라 챕터/지점이 잠겨 있습니다.", "Chapter and site are locked while an expedition run is active.", false),
            ["ui.town.error.quick_battle_locked"] = ("authored 원정 런이 열려 있는 동안 Quick Battle (Smoke)는 사용할 수 없습니다.", "Quick Battle (Smoke) is locked while an authored expedition run is active.", false),
            ["ui.town.error.scout_failed"] = ("정찰에 실패했습니다.", "Scout action failed.", false),
            ["ui.town.status.chapter_changed"] = ("캠페인 챕터를 변경했습니다.", "Campaign chapter updated.", false),
            ["ui.town.status.site_changed"] = ("원정 지점을 변경했습니다.", "Expedition site updated.", false),
            ["ui.town.status.default.reward"] = ("이전 노드 보상을 열고 런을 이어가세요.", "Open the pending reward, then continue the run.", false),
            ["ui.town.status.scout_used"] = ("정찰을 사용했습니다.", "Scout used.", false),
            ["ui.town.status.retrain_active"] = ("플렉스 액티브를 재훈련했습니다.", "Flex active retrained.", false),
            ["ui.town.status.retrain_passive"] = ("플렉스 패시브를 재훈련했습니다.", "Flex passive retrained.", false),
            ["ui.town.status.retrain_full"] = ("전체 재훈련을 적용했습니다.", "Full retrain applied.", false),
            ["ui.town.status.dismiss_success"] = ("선택 영웅을 해산했습니다.", "Selected hero dismissed.", false),
            ["ui.town.status.refit_success"] = ("선택 아이템을 개조했습니다.", "Selected item refit applied.", false),
            ["ui.town.status.board_selected"] = ("패시브 보드를 변경했습니다.", "Passive board updated.", false),
            ["ui.town.status.node_toggled"] = ("패시브 노드 상태를 변경했습니다.", "Passive node toggled.", false),
            ["ui.town.status.perm_augment_equipped"] = ("영구 증강을 장착했습니다.", "Permanent augment equipped.", false),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.expedition.panel.map"] = ("지도 / 위치", "Map / Position", false),
            ["ui.expedition.panel.routes"] = ("경로", "Routes", false),
            ["ui.expedition.panel.selected_route"] = ("선택 경로", "Selected Route", false),
            ["ui.expedition.panel.squad"] = ("스쿼드 / 배치", "Squad / Deploy", false),
            ["ui.expedition.group.primary"] = ("주요 진행", "Primary", false),
            ["ui.expedition.group.warning"] = ("경고", "Warning", false),
            ["ui.expedition.help.body"] = ("노드를 고른 뒤 오른쪽 요약에서 보상과 위험을 확인하고 전투 또는 정산으로 진행하세요.", "Choose a node, then read the selected route summary before entering battle or resolving settlement.", false),
            ["ui.expedition.action.resolve_settlement"] = ("정산 진행", "Resolve Settlement", false),
            ["ui.expedition.node.type"] = ("유형: {0}", "Type: {0}", true),
            ["ui.expedition.node.reward"] = ("예정 보상: {0}", "Planned Reward: {0}", true),
            ["ui.expedition.node.effect"] = ("노드 효과: {0}", "Node Effect: {0}", true),
            ["ui.expedition.position.chapter"] = ("챕터: {0}", "Chapter: {0}", true),
            ["ui.expedition.position.site"] = ("지점: {0}", "Site: {0}", true),
            ["ui.expedition.position.current"] = ("현재: {0}", "Current: {0}", true),
            ["ui.expedition.position.selected"] = ("선택: {0}", "Selected: {0}", true),
            ["ui.expedition.position.type"] = ("유형: {0}", "Type: {0}", true),
            ["ui.expedition.position.node_type.battle"] = ("전투", "Battle", false),
            ["ui.expedition.position.node_type.settlement"] = ("정산", "Settlement", false),
            ["ui.expedition.reward.type"] = ("유형: {0}", "Type: {0}", true),
            ["ui.expedition.reward.return_town"] = ("Town 복귀: 현재 원정을 포기하고 선택한 경로 약속을 잃습니다.", "Return to Town: abandon this expedition and lose the current route commitment.", false),
            ["ui.expedition.tooltip.next_action_locked"] = ("먼저 경로를 선택하세요. 요약 패널에서 보상과 위험을 설명합니다.", "Choose a route first. The summary panel explains the reward and risk.", false),
            ["ui.expedition.tooltip.enter_battle"] = ("현재 배치와 태세로 선택한 전투 노드에 진입합니다.", "Enter the selected battle node with the current deploy and posture.", false),
            ["ui.expedition.tooltip.resolve_settlement"] = ("전투 없이 정산 노드를 처리하고 Reward로 이동합니다.", "Resolve this settlement node and move to Reward without a battle.", false),
            ["ui.expedition.tooltip.return_town"] = ("현재 원정을 포기하고 Town으로 돌아갑니다.", "Abandon the current expedition and return to Town.", false),
            ["ui.expedition.tooltip.route_card"] = ("{0}. 예정 보상: {1}. 노드 효과: {2}", "{0}. Planned reward: {1}. Node effect: {2}", true),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.battle.panel.summary"] = ("요약", "Summary", false),
            ["ui.battle.panel.allies"] = ("아군", "Allies", false),
            ["ui.battle.panel.enemies"] = ("적군", "Enemies", false),
            ["ui.battle.panel.log"] = ("로그", "Log", false),
            ["ui.battle.group.playback"] = ("재생", "Playback", false),
            ["ui.battle.group.primary"] = ("주요 행동", "Primary Action", false),
            ["ui.battle.group.smoke"] = ("디버그 / 스모크", "Debug / Smoke", false),
            ["ui.battle.group.sandbox"] = ("Combat Sandbox", "Combat Sandbox", false),
            ["ui.battle.group.utility"] = ("유틸리티", "Utility", false),
            ["ui.battle.help.body"] = ("요약, 최근 로그, 선택 유닛 패널로 현재 전투를 읽고 종료 후 Continue를 누르세요.", "Read the battle through the summary, recent log, and selected unit panel. Continue unlocks after the battle resolves.", false),
            ["ui.battle.help.body_sandbox"] = ("요약, 최근 로그, 선택 유닛 패널로 현재 전투를 읽으세요. Combat Sandbox는 Reward로 가지 않고, 같은 seed 재생, 새 seed, sandbox 종료만 제공합니다.", "Read the battle through the summary, recent log, and selected unit panel. Combat Sandbox stays inside battle: replay the same seed, roll a new seed, or exit the sandbox.", false),
            ["ui.battle.action.speed_1"] = ("x1", "x1", false),
            ["ui.battle.action.speed_2"] = ("x2", "x2", false),
            ["ui.battle.action.speed_4"] = ("x4", "x4", false),
            ["ui.battle.action.replay"] = ("다시 재생", "Replay", false),
            ["ui.battle.action.replay_same_seed"] = ("같은 seed 다시 재생", "Replay Same Seed", false),
            ["ui.battle.action.rebattle"] = ("재전투 (디버그)", "Rebattle (Debug)", false),
            ["ui.battle.action.new_seed"] = ("새 seed", "New Seed", false),
            ["ui.battle.action.return_town_debug"] = ("Town 복귀 (디버그)", "Return to Town (Debug)", false),
            ["ui.battle.action.exit_sandbox"] = ("Sandbox 종료", "Exit Sandbox", false),
            ["ui.battle.playback.ingame"] = ("Authored 원정 전투", "Authored Expedition Battle", false),
            ["ui.battle.playback.quick"] = ("Quick Battle (Smoke) | 배속 x{0:0}", "Quick Battle (Smoke) | Speed x{0:0}", true),
            ["ui.battle.playback.quick_paused"] = ("Quick Battle (Smoke) | 배속 x{0:0} | 일시정지", "Quick Battle (Smoke) | Speed x{0:0} | Paused", true),
            ["ui.battle.playback.direct"] = ("Combat Sandbox | 배속 x{0:0}", "Combat Sandbox | Speed x{0:0}", true),
            ["ui.battle.playback.direct_paused"] = ("Combat Sandbox | 배속 x{0:0} | 일시정지", "Combat Sandbox | Speed x{0:0} | Paused", true),
            ["ui.battle.pressure.allies"] = ("아군 우세", "Allies pressing", false),
            ["ui.battle.pressure.enemies"] = ("적군 우세", "Enemies pressing", false),
            ["ui.battle.pressure.even"] = ("압박 균형", "Pressure even", false),
            ["ui.battle.status.finished_summary"] = ("결과 | {0} | {1}{2}", "Result | {0} | {1}{2}", true),
            ["ui.battle.status.step_focus"] = ("스텝 {0:000} | {1} {2} -> {3} | {4}{5}", "Step {0:000} | {1} {2} -> {3} | {4}{5}", true),
            ["ui.battle.status.step_only"] = ("스텝 {0:000} | {1}{2}", "Step {0:000} | {1}{2}", true),
            ["ui.battle.result.summary"] = ("{0} | {1:000} 스텝 | 이벤트 {2}개", "{0} | {1:000} steps | {2} events", true),
            ["ui.battle.error.continue_before_finish"] = ("전투가 완전히 끝난 뒤 Continue가 활성화됩니다.", "Continue activates after the battle is fully resolved.", false),
            ["ui.battle.error.rebattle_smoke_only"] = ("Rebattle은 Quick Battle (Smoke)에서만 사용할 수 있습니다.", "Re-battle is only available in Quick Battle (Smoke).", false),
            ["ui.battle.error.return_town_smoke_only"] = ("직접 Town 복귀는 Quick Battle (Smoke)에서만 사용할 수 있습니다.", "Direct return to Town is only available in Quick Battle (Smoke).", false),
            ["ui.battle.error.return_town_before_finish"] = ("직접 Town으로 돌아가려면 전투를 먼저 끝내야 합니다.", "Finish the battle before returning directly to Town.", false),
            ["ui.battle.error.direct_sandbox_reward_hidden"] = ("Combat Sandbox는 Reward로 이어지지 않습니다. Exit Sandbox 또는 replay control을 사용하세요.", "Combat Sandbox does not continue into Reward. Use Exit Sandbox or replay controls instead.", false),
            ["ui.battle.settings.display"] = ("표시", "Display", false),
            ["ui.battle.settings.debug"] = ("디버그", "Debug", false),
            ["ui.battle.tooltip.playback"] = ("Quick Battle (Smoke)에서 일시정지, 다시 재생, 배속 조절을 사용할 수 있습니다.", "Quick Battle (Smoke) lets you pause, replay, and change playback speed.", false),
            ["ui.battle.tooltip.playback_direct"] = ("Combat Sandbox에서 일시정지, 같은 seed 다시 재생, 새 seed를 사용할 수 있습니다.", "Combat Sandbox lets you pause, replay the same seed, and roll a new seed.", false),
            ["ui.battle.tooltip.continue_ready"] = ("확정된 전투 결과를 가지고 Reward로 진행합니다.", "Proceed to Reward with the resolved battle result.", false),
            ["ui.battle.tooltip.continue_locked"] = ("전투가 완전히 끝난 뒤 Continue가 활성화됩니다.", "Continue activates after the battle is fully resolved.", false),
            ["ui.battle.tooltip.replay"] = ("현재 Quick Battle (Smoke) timeline을 처음부터 다시 재생합니다.", "Replay the current Quick Battle (Smoke) timeline from the start.", false),
            ["ui.battle.tooltip.replay_same_seed"] = ("현재 Combat Sandbox 전투를 같은 deterministic seed로 다시 재생합니다.", "Replay the active Combat Sandbox battle with the same deterministic seed.", false),
            ["ui.battle.tooltip.rebattle"] = ("새 seed로 Quick Battle (Smoke)를 다시 시작합니다.", "Restart Quick Battle (Smoke) with a fresh seed.", false),
            ["ui.battle.tooltip.new_seed"] = ("active preset을 유지한 채 Combat Sandbox를 다음 seed로 다시 시작합니다.", "Restart Combat Sandbox with the next seed while keeping the active preset.", false),
            ["ui.battle.tooltip.return_town_direct"] = ("전투가 끝난 뒤 Quick Battle (Smoke)를 정리하고 즉시 Town으로 돌아갑니다.", "Leave Quick Battle (Smoke) after the battle is resolved and go directly back to Town.", false),
            ["ui.battle.tooltip.exit_sandbox"] = ("전투가 끝난 뒤 Combat Sandbox를 종료합니다.", "Leave Combat Sandbox after the battle is resolved.", false),
            ["ui.battle.tooltip.settings"] = ("표시 설정을 엽니다. 디버그 설정은 Quick Battle (Smoke)에서만 보입니다.", "Open display settings. Debug settings appear only in Quick Battle (Smoke).", false),
            ["ui.battle.tooltip.settings_sandbox"] = ("표시 설정을 엽니다. sandbox diagnostics는 Combat Sandbox에서만 보입니다.", "Open display settings. Sandbox diagnostics appear only in Combat Sandbox.", false),
            ["ui.battle.tooltip.overhead_ui"] = ("전장 유닛 위에 HP와 상태를 표시합니다.", "Show HP and state over units in the battle field.", false),
            ["ui.battle.tooltip.damage_text"] = ("전투 중 피해와 회복 수치를 표시합니다.", "Show floating damage and heal numbers during battle.", false),
            ["ui.battle.tooltip.team_summary"] = ("양쪽 팀 요약 패널 표시를 토글합니다.", "Show ally and enemy team summaries in the side panels.", false),
            ["ui.battle.tooltip.debug_overlay"] = ("타게팅 선과 전투 진단 정보를 표시합니다.", "Show targeting lines and battle diagnostics for Quick Battle (Smoke).", false),
            ["ui.battle.tooltip.debug_overlay_sandbox"] = ("타게팅 선과 sandbox diagnostics를 표시합니다.", "Show targeting lines and sandbox diagnostics for Combat Sandbox.", false),
            ["ui.battle.log.basic_attack"] = ("[{0}] {1}이(가) {2}에게 기본 공격 -{3:0}", "[{0}] {1} hit {2} -{3:0}", true),
            ["ui.battle.log.heal"] = ("[{0}] {1}이(가) {2}을(를) 회복 +{3:0}", "[{0}] {1} heal {2} +{3:0}", true),
            ["ui.battle.log.skill"] = ("[{0}] {1} 스킬 {2} -{3:0}", "[{0}] {1} skill {2} -{3:0}", true),
            ["ui.battle.log.guard"] = ("[{0}] {1} 방어", "[{0}] {1} guard", true),
            ["ui.battle.log.down"] = ("[{0}] {1} 다운", "[{0}] {1} down", true),
            ["ui.battle.log.generic"] = ("[{0}] {1} {2}", "[{0}] {1} {2}", true),
            ["ui.battle.axis.state"] = ("상태", "State", false),
            ["ui.battle.axis.target"] = ("대상", "Target", false),
            ["ui.battle.axis.range"] = ("선호 사거리", "Preferred Range", false),
            ["ui.battle.axis.slot"] = ("교전 슬롯", "Engage Slot", false),
            ["ui.battle.axis.targeting"] = ("타게팅", "Targeting", false),
            ["ui.battle.axis.retarget_lock"] = ("재타게팅 잠금", "Retarget Lock", false),
            ["ui.battle.axis.guard_radius"] = ("가드 반경", "Guard Radius", false),
            ["ui.battle.axis.cluster_radius"] = ("클러스터 반경", "Cluster Radius", false),
            ["ui.battle.target.none"] = ("현재 대상 없음", "No current target", false),
            ["ui.battle.range.contact"] = ("근접", "Contact", false),
            ["ui.battle.targeting.current"] = ("현재 대상", "Current target", false),
            ["ui.battle.targeting.keep_current"] = ("현재 대상 유지", "Keep current", false),
        });

        AddOrUpdateEntries(new Dictionary<string, (string ko, string en, bool smart)>(StringComparer.Ordinal)
        {
            ["ui.reward.panel.summary"] = ("요약", "Summary", false),
            ["ui.reward.panel.build_context"] = ("빌드 맥락", "Build Context", false),
            ["ui.reward.help.body"] = ("보상 한 장을 고르면 즉시 적용되고, Town으로 돌아가면 런이 재개되거나 종료됩니다.", "Pick one reward to apply it immediately, then return to Town to resume or close the run.", false),
            ["ui.reward.action.open"] = ("보상 열기", "Open Reward", false),
            ["ui.reward.action.return_town_smoke"] = ("Town 복귀 / 스모크 종료", "Return to Town / Smoke Complete", false),
            ["ui.reward.action.return_town_failed"] = ("Town 복귀 / 런 실패", "Return to Town / Run Failed", false),
            ["ui.reward.action.return_town_complete"] = ("Town 복귀 / 런 완료", "Return to Town / Run Complete", false),
            ["ui.reward.action.return_town_resume"] = ("Town 복귀 / 다음에 재개", "Return to Town / Resume Later", false),
            ["ui.reward.result.quick_smoke"] = ("Quick Battle 스모크", "Quick Battle Smoke", false),
            ["ui.reward.result.run_complete"] = ("최종 철수", "Final Extract", false),
            ["ui.reward.status.default.return_town"] = ("보상이 확정되었습니다. Town으로 돌아가 다음 상태를 확인하세요.", "Reward locked in. Return to Town to continue.", false),
            ["ui.reward.status.default.quick"] = ("Quick Battle 스모크 정산입니다. 카드 한 장을 고르고 Town으로 돌아가세요.", "Quick Battle smoke settlement: pick one card and return to Town.", false),
            ["ui.reward.status.default.defeat"] = ("런이 실패했습니다. fallback 보상을 고르고 Town으로 돌아가세요.", "Run failed. Pick a fallback reward and return to Town.", false),
            ["ui.reward.status.default.complete"] = ("런이 완료되었습니다. 보상을 고르고 Town으로 돌아가세요.", "Run complete. Pick one reward and return to Town.", false),
            ["ui.reward.status.default.resume"] = ("보상을 고르고 Town으로 돌아간 뒤 원정을 다시 이어갈 수 있습니다.", "Pick one reward and return to Town. You can resume the expedition later.", false),
            ["ui.reward.summary.settlement_result"] = ("정산 결과: {0}", "Settlement: {0}", true),
            ["ui.reward.summary.base_reward"] = ("기본 보상: {0}", "Base Reward: {0}", true),
            ["ui.reward.summary.auto_loot"] = ("자동 전리품: {0}", "Auto Loot: {0}", true),
            ["ui.reward.summary.choice_applied"] = ("선택한 보상: {0}", "Chosen Reward: {0}", true),
            ["ui.reward.summary.permanent_unlock"] = ("대기 중인 영구 해금 후보: {0}", "Permanent Candidate Pending: {0}", true),
            ["ui.reward.summary.wallet"] = ("현재 자원: {0} Gold / {1} Echo", "Wallet Now: {0} Gold / {1} Echo", true),
            ["ui.reward.summary.inventory_now"] = ("현재 인벤토리: {0}개", "Inventory Now: {0} items", true),
            ["ui.reward.summary.continuation"] = ("이어지는 상태: {0}", "Continuation: {0}", true),
            ["ui.reward.summary.awaiting_choice"] = ("보상 한 장을 고른 뒤 돌아가세요.", "Choose one reward before returning.", false),
            ["ui.reward.summary.none"] = ("정산 요약을 불러올 수 없습니다.", "Settlement summary is unavailable.", false),
            ["ui.reward.summary.route_only"] = ("경로: {0} / {1}", "Route: {0} / {1}", true),
            ["ui.reward.build.posture"] = ("태세: {0}", "Posture: {0}", true),
            ["ui.reward.build.equipped_permanent"] = ("장착 영구 증강: {0}", "Equipped Permanent: {0}", true),
            ["ui.reward.build.bench"] = ("벤치 후보: {0}", "Bench Candidates: {0}", true),
            ["ui.reward.build.temp_augments"] = ("현재 임시 증강: {0}", "Current Temp Augments: {0}", true),
            ["ui.reward.build.thesis"] = ("빌드 테마: {0}", "Build Thesis: {0}", true),
            ["ui.reward.build.no_permanent"] = ("영구 테마 없음", "No permanent thesis", false),
            ["ui.reward.build.no_temp"] = ("임시 증강 없음", "No temporary augments", false),
            ["ui.reward.build.temp_count"] = ("임시 증강 {0}개", "{0} temp augment(s)", true),
            ["ui.reward.build.thesis.hex"] = ("Hex 집중", "Hex focus", false),
            ["ui.reward.build.thesis.hunt"] = ("Hunt 집중", "Hunt focus", false),
            ["ui.reward.build.thesis.tempo"] = ("Tempo 집중", "Tempo focus", false),
            ["ui.reward.build.thesis.ward"] = ("Ward 집중", "Ward focus", false),
            ["ui.reward.choice.build_impact"] = ("빌드 영향: {0}", "Build Impact: {0}", true),
            ["ui.reward.build_impact.gold"] = ("경제 축: 영입과 갱신 여유를 늘립니다.", "Economy rail: recruit and refresh.", false),
            ["ui.reward.build_impact.echo"] = ("경제 축: 정찰, 재훈련, 개조 복구에 씁니다.", "Economy rail: scout, retrain, refit, and recovery.", false),
            ["ui.reward.build_impact.permanent_slot"] = ("현재 일반 경로에서는 영구 슬롯 보상을 생성하지 않습니다.", "Normal lane does not generate permanent slot rewards.", false),
            ["ui.reward.build_impact.item.weapon"] = ("영웅 훅: 공격적이거나 규칙을 바꾸는 무기 라인입니다.", "Hero hook: offensive or rule-changing weapon line.", false),
            ["ui.reward.build_impact.item.armor"] = ("영웅 훅: 전열 내구나 보호 라인입니다.", "Hero hook: frontline durability or protection line.", false),
            ["ui.reward.build_impact.item.accessory"] = ("영웅 훅: 유틸리티나 유지력 액세서리 라인입니다.", "Hero hook: utility or sustain accessory line.", false),
            ["ui.reward.build_impact.item.default"] = ("영웅 훅: 인벤토리에 바로 들어가는 영구 아이템입니다.", "Hero hook: inventory-ready permanent item.", false),
            ["ui.reward.build_impact.temp_fixed"] = ("현재 빌드에 즉시 얹히는 고정 임시 증강입니다.", "Immediate fixed temporary augment for the current build.", false),
            ["ui.reward.build_impact.temp_unlock"] = ("임시 증강이 영구 해금 미리보기와 연결됩니다.", "Temporary augment that previews a permanent unlock path.", false),
            ["ui.reward.build_impact.augment.default"] = ("빌드 훅: 임시 증강 압박 라인입니다.", "Build hook: temporary augment pressure line.", false),
            ["ui.reward.build_impact.augment.hex"] = ("빌드 훅: hex 압박 라인입니다.", "Build hook: hex pressure line.", false),
            ["ui.reward.build_impact.augment.hunt"] = ("빌드 훅: hunt 마무리 라인입니다.", "Build hook: hunt finish line.", false),
            ["ui.reward.build_impact.augment.tempo"] = ("빌드 훅: tempo 가속 라인입니다.", "Build hook: tempo acceleration line.", false),
            ["ui.reward.build_impact.augment.ward"] = ("빌드 훅: ward 안정화 라인입니다.", "Build hook: ward stability line.", false),
            ["ui.reward.build_impact.augment.permanent"] = ("빌드 훅: 영구 해금 후보 라인입니다.", "Build hook: permanent unlock candidate.", false),
            ["ui.reward.continuation.smoke"] = ("Quick Battle smoke lane 종료", "Quick Battle smoke lane closes", false),
            ["ui.reward.continuation.complete"] = ("런 종료", "run closes", false),
            ["ui.reward.continuation.resume"] = ("Town으로 돌아간 뒤 원정을 다시 재개할 수 있습니다.", "Return to Town, then resume the expedition", false),
            ["ui.reward.tooltip.choice"] = ("{0}. {1}", "{0}. {1}", true),
            ["ui.reward.tooltip.return_locked"] = ("먼저 보상 한 장을 고르세요. 적용 결과는 요약에 남아 있습니다.", "Choose one reward first. The summary will keep the applied delta on screen.", false),
            ["ui.reward.tooltip.return_ready"] = ("적용된 보상과 이어지는 상태를 가지고 Town으로 돌아갑니다.", "Return to Town with the applied reward result and continuation state.", false),
        });
    }

    private static void AddOrUpdateEntries(IEnumerable<KeyValuePair<string, (string ko, string en, bool smart)>> entries)
    {
        foreach (var entry in entries)
        {
            SharedEntries[entry.Key] = entry.Value;
        }
    }

    public static Font GetSharedUiFont()
    {
        return AssetDatabase.LoadAssetAtPath<Font>(SharedFontSourcePath)
               ?? AssetDatabase.LoadAssetAtPath<GameFontCatalog>(SharedFontCatalogPath)?.SharedUiFont
               ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/_Game");
        EnsureFolder("Assets/Resources/_Game/Fonts");
        EnsureFolder(LocalizationRoot);
        EnsureFolder(LocaleRoot);
        EnsureFolder(StringTableRoot);
        EnsureFolder(AssetTableRoot);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static LocalizationSettings EnsureLocalizationSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "Localization Settings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        return settings;
    }

    private static IReadOnlyDictionary<string, Locale> EnsureLocales()
    {
        var locales = new Dictionary<string, Locale>(StringComparer.Ordinal)
        {
            ["ko"] = EnsureLocaleAsset("ko", "Korean", 0),
            ["en"] = EnsureLocaleAsset("en", "English", 1),
        };

        foreach (var locale in locales.Values)
        {
            LocalizationEditorSettings.AddLocale(locale);
        }

        var pseudo = AssetDatabase.LoadAssetAtPath<PseudoLocale>($"{LocaleRoot}/pseudo.asset");
        if (pseudo == null)
        {
            pseudo = PseudoLocale.CreatePseudoLocale();
            pseudo.Identifier = new LocaleIdentifier("qps-ploc");
            pseudo.LocaleName = "Pseudo";
            AssetDatabase.CreateAsset(pseudo, $"{LocaleRoot}/pseudo.asset");
        }

        LocalizationEditorSettings.AddLocale(pseudo);
        return locales;
    }

    private static Locale EnsureLocaleAsset(string code, string localeName, ushort sortOrder)
    {
        var path = $"{LocaleRoot}/{code}.asset";
        var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
        if (locale == null)
        {
            locale = Locale.CreateLocale(new LocaleIdentifier(code));
            AssetDatabase.CreateAsset(locale, path);
        }

        locale.Identifier = new LocaleIdentifier(code);
        locale.LocaleName = localeName;
        locale.SortOrder = sortOrder;
        EditorUtility.SetDirty(locale);
        return locale;
    }

    private static void ConfigureStartupSelectors(LocalizationSettings settings)
    {
        var selectors = LocalizationSettings.StartupLocaleSelectors;
        selectors.Clear();
        selectors.Add(new PlayerPrefLocaleSelector());
        selectors.Add(new SystemLocaleSelector());
        selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("en") });
        EditorUtility.SetDirty(settings);
    }

    private static void EnsureStringTables(IReadOnlyDictionary<string, Locale> locales)
    {
        foreach (var tableName in RequiredStringTables)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName)
                             ?? LocalizationEditorSettings.CreateStringTableCollection(
                                 tableName,
                                 StringTableRoot,
                                 locales.Values.ToList());

            foreach (var locale in locales.Values)
            {
                var table = collection.GetTable(locale.Identifier) as StringTable
                            ?? collection.AddNewTable(locale.Identifier) as StringTable;
                if (table == null)
                {
                    continue;
                }

                SyncSharedEntries(tableName, locale.Identifier.Code, table);

                LocalizationEditorSettings.SetPreloadTableFlag(table, tableName is GameLocalizationTables.UICommon or GameLocalizationTables.UITown or GameLocalizationTables.UIExpedition or GameLocalizationTables.UIBattle or GameLocalizationTables.UIReward or GameLocalizationTables.CombatLog or GameLocalizationTables.SystemMessages);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
        }
    }

    private static void SyncSharedEntries(string tableName, string localeCode, StringTable table)
    {
        foreach (var (key, value) in SharedEntries)
        {
            if (!BelongsToTable(tableName, key))
            {
                continue;
            }

            var entry = table.GetEntry(key) ?? table.AddEntry(key, localeCode == "ko" ? value.ko : value.en);
            entry.Value = localeCode == "ko" ? value.ko : value.en;
            entry.IsSmart = value.smart;
            EditorUtility.SetDirty(entry.Table);
        }
    }

    private static bool BelongsToTable(string tableName, string key)
    {
        return tableName switch
        {
            GameLocalizationTables.UICommon => key.StartsWith("ui.common.", StringComparison.Ordinal),
            GameLocalizationTables.UITown => key.StartsWith("ui.town.", StringComparison.Ordinal),
            GameLocalizationTables.UIExpedition => key.StartsWith("ui.expedition.", StringComparison.Ordinal),
            GameLocalizationTables.UIBattle => key.StartsWith("ui.battle.", StringComparison.Ordinal),
            GameLocalizationTables.UIReward => key.StartsWith("ui.reward.", StringComparison.Ordinal),
            GameLocalizationTables.CombatLog => key.StartsWith("combat.log.", StringComparison.Ordinal),
            GameLocalizationTables.SystemMessages => key.StartsWith("system.", StringComparison.Ordinal),
            _ => false
        };
    }

    private static void EnsureSharedFontCatalog()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SharedFontSourcePath);
        if (font == null)
        {
            Debug.LogWarning($"Shared UI font asset not found. Expected path: {SharedFontSourcePath}");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<GameFontCatalog>(SharedFontCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<GameFontCatalog>();
            AssetDatabase.CreateAsset(catalog, SharedFontCatalogPath);
        }

        var serializedObject = new SerializedObject(catalog);
        serializedObject.FindProperty("sharedUiFont").objectReferenceValue = font;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }
}
