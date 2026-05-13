using UnityEngine;

namespace SM.Unity.P09Appearance;

/// <summary>
/// 캐릭터 표현 baseline. Studio preview / Wiki capture / (향후) 캐릭터 도감·콜렉션 화면이
/// 모두 이 단일 자산을 읽어서 같은 라이팅·카메라·셰이더 처리로 캐릭터를 렌더링한다.
///
/// 원칙: "wiki에 올라가는 이미지 = 사용자가 인 게임에서 보는 캐릭터." 환경 차이(예: battle 스테이지)는
/// 이 baseline에서 명시적으로만 가산되어야 한다. 의도치 않은 drift는 art bug로 간주.
/// </summary>
[CreateAssetMenu(
    fileName = "CharacterShowcaseProfile",
    menuName = "SM/Character/Showcase Profile",
    order = 0)]
public sealed class CharacterShowcaseProfile : ScriptableObject
{
    [Header("카메라")]
    [Tooltip("Preview / Wiki capture 공통 카메라 FOV. 23~25 사이에서 캐릭터 비율이 자연스러움.")]
    [Range(10f, 60f)] public float fieldOfView = 23f;
    [Tooltip("near clip — preview에서 카메라가 캐릭터를 매우 가까이 보므로 매우 작아야 함.")]
    public float nearClip = 0.05f;
    public float farClip = 80f;
    [Tooltip("HDR 카메라 렌더링. PreviewRenderUtility에선 보통 false가 안정적.")]
    public bool allowHdr;
    [Tooltip("카메라 clear color. Wiki capture는 spec에서 per-character override 가능.")]
    public Color backgroundColor = new(0.68f, 0.79f, 0.88f, 1f);

    [Header("Ambient (Showcase studio)")]
    [Tooltip("PreviewRenderUtility의 단일 ambient color (Trilight 아님). 0.6 근방이 중성.")]
    public Color ambientColor = new(0.62f, 0.62f, 0.62f, 1f);

    [Header("Key Light (3-point rig — main)")]
    [Tooltip("X=피치 (위아래), Y=요 (좌우). 부드러운 위쪽 측면에서 들어옴.")]
    public Vector3 keyRotation = new(38f, -32f, 0f);
    [ColorUsage(false, false)] public Color keyColor = Color.white;
    [Range(0f, 3f)] public float keyIntensity = 1.35f;

    [Header("Fill Light (3-point rig — counter)")]
    [Tooltip("Key의 반대쪽에서 약하게 들어오는 보조광. 그림자 영역을 너무 어둡지 않게.")]
    public Vector3 fillRotation = new(330f, 142f, 0f);
    [ColorUsage(false, false)] public Color fillColor = Color.white;
    [Range(0f, 2f)] public float fillIntensity = 0.78f;

    [Header("셰이더 처리")]
    [Tooltip("Preview / capture에서 캐릭터 머티리얼을 어떻게 다룰지. " +
             "CompatibilityReadable이 권장: lilToon/Quibli는 PreviewRenderUtility에서 깨질 수 있어 " +
             "texture-preserving unlit으로 swap 후 렌더링. Source는 진단용 raw 셰이더.")]
    public ShaderTreatment shaderTreatment = ShaderTreatment.CompatibilityReadable;

    public enum ShaderTreatment
    {
        /// <summary>Source 셰이더 그대로 (lilToon/Quibli). PreviewRenderUtility에선 깨질 수 있음 — 진단 / 인 게임 일치 검증용.</summary>
        Source = 0,
        /// <summary>Texture-preserving unlit preview material로 swap. Editor preview / Wiki capture 신뢰성 baseline.</summary>
        CompatibilityReadable = 1,
    }
}
