using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance;

public sealed class P09AppearanceStudioWindow : EditorWindow
{
    private const string VisualPrefabPath = "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";
    private const string PreviewRootName = "__SM_P09AppearancePreview";

    private readonly List<Material> _previewMaterials = new();
    private Vector2 _characterScroll;
    private Vector2 _detailScroll;
    private IReadOnlyList<CharacterDefinition> _characters = new List<CharacterDefinition>();
    private BattleP09AppearanceCatalog _catalog = null!;
    private BattleP09AppearancePreset? _selectedPreset;
    private CharacterDefinition? _selectedCharacter;
    private GameObject? _previewRoot;

    [MenuItem("SM/캐릭터/P09 외형 편집")]
    public static void Open()
    {
        OpenWindow();
    }

    [MenuItem("SM/Characters/P09 Appearance Studio")]
    private static void OpenLegacy()
    {
        OpenWindow();
    }

    private static void OpenWindow()
    {
        GetWindow<P09AppearanceStudioWindow>("P09 외형 편집");
    }

    private void OnEnable()
    {
        RefreshData(ensurePresets: false);
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    private void OnGUI()
    {
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCharacterList();
            DrawDetailPanel();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                RefreshData(ensurePresets: false);
            }

            if (GUILayout.Button("P09 카탈로그 재생성", EditorStyles.toolbarButton, GUILayout.Width(142f)))
            {
                RefreshData(ensurePresets: true);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_selectedPreset == null))
            {
                if (GUILayout.Button("미리보기 갱신", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                {
                    UpdatePreview();
                }

                if (GUILayout.Button("미리보기 지우기", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                {
                    DestroyPreview();
                }
            }
        }
    }

    private void DrawCharacterList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(260f)))
        {
            EditorGUILayout.LabelField("캐릭터", EditorStyles.boldLabel);
            _characterScroll = EditorGUILayout.BeginScrollView(_characterScroll);
            foreach (var character in _characters)
            {
                var selected = _selectedCharacter == character;
                var label = string.IsNullOrWhiteSpace(character.LegacyDisplayName)
                    ? character.Id
                    : $"{character.LegacyDisplayName} [{character.Id}]";
                var style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Button(label, style))
                {
                    SelectCharacter(character);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawDetailPanel()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (_selectedCharacter == null || _selectedPreset == null)
            {
                EditorGUILayout.HelpBox("P09 외형 프리셋을 수정할 캐릭터를 선택하세요.", MessageType.Info);
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.LabelField(_selectedPreset.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_selectedPreset.CharacterId, EditorStyles.miniLabel, GUILayout.Height(18f));

            EditorGUILayout.Space(6f);
            DrawPartSection("신체", new[]
            {
                BattleP09AppearancePartType.Sex,
                BattleP09AppearancePartType.FaceType,
                BattleP09AppearancePartType.HairStyle,
                BattleP09AppearancePartType.HairColor,
                BattleP09AppearancePartType.Skin,
                BattleP09AppearancePartType.EyeColor,
                BattleP09AppearancePartType.FacialHair,
                BattleP09AppearancePartType.BustSize,
            });

            DrawPartSection("장비", new[]
            {
                BattleP09AppearancePartType.Head,
                BattleP09AppearancePartType.Chest,
                BattleP09AppearancePartType.Arm,
                BattleP09AppearancePartType.Waist,
                BattleP09AppearancePartType.Leg,
                BattleP09AppearancePartType.Weapon,
                BattleP09AppearancePartType.Shield,
            });

            DrawColorOverrides();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("프리셋 찾기", GUILayout.Width(110f)))
                {
                    EditorGUIUtility.PingObject(_selectedPreset);
                    Selection.activeObject = _selectedPreset;
                }

                if (GUILayout.Button("저장"))
                {
                    SaveSelectedPreset(updatePreview: true);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPartSection(string title, IReadOnlyList<BattleP09AppearancePartType> types)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (var type in types)
        {
            DrawPartPopup(type);
        }
    }

    private void DrawPartPopup(BattleP09AppearancePartType type)
    {
        if (_selectedPreset == null)
        {
            return;
        }

        var sexId = type == BattleP09AppearancePartType.Sex
            ? 0
            : _selectedPreset.SexId;
        var options = _catalog.GetOptions(type, sexId).ToList();
        if (options.Count == 0)
        {
            EditorGUILayout.LabelField(GetPartLabel(type), "P09 옵션 없음");
            return;
        }

        var currentId = _selectedPreset.GetContentId(type);
        var currentIndex = Mathf.Max(0, options.FindIndex(option => option.ContentId == currentId));
        var labels = options.Select(option => BuildOptionLabel(type, option)).ToArray();
        EditorGUI.BeginChangeCheck();
        var nextIndex = EditorGUILayout.Popup(GetPartLabel(type), currentIndex, labels);
        if (EditorGUI.EndChangeCheck())
        {
            _selectedPreset.SetContentId(type, options[nextIndex].ContentId);
            SaveSelectedPreset(updatePreview: true);
        }
    }

    private void DrawColorOverrides()
    {
        if (_selectedPreset == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("머티리얼 색상 조정", EditorStyles.boldLabel);
        var serialized = new SerializedObject(_selectedPreset);
        var property = serialized.FindProperty("materialColorOverrides");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent("색상 오버라이드"), includeChildren: true);
        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            SaveSelectedPreset(updatePreview: true);
        }
    }

    private void RefreshData(bool ensurePresets)
    {
        _catalog = BattleP09AppearanceCatalogBuilder.EnsureCatalog();
        if (ensurePresets)
        {
            BattleP09AppearanceCatalogBuilder.EnsureMissingPresets();
        }

        _characters = BattleP09AppearanceCatalogBuilder.LoadCharacters();
        if (_selectedCharacter != null)
        {
            SelectCharacter(_characters.FirstOrDefault(character => character.Id == _selectedCharacter.Id));
        }
        else if (_characters.Count > 0)
        {
            SelectCharacter(_characters[0]);
        }
    }

    private void SelectCharacter(CharacterDefinition? character)
    {
        _selectedCharacter = character;
        _selectedPreset = null;
        if (character == null)
        {
            return;
        }

        _selectedPreset = BattleP09AppearanceCatalogBuilder.FindPreset(character.Id);
        if (_selectedPreset == null)
        {
            var seedIndex = Mathf.Max(0, _characters.ToList().FindIndex(item => item.Id == character.Id));
            _selectedPreset = BattleP09AppearanceCatalogBuilder.EnsurePreset(character, _catalog, seedIndex);
        }

        Selection.activeObject = _selectedPreset;
        UpdatePreview();
    }

    private void SaveSelectedPreset(bool updatePreview)
    {
        if (_selectedPreset == null)
        {
            return;
        }

        ConfigureSelectedPresetIdentity();
        _selectedPreset.EnsureDefaultColorOverrides();
        EditorUtility.SetDirty(_selectedPreset);
        AssetDatabase.SaveAssets();
        if (updatePreview)
        {
            UpdatePreview();
        }
    }

    private void ConfigureSelectedPresetIdentity()
    {
        if (_selectedPreset == null || _selectedCharacter == null)
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(_selectedCharacter.LegacyDisplayName)
            ? _selectedCharacter.Id
            : _selectedCharacter.LegacyDisplayName;
        _selectedPreset.ConfigureIdentity(_selectedCharacter.Id, displayName, _catalog);
    }

    private void UpdatePreview()
    {
        if (_selectedPreset == null)
        {
            DestroyPreview();
            return;
        }

        CreatePreview();
        if (_previewRoot == null)
        {
            return;
        }

        _selectedPreset.ApplyTo(_previewRoot.transform, _previewMaterials);
        EditorUtility.SetDirty(_previewRoot);
        SceneView.RepaintAll();
    }

    private void CreatePreview()
    {
        DestroyPreview();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[P09AppearanceStudio] Missing P09 preview prefab: {VisualPrefabPath}");
            return;
        }

        _previewRoot = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (_previewRoot == null)
        {
            return;
        }

        _previewRoot.name = PreviewRootName;
        _previewRoot.hideFlags = HideFlags.DontSave;
        _previewRoot.transform.position = Vector3.zero;
        _previewRoot.transform.rotation = Quaternion.identity;
        _previewRoot.transform.localScale = Vector3.one;
        Selection.activeGameObject = _previewRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void DestroyPreview()
    {
        foreach (var material in _previewMaterials)
        {
            if (material != null)
            {
                DestroyImmediate(material);
            }
        }

        _previewMaterials.Clear();

        if (_previewRoot != null)
        {
            ClearPreviewSelection(_previewRoot);
            DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }

        var existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            ClearPreviewSelection(existing);
            DestroyImmediate(existing);
        }
    }

    private void ClearPreviewSelection(GameObject preview)
    {
        var activeTransform = Selection.activeTransform;
        if (activeTransform == null)
        {
            return;
        }

        if (activeTransform == preview.transform || activeTransform.IsChildOf(preview.transform))
        {
            Selection.activeObject = _selectedPreset;
        }
    }

    private static string GetPartLabel(BattleP09AppearancePartType type)
    {
        return type switch
        {
            BattleP09AppearancePartType.Sex => "성별",
            BattleP09AppearancePartType.FaceType => "얼굴 타입",
            BattleP09AppearancePartType.HairStyle => "헤어 스타일",
            BattleP09AppearancePartType.HairColor => "머리 색",
            BattleP09AppearancePartType.Skin => "피부 톤",
            BattleP09AppearancePartType.EyeColor => "눈 색",
            BattleP09AppearancePartType.FacialHair => "수염",
            BattleP09AppearancePartType.BustSize => "가슴 크기",
            BattleP09AppearancePartType.Head => "머리 장비",
            BattleP09AppearancePartType.Chest => "상의",
            BattleP09AppearancePartType.Arm => "팔 장비",
            BattleP09AppearancePartType.Waist => "허리 장비",
            BattleP09AppearancePartType.Leg => "하의",
            BattleP09AppearancePartType.Weapon => "무기",
            BattleP09AppearancePartType.Shield => "방패",
            _ => ObjectNames.NicifyVariableName(type.ToString())
        };
    }

    private static string BuildOptionLabel(BattleP09AppearancePartType type, BattleP09AppearanceOption option)
    {
        var displayName = TranslateOptionName(type, option.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = option.MeshName;
        }

        return string.IsNullOrWhiteSpace(displayName)
            ? $"#{option.ContentId}"
            : $"{option.ContentId}: {displayName}";
    }

    private static string TranslateOptionName(BattleP09AppearancePartType type, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        if (type == BattleP09AppearancePartType.Sex)
        {
            return displayName switch
            {
                "Male" => "남성",
                "Female" => "여성",
                _ => displayName
            };
        }

        if (type == BattleP09AppearancePartType.HairColor || type == BattleP09AppearancePartType.EyeColor)
        {
            return displayName switch
            {
                "Blue" => "파랑",
                "Brown" => "갈색",
                "Gold" => "금색",
                "Green" => "녹색",
                "Ivory" => "상아색",
                "Orange" => "주황",
                "Pink" => "분홍",
                "Purple" => "보라",
                "Red" => "빨강",
                _ => displayName
            };
        }

        if (type == BattleP09AppearancePartType.Skin)
        {
            return displayName switch
            {
                "Bright" => "밝은 피부",
                "Default" => "기본 피부",
                "Dark" => "어두운 피부",
                _ => displayName
            };
        }

        return displayName;
    }
}
