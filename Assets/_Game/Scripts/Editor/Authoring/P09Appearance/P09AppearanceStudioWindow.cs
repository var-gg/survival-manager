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

    [MenuItem("SM/Characters/P09 Appearance Studio")]
    public static void Open()
    {
        GetWindow<P09AppearanceStudioWindow>("P09 Appearance");
    }

    private void OnEnable()
    {
        RefreshData(ensurePresets: true);
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
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                RefreshData(ensurePresets: false);
            }

            if (GUILayout.Button("Rebuild P09 Catalog", EditorStyles.toolbarButton, GUILayout.Width(132f)))
            {
                RefreshData(ensurePresets: true);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_selectedPreset == null))
            {
                if (GUILayout.Button("Update Preview", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                {
                    UpdatePreview();
                }

                if (GUILayout.Button("Clear Preview", EditorStyles.toolbarButton, GUILayout.Width(92f)))
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
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
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
                EditorGUILayout.HelpBox("Select a character to edit its P09 appearance preset.", MessageType.Info);
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.LabelField(_selectedPreset.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_selectedPreset.CharacterId, EditorStyles.miniLabel, GUILayout.Height(18f));

            EditorGUILayout.Space(6f);
            DrawPartSection("Body", new[]
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

            DrawPartSection("Equipment", new[]
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
                if (GUILayout.Button("Ping Preset", GUILayout.Width(110f)))
                {
                    EditorGUIUtility.PingObject(_selectedPreset);
                    Selection.activeObject = _selectedPreset;
                }

                if (GUILayout.Button("Save"))
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
            EditorGUILayout.LabelField(type.ToString(), "No P09 options");
            return;
        }

        var currentId = _selectedPreset.GetContentId(type);
        var currentIndex = Mathf.Max(0, options.FindIndex(option => option.ContentId == currentId));
        var labels = options.Select(option => option.Label).ToArray();
        EditorGUI.BeginChangeCheck();
        var nextIndex = EditorGUILayout.Popup(ObjectNames.NicifyVariableName(type.ToString()), currentIndex, labels);
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
        EditorGUILayout.LabelField("Material Color Adjust", EditorStyles.boldLabel);
        var serialized = new SerializedObject(_selectedPreset);
        var property = serialized.FindProperty("materialColorOverrides");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, includeChildren: true);
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

        _selectedPreset = BattleP09AppearanceCatalogBuilder.FindPreset(character.Id)
                          ?? BattleP09AppearanceCatalogBuilder.EnsurePreset(character, _catalog, 0);
        Selection.activeObject = _selectedPreset;
        UpdatePreview();
    }

    private void SaveSelectedPreset(bool updatePreview)
    {
        if (_selectedPreset == null)
        {
            return;
        }

        EditorUtility.SetDirty(_selectedPreset);
        AssetDatabase.SaveAssets();
        if (updatePreview)
        {
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (_selectedPreset == null)
        {
            DestroyPreview();
            return;
        }

        if (_previewRoot == null)
        {
            CreatePreview();
        }

        if (_previewRoot == null)
        {
            return;
        }

        foreach (var material in _previewMaterials)
        {
            if (material != null)
            {
                DestroyImmediate(material);
            }
        }

        _previewMaterials.Clear();
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
            DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }

        var existing = GameObject.Find(PreviewRootName);
        if (existing != null)
        {
            DestroyImmediate(existing);
        }
    }
}
