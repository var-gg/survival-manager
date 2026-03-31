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
        ["ui.common.locale.english"] = ("English", "English", false),
        ["ui.common.confirm"] = ("확인", "Confirm", false),
        ["ui.common.cancel"] = ("취소", "Cancel", false),
        ["ui.common.save"] = ("저장", "Save", false),
        ["ui.common.load"] = ("불러오기", "Load", false),
        ["ui.common.return_town"] = ("Town 복귀", "Return Town", false),
        ["ui.common.settings"] = ("설정", "Settings", false),
        ["ui.common.pause"] = ("일시정지", "Pause", false),
        ["ui.common.continue"] = ("계속", "Continue", false),
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
        ["ui.town.currency.summary"] = ("골드: {0} | 영구 슬롯: {1} | 특성 리롤: {2}", "Gold: {0} | Perm Slots: {1} | Trait Reroll: {2}", true),
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
        ["ui.town.deploy.resume"] = ("원정 재개: {0}", "Resume Expedition: {0}", true),
        ["ui.town.deploy.last_reward"] = ("직전 보상: {0}", "Last Reward: {0}", true),
        ["ui.town.deploy.last_effect"] = ("직전 노드 효과: {0}", "Last Node Effect: {0}", true),
        ["ui.town.deploy.route_needed"] = ("경로 선택 필요", "Route selection required", false),
        ["ui.town.status.default.resume"] = ("영입, 저장, 원정 재개 또는 Quick Battle을 진행하세요.", "Recruit, save, resume the expedition, or run a quick battle.", false),
        ["ui.town.status.default.start"] = ("영입, 저장, 원정 시작 또는 Quick Battle을 진행하세요.", "Recruit, save, start an expedition, or run a quick battle.", false),
        ["ui.town.status.profile_loaded"] = ("프로필을 다시 불러왔습니다.", "Profile reloaded.", false),
        ["ui.town.status.profile_saved"] = ("프로필을 저장했습니다.", "Profile saved.", false),
        ["ui.town.status.recruit_success"] = ("후보 {0}을 영입했습니다. (-{1} Gold)", "Recruited offer {0}. (-{1} Gold)", true),
        ["ui.town.status.reroll_success"] = ("영입 후보를 리롤했습니다. (-{0} Gold)", "Recruit offers rerolled. (-{0} Gold)", true),
        ["ui.town.status.team_posture"] = ("팀 태세: {0}", "Team posture: {0}", true),
        ["ui.town.status.anchor_cycled"] = ("{0} 배치를 갱신했습니다.", "{0} deployment updated.", true),
        ["ui.town.error.recruit_failed"] = ("영입에 실패했습니다.", "Failed to recruit the selected offer.", false),
        ["ui.town.error.reroll_failed"] = ("영입 후보 리롤에 실패했습니다.", "Failed to reroll recruit offers.", false),
        ["ui.town.action.recruit"] = ("영입", "Recruit", false),
        ["ui.town.action.reroll"] = ("리롤", "Reroll", false),
        ["ui.town.action.debug_start"] = ("원정 시작", "Start Expedition", false),
        ["ui.town.action.quick_battle"] = ("Quick Battle", "Quick Battle", false),
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
        ["ui.expedition.effect.gold"] = ("+{0} Gold", "+{0} Gold", true),
        ["ui.expedition.effect.reroll"] = ("특성 리롤 +{0}", "Trait Reroll +{0}", true),
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
        ["ui.expedition.route.relay.reward"] = ("특성 리롤 보상", "Trait Reroll Reward", false),
        ["ui.expedition.route.relay.desc"] = ("전투 후 특성 리롤 자원을 확보하는 경로입니다.", "Battle for extra trait reroll currency.", false),
        ["ui.expedition.route.shrine.label"] = ("제단 경로", "Shrine Route", false),
        ["ui.expedition.route.shrine.reward"] = ("임시 증강 확보", "Temporary Augment", false),
        ["ui.expedition.route.shrine.desc"] = ("강한 전투 후 임시 증강을 챙길 수 있습니다.", "Win a harder battle to claim a temporary augment.", false),
        ["ui.expedition.route.extract.label"] = ("철수 경로", "Extract Route", false),
        ["ui.expedition.route.extract.reward"] = ("귀환 보상", "Extraction Reward", false),
        ["ui.expedition.route.extract.desc"] = ("안전하게 귀환하며 마지막 보상을 챙깁니다.", "Safely return and claim the final reward.", false),
        ["ui.reward.title"] = ("보상 운영 화면", "Reward Operator UI", false),
        ["ui.reward.summary.battle_result"] = ("전투 결과: {0}", "Battle Result: {0}", true),
        ["ui.reward.summary.gold"] = ("골드: {0}", "Gold: {0}", true),
        ["ui.reward.summary.reroll"] = ("특성 리롤: {0}", "Trait Reroll: {0}", true),
        ["ui.reward.summary.slots"] = ("영구 슬롯: {0}", "Perm Slots: {0}", true),
        ["ui.reward.summary.inventory"] = ("인벤토리: {0}", "Inventory: {0}", true),
        ["ui.reward.summary.temp_augments"] = ("임시 증강: {0}", "Temp Augments: {0}", true),
        ["ui.reward.battle_summary.base"] = ("{0} / {1} steps / {2} events", "{0} / {1} steps / {2} events", true),
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
        ["ui.reward.kind.gold"] = ("Gold +{0}", "Gold +{0}", true),
        ["ui.reward.kind.item"] = ("아이템 / {0}", "Item / {0}", true),
        ["ui.reward.kind.temp_augment"] = ("임시 증강 / {0}", "Temp / {0}", true),
        ["ui.reward.kind.reroll"] = ("특성 리롤 +{0}", "Trait Reroll +{0}", true),
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
        ["ui.reward.choice.scout_intel.desc"] = ("특성 리롤 자원을 추가로 획득합니다.", "Gain extra trait reroll currency.", false),
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
        ["ui.reward.choice.doctrine_cache.desc"] = ("리롤 자원을 크게 확보합니다.", "Gain a larger reroll payout.", false),
        ["ui.reward.choice.fallback_stash.title"] = ("비상 보급함", "Fallback Stash", false),
        ["ui.reward.choice.fallback_stash.desc"] = ("패배 후 최소 보상을 챙깁니다.", "Collect a minimum fallback payout after defeat.", false),
        ["ui.reward.choice.tactical_notes.title"] = ("전술 메모", "Tactical Notes", false),
        ["ui.reward.choice.tactical_notes.desc"] = ("전술 수정용 리롤 자원을 획득합니다.", "Gain a reroll for tactical adjustment.", false),
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
        ["ui.battle.status.last_event"] = ("Step {0} | {1} -> {2} | {3} {4:0}{5}", "Step {0} | {1} -> {2} | {3} {4:0}{5}", true),
        ["ui.battle.status.windup"] = ("Step {0} | {1} 준비 {2}% -> {3}{4}", "Step {0} | {1} windup {2}% -> {3}{4}", true),
        ["ui.battle.status.posture"] = ("Step {0} | posture {1}{2}", "Step {0} | posture {1}{2}", true),
        ["ui.battle.error.no_allies"] = ("전투에 투입할 아군이 없습니다.", "No allied unit is ready for battle.", false),
        ["ui.battle.settings.title"] = ("전투 표시 옵션", "Battle View Settings", false),
        ["ui.battle.settings.closed"] = ("설정 패널 닫힘", "Settings panel closed", false),
        ["ui.battle.settings.state_changed"] = ("{0}: {1}", "{0}: {1}", true),
        ["ui.battle.settings.world_hp_label"] = ("캐릭터 HP", "Actor HP", false),
        ["ui.battle.settings.overlay_hp_label"] = ("오버레이 HP", "Overlay HP", false),
        ["ui.battle.settings.team_summary_label"] = ("팀 요약", "Team Summary", false),
        ["ui.battle.settings.world_hp"] = ("캐릭터 HP {0}", "Actor HP {0}", true),
        ["ui.battle.settings.overlay_hp"] = ("오버레이 HP {0}", "Overlay HP {0}", true),
        ["ui.battle.settings.team_summary"] = ("팀 요약 {0}", "Team Summary {0}", true),
        ["system.bootstrap.missing_sample_content.editor"] = ("샘플 콘텐츠 canonical root가 비어 있습니다. 먼저 SM/Bootstrap/Ensure Sample Content를 실행하고, 복구가 안 되면 SM/Seed/Generate Sample Content를 repair 용도로 실행한 뒤 다시 Play 하세요.", "Sample content canonical root is empty. Run SM/Bootstrap/Ensure Sample Content first, and use SM/Seed/Generate Sample Content only as a repair path before entering Play mode.", false),
        ["system.bootstrap.missing_sample_content.player"] = ("필수 샘플 콘텐츠 canonical root가 비어 있어 시작할 수 없습니다.", "Required sample content canonical root is empty, so boot cannot continue.", false),
        ["system.runtime.missing_root"] = ("GameSessionRoot가 없습니다.", "GameSessionRoot is missing.", false),
        ["combat.log.damage"] = ("S{0} {1}이(가) {2}에게 {3:0} 피해", "S{0} {1} dealt {3:0} damage to {2}", true),
        ["combat.log.heal"] = ("S{0} {1}이(가) {2}를 {3:0} 회복", "S{0} {1} healed {2} for {3:0}", true),
        ["combat.log.skill"] = ("S{0} {1}이(가) {2}에게 스킬 {3:0}", "S{0} {1} used a skill on {2} for {3:0}", true),
        ["combat.log.guard"] = ("S{0} {1}이(가) 방어 자세", "S{0} {1} took a guard stance", true),
        ["combat.log.generic"] = ("S{0} {1} {2}", "S{0} {1} {2}", true),
    };

    [MenuItem("SM/Bootstrap/Ensure Localization Foundation")]
    public static void EnsureFoundationMenu()
    {
        EnsureFoundationAssets();
    }

    public static void EnsureFoundationAssets()
    {
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

                if (tableName is GameLocalizationTables.UICommon or GameLocalizationTables.CombatLog or GameLocalizationTables.SystemMessages)
                {
                    SyncSharedEntries(tableName, locale.Identifier.Code, table);
                }

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
