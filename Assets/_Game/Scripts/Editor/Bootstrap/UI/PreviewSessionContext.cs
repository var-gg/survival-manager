using System;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// Town Preview 9 surface 공용 — real <see cref="GameSessionRoot"/> + ContentTextResolver +
/// 표준 sprite path loader를 한 곳에 모은 dev-tool helper.
///
/// 각 Bootstrap은 BuildInto 진입에서 <see cref="EnsureSession"/>으로 root를 확보한 뒤
/// Presenter를 inject한다. real-session wire에 실패하면 (예: 첫 회차 profile 생성 실패 등)
/// caller가 catch해서 mock fallback으로 내려간다 — 이 helper는 throw만 한다.
///
/// audit §5 "본 audit이 닫지 않는 것" — runtime presenter 연결의 표준 진입점.
/// </summary>
internal static class PreviewSessionContext
{
    private const string ClassSpriteFmt    = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string AffixSpriteFmt    = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";
    private const string PostureSpriteFmt  = "Assets/_Game/UI/Foundation/Sprites/Posture/posture_{0}.png";
    private const string ThreatSpriteFmt   = "Assets/_Game/UI/Foundation/Sprites/Threat/threat_{0}.png";
    private const string AugmentSpriteFmt  = "Assets/_Game/UI/Foundation/Sprites/Augment/augment_{0}.png";
    private const string CurrencySpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_{0}.png";
    private const string PortraitPathFmt   = "Assets/Resources/_Game/Art/Characters/hero_{0}/portrait_full.png";

    /// <summary>
    /// GameSessionRoot Instance를 보장하고 (없으면 EnsureInstance) OfflineLocal session을 강제 시작한다.
    /// EnsureInstance는 <see cref="SessionRealmAutoStartPolicy.ShouldForceOfflineLocalForScene"/>이 false일 때
    /// (= Boot scene일 때) auto-start를 안 부르므로, Preview는 항상 명시 호출한다. 이미 active session이면 idempotent.
    /// </summary>
    public static GameSessionRoot EnsureSession()
    {
        var root = GameSessionRoot.EnsureInstance();
        if (!root.HasActiveSession)
        {
            root.EnsureOfflineLocalSession();
        }
        return root;
    }

    /// <summary>
    /// ContentTextResolver factory — Localization init 여부와 무관하게 fallback 텍스트로 동작한다
    /// (LocalizePlayerFacingContent가 entry 없으면 fallback 인자를 그대로 반환).
    /// </summary>
    public static ContentTextResolver CreateContentText(GameSessionRoot root)
        => new(root.Localization, root.CombatContentLookup);

    public static Texture2D? LoadClassSprite(string key)    => Load(ClassSpriteFmt, key);
    public static Texture2D? LoadAffixSprite(string key)    => Load(AffixSpriteFmt, key);
    public static Texture2D? LoadPostureSprite(string key)  => Load(PostureSpriteFmt, key);
    public static Texture2D? LoadThreatSprite(string key)   => Load(ThreatSpriteFmt, key);
    public static Texture2D? LoadAugmentSprite(string key)  => Load(AugmentSpriteFmt, key);
    public static Texture2D? LoadCurrencySprite(string key) => Load(CurrencySpriteFmt, key);
    public static Texture2D? LoadHeroPortrait(string heroId) => Load(PortraitPathFmt, heroId);

    private static Texture2D? Load(string fmt, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(fmt, key));
    }
}
