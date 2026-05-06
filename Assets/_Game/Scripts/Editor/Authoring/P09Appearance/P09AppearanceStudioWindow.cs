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
    private const float PreviewPanelWidth = 500f;
    private const float PreviewPanelHeight = 620f;

    private readonly List<Material> _previewMaterials = new();
    private Vector2 _characterScroll;
    private Vector2 _detailScroll;
    private IReadOnlyList<CharacterDefinition> _characters = new List<CharacterDefinition>();
    private BattleP09AppearanceCatalog _catalog = null!;
    private BattleP09AppearancePreset? _selectedPreset;
    private CharacterDefinition? _selectedCharacter;
    private GameObject? _previewRoot;
    private PreviewRenderUtility? _previewRenderer;
    private Bounds _previewBounds;
    private PreviewFraming _previewFraming = PreviewFraming.FullBody;
    private float _previewYaw;

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
        var window = GetWindow<P09AppearanceStudioWindow>("P09 외형 편집");
        window.minSize = new Vector2(1180f, 720f);
    }

    private void OnEnable()
    {
        RefreshData(ensurePresets: false);
    }

    private void OnDisable()
    {
        DestroyPreview();
        _previewRenderer?.Cleanup();
        _previewRenderer = null;
    }

    private void OnGUI()
    {
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCharacterList();
            DrawDetailPanel();
            DrawPreviewPanel();
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

                if (GUILayout.Button("앞", EditorStyles.toolbarButton, GUILayout.Width(36f)))
                {
                    _previewYaw = 0f;
                    Repaint();
                }

                if (GUILayout.Button("좌", EditorStyles.toolbarButton, GUILayout.Width(36f)))
                {
                    _previewYaw = 90f;
                    Repaint();
                }

                if (GUILayout.Button("우", EditorStyles.toolbarButton, GUILayout.Width(36f)))
                {
                    _previewYaw = -90f;
                    Repaint();
                }

                if (GUILayout.Button("뒤", EditorStyles.toolbarButton, GUILayout.Width(36f)))
                {
                    _previewYaw = 180f;
                    Repaint();
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

    private void DrawPreviewPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(PreviewPanelWidth)))
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawPreviewFramingButton("전체", PreviewFraming.FullBody);
                DrawPreviewFramingButton("상반신", PreviewFraming.UpperBody);
                DrawPreviewFramingButton("얼굴", PreviewFraming.Face);
            }

            var rect = GUILayoutUtility.GetRect(
                PreviewPanelWidth,
                PreviewPanelHeight,
                GUILayout.ExpandWidth(false),
                GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            if (_previewRoot == null)
            {
                EditorGUI.LabelField(rect, "미리보기를 갱신하세요.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            DrawRenderedPreview(rect);
        }
    }

    private void DrawPreviewFramingButton(string label, PreviewFraming framing)
    {
        var selected = _previewFraming == framing;
        using (new EditorGUI.DisabledScope(selected))
        {
            if (GUILayout.Button(label, EditorStyles.toolbarButton))
            {
                _previewFraming = framing;
                Repaint();
            }
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
        _catalog = ensurePresets
            ? BattleP09AppearanceCatalogBuilder.EnsureCatalog()
            : BattleP09AppearanceCatalogBuilder.LoadCatalog()
              ?? BattleP09AppearanceCatalogBuilder.EnsureCatalog();
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
        ApplyPreviewReadableMaterials(_previewRoot.transform, _previewMaterials);
        _previewBounds = CalculatePreviewBounds(_previewRoot.transform);
        SceneView.RepaintAll();
        Repaint();
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

        _previewRoot = Instantiate(prefab);
        if (_previewRoot == null)
        {
            return;
        }

        _previewRoot.name = PreviewRootName;
        ApplyHideFlags(_previewRoot.transform);
        _previewRoot.transform.position = Vector3.zero;
        _previewRoot.transform.rotation = Quaternion.identity;
        _previewRoot.transform.localScale = Vector3.one;
        EnsurePreviewRenderer();
        _previewRenderer?.AddSingleGO(_previewRoot);
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

    private void DrawRenderedPreview(Rect rect)
    {
        EnsurePreviewRenderer();
        if (_previewRenderer == null)
        {
            return;
        }

        _previewRenderer.BeginPreview(rect, GUIStyle.none);
        ConfigurePreviewCamera(rect);
        _previewRenderer.Render();
        var texture = _previewRenderer.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
    }

    private void EnsurePreviewRenderer()
    {
        if (_previewRenderer != null)
        {
            return;
        }

        _previewRenderer = new PreviewRenderUtility();
        _previewRenderer.cameraFieldOfView = 24f;
        _previewRenderer.camera.clearFlags = CameraClearFlags.Color;
        _previewRenderer.camera.backgroundColor = new Color(0.68f, 0.79f, 0.88f, 1f);
        _previewRenderer.camera.nearClipPlane = 0.05f;
        _previewRenderer.camera.farClipPlane = 80f;
        _previewRenderer.ambientColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        _previewRenderer.lights[0].intensity = 1.15f;
        _previewRenderer.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        _previewRenderer.lights[1].intensity = 0.65f;
        _previewRenderer.lights[1].transform.rotation = Quaternion.Euler(340f, 140f, 0f);
    }

    private static void ApplyPreviewReadableMaterials(Transform modelRoot, ICollection<Material> generatedMaterials)
    {
        var previewShader = Shader.Find("Unlit/Texture")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
        if (previewShader == null)
        {
            return;
        }

        var materialCache = new Dictionary<Material, Material>();
        foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(false))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.shader == null)
                {
                    continue;
                }

                if (!ShouldUsePreviewReadableMaterial(source))
                {
                    continue;
                }

                if (!materialCache.TryGetValue(source, out var previewMaterial))
                {
                    previewMaterial = CreatePreviewReadableMaterial(source, previewShader);
                    materialCache[source] = previewMaterial;
                    generatedMaterials.Add(previewMaterial);
                }

                materials[i] = previewMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool ShouldUsePreviewReadableMaterial(Material material)
    {
        return material.shader.name.Contains("lilToon", System.StringComparison.Ordinal)
               || material.shader.name.StartsWith("Hidden/", System.StringComparison.Ordinal);
    }

    private static Material CreatePreviewReadableMaterial(Material source, Shader previewShader)
    {
        var previewMaterial = new Material(previewShader)
        {
            name = $"{source.name}_P09Preview",
            hideFlags = HideFlags.DontSave,
            renderQueue = source.renderQueue
        };

        CopyTexture(source, previewMaterial, "_MainTex", "_MainTex");
        CopyTexture(source, previewMaterial, "_MainTex", "_BaseMap");
        CopyColor(source, previewMaterial, "_Color", "_Color");
        CopyColor(source, previewMaterial, "_Color", "_BaseColor");
        return previewMaterial;
    }

    private static void CopyTexture(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (!source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
        {
            return;
        }

        var texture = source.GetTexture(sourceProperty);
        if (texture == null)
        {
            return;
        }

        target.SetTexture(targetProperty, texture);
        target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
        target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
    }

    private static void CopyColor(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (source.HasProperty(sourceProperty) && target.HasProperty(targetProperty))
        {
            target.SetColor(targetProperty, source.GetColor(sourceProperty));
        }
    }

    private void ConfigurePreviewCamera(Rect rect)
    {
        var camera = _previewRenderer!.camera;
        var bounds = _previewBounds.size.sqrMagnitude > 0.001f
            ? _previewBounds
            : new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));

        var target = bounds.center;
        var height = Mathf.Max(0.4f, bounds.size.y);
        switch (_previewFraming)
        {
            case PreviewFraming.UpperBody:
                target += Vector3.up * (height * 0.18f);
                height *= 0.55f;
                break;
            case PreviewFraming.Face:
                target += Vector3.up * (height * 0.38f);
                height *= 0.26f;
                break;
        }

        var aspect = Mathf.Max(0.25f, rect.width / Mathf.Max(1f, rect.height));
        var verticalDistance = height * 0.5f / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        var horizontalDistance = height * 0.28f / (Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * aspect);
        var distance = Mathf.Max(verticalDistance, horizontalDistance) * 1.18f;
        var direction = Quaternion.Euler(4f, _previewYaw, 0f) * Vector3.forward;
        camera.transform.position = target + direction * distance;
        camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
    }

    private static Bounds CalculatePreviewBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(false)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.up, new Vector3(1f, 2f, 1f));
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void ApplyHideFlags(Transform root)
    {
        root.gameObject.hideFlags = HideFlags.HideAndDontSave;
        foreach (Transform child in root)
        {
            ApplyHideFlags(child);
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

    private enum PreviewFraming
    {
        FullBody,
        UpperBody,
        Face
    }
}
