using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>HUB 초안 fantasy만 보존한다. 시스템 motif와 recipe는 파생기에 남긴다.</summary>
public static class OwnerConceptAnchorCatalog
{
    public static IReadOnlyList<OwnerConceptAnchor> CreateRatificationPendingDraft()
        => new[]
        {
            Anchor("anchor_reaper_legion", "사신 군단", "죽음이 눈덩이처럼 구른다 — 킬 하나가 다음 킬을 부르는 언데드 물결"),
            Anchor("anchor_wither_spiral", "쇠약의 탑", "적을 천천히, 확실하게 무너뜨린다 — 저주가 쌓일수록 이길 수밖에 없는 판"),
            Anchor("anchor_iron_line", "철벽 전열", "아무도 죽지 않는 팀 — 방벽 뒤에서 적이 먼저 지친다"),
            Anchor("anchor_spearpoint", "암살 창끝", "적의 심장(후열 딜러)을 먼저 뽑는다 — 표식 찍고 파고들기"),
            Anchor("anchor_arrow_storm", "화살 폭풍", "거리를 지배한다 — 닿기 전에 끝내는 원거리 일제사격"),
            Anchor("anchor_decisive_blow", "결정타", "흔들리는 적을 끊어낸다 — 처형 라인으로 전투를 접는 팀"),
            Anchor("anchor_snare_net", "그물", "움직이지 못하는 적은 적이 아니다 — 속박·침묵·기절의 연쇄 제압"),
            Anchor("anchor_undying_light", "불멸의 빛", "쓰러지기 직전에 반드시 일으켜 세운다 — 구원의 순간을 반복하는 성직 수호"),
            Anchor("anchor_bait_and_trap", "미끼와 덫", "적을 유인해 진형으로 이긴다 — 차단과 구출이 승부수인 판짜기"),
            Anchor("anchor_all_in_carry", "일점 돌파", "한 명에게 전부 건다 — 아이템·노드를 몰빵한 캐리가 하드캐리"),
        };

    private static OwnerConceptAnchor Anchor(string id, string displayName, string fantasy)
        => new(id, displayName, fantasy, RatificationPending: true);
}
