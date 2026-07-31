using UnityEngine;

namespace SM.Unity.Narrative;

public readonly record struct DialogueSceneViewState(
    string SpeakerNameText,
    string LineText,
    bool IsNarrator,
    StorySpeakerSide ActiveSpeakerSide,
    Sprite? LeftPortrait,
    bool ShowLeftPortrait,
    Sprite? RightPortrait,
    bool ShowRightPortrait,
    bool ShowSkipAll,
    bool ShowSkipConfirmation,
    string SkipConfirmTitleText,
    string SkipConfirmBodyText,
    // 이 셋은 UXML 에 "Skip Scene" / "Skip" / "Cancel" 로 <b>박혀</b> 있었다.
    // 확인창 제목·본문만 런타임에서 번역되고 정작 누르는 버튼 셋은 영문이 그대로 떴다.
    // 스토리는 한국어 1차이고, 신규 플레이어가 가장 먼저 보는 화면이 이 컷씬이다.
    // "Tap to continue" 역시 UXML 에 박혀 있었고 런타임에서 <b>한 번도 설정되지 않았다</b> —
    // 보이기/숨기기만 제어했다. 한 줄이 끝날 때마다 뜨는 문구라 노출 빈도가 가장 높다.
    string ContinueHintText,
    string SkipButtonText,
    string SkipConfirmAcceptText,
    string SkipConfirmCancelText,
    bool IsTyping,
    bool ShowContinueHint);
