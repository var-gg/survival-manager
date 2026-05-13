using UnityEditor;
using UnityEngine;

namespace SM.Editor;

public static partial class BattleMapCatalogBootstrap
{
    private static void EnsureWolfPineEdgeTreatment(Transform root)
    {
        EnsureWolfPineBuffer(root);
        EnsureWolfPineFrame(root);
        // EnsureWolfPineBackdrop(root); — 거대 BackgroundMountain(~1500m) prefab은 부감 카메라 시야를 압도해 비활성화.
        //                               필요 시 다시 활성화하고 BattlePresentationController.DisableObtrusiveBackdrops도 해제.
    }

    private static void EnsureWolfPineBuffer(Transform root)
    {
        var buffer = EnsureChild(root, "WolfPineBuffer");
        ClearChildren(buffer);

        foreach (var placement in BufferPlacements)
        {
            AddVendorPrefab(
                buffer,
                placement.Name,
                placement.Path,
                placement.LocalPosition,
                placement.LocalEulerAngles,
                placement.LocalScale);
        }
    }

    private static void EnsureWolfPineFrame(Transform root)
    {
        var frame = EnsureChild(root, "WolfPineFrame");
        ClearChildren(frame);

        foreach (var placement in FramePlacements)
        {
            AddVendorPrefab(
                frame,
                placement.Name,
                placement.Path,
                placement.LocalPosition,
                placement.LocalEulerAngles,
                placement.LocalScale);
        }
    }

    private static void EnsureWolfPineBackdrop(Transform root)
    {
        var backdrop = EnsureChild(root, "WolfPineBackdrop");
        ClearChildren(backdrop);

        AddVendorPrefab(
            backdrop,
            "BackdropMountain_Left",
            "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Mountains/P_fwOF_BackgroundMountain_01.prefab",
            new Vector3(-42f, -6.8f, 66f),
            new Vector3(0f, 168f, 0f),
            new Vector3(0.95f, 0.45f, 0.62f));
        AddVendorPrefab(
            backdrop,
            "BackdropMountain_Center",
            "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Mountains/P_fwOF_BackgroundMountain_01.prefab",
            new Vector3(0f, -7.0f, 70f),
            new Vector3(0f, 184f, 0f),
            new Vector3(1.10f, 0.48f, 0.68f));
        AddVendorPrefab(
            backdrop,
            "BackdropMountain_Right",
            "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Mountains/P_fwOF_BackgroundMountain_01.prefab",
            new Vector3(42f, -6.9f, 66.5f),
            new Vector3(0f, 198f, 0f),
            new Vector3(0.92f, 0.44f, 0.60f));
    }

    private static readonly VendorPlacement[] BufferPlacements =
    {
        new("Buffer_Grass_Top_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_1.prefab", new Vector3(-12.6f, -1.07f, 6.3f), new Vector3(0f, 28f, 0f), new Vector3(1.42f, 1.42f, 1.42f)),
        new("Buffer_Grass_Top_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_2.prefab", new Vector3(-9.8f, -1.07f, 7.2f), new Vector3(0f, -18f, 0f), new Vector3(1.20f, 1.20f, 1.20f)),
        new("Buffer_Fern_Top_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_01.prefab", new Vector3(-7.2f, -1.06f, 5.7f), new Vector3(0f, 36f, 0f), new Vector3(1.18f, 1.18f, 1.18f)),
        new("Buffer_Plant_Top_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_FW01_Plant_A_03.prefab", new Vector3(-4.8f, -1.06f, 7.6f), new Vector3(0f, -42f, 0f), new Vector3(1.18f, 1.18f, 1.18f)),
        new("Buffer_Stump_Top_05", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Stump_03.prefab", new Vector3(-2.0f, -1.05f, 6.5f), new Vector3(0f, 22f, 0f), new Vector3(1.02f, 1.02f, 1.02f)),
        new("Buffer_Grass_Top_06", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(0.9f, -1.07f, 7.4f), new Vector3(0f, 58f, 0f), new Vector3(1.34f, 1.34f, 1.34f)),
        new("Buffer_Fern_Top_07", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_02.prefab", new Vector3(3.6f, -1.06f, 5.6f), new Vector3(0f, -28f, 0f), new Vector3(1.12f, 1.12f, 1.12f)),
        new("Buffer_Plant_Top_08", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_FW01_Plant_B_02.prefab", new Vector3(6.2f, -1.06f, 7.5f), new Vector3(0f, 18f, 0f), new Vector3(1.16f, 1.16f, 1.16f)),
        new("Buffer_Bush_Top_09", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Bush_01.prefab", new Vector3(9.2f, -1.12f, 6.4f), new Vector3(0f, -16f, 0f), new Vector3(0.86f, 0.86f, 0.86f)),
        new("Buffer_Grass_Top_10", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_3.prefab", new Vector3(12.0f, -1.07f, 7.1f), new Vector3(0f, 44f, 0f), new Vector3(1.30f, 1.30f, 1.30f)),
        new("Buffer_Log_Top_11", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_01.prefab", new Vector3(13.7f, -1.04f, 5.3f), new Vector3(0f, -34f, 0f), new Vector3(1.25f, 1.25f, 1.25f)),
        new("Buffer_Flower_Top_12", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Flowers_03_A.prefab", new Vector3(-10.7f, -1.055f, 5.2f), new Vector3(0f, 16f, 0f), new Vector3(0.95f, 0.95f, 0.95f)),
        new("Buffer_Clover_Top_13", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Clover_03.prefab", new Vector3(-0.3f, -1.055f, 5.1f), new Vector3(0f, 72f, 0f), new Vector3(1.15f, 1.15f, 1.15f)),
        new("Buffer_Wood_Top_14", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_WoodDebris_Single_03.prefab", new Vector3(7.6f, -1.045f, 5.1f), new Vector3(0f, -70f, 0f), new Vector3(1.05f, 1.05f, 1.05f)),
        new("Buffer_Mushroom_Top_15", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Mushroom_A_Group_01.prefab", new Vector3(11.0f, -1.045f, 5.4f), new Vector3(0f, 12f, 0f), new Vector3(0.82f, 0.82f, 0.82f)),
        new("Buffer_Grass_Top_16", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(4.9f, -1.065f, 6.6f), new Vector3(0f, -74f, 0f), new Vector3(1.55f, 1.55f, 1.55f)),
        new("Buffer_Grass_Bottom_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_2.prefab", new Vector3(-12.3f, -1.07f, -6.4f), new Vector3(0f, -24f, 0f), new Vector3(1.36f, 1.36f, 1.36f)),
        new("Buffer_Bush_Bottom_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_FW01_Bush_02.prefab", new Vector3(-9.5f, -1.12f, -7.3f), new Vector3(0f, 24f, 0f), new Vector3(0.84f, 0.84f, 0.84f)),
        new("Buffer_Fern_Bottom_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_03.prefab", new Vector3(-6.6f, -1.06f, -5.6f), new Vector3(0f, -32f, 0f), new Vector3(1.16f, 1.16f, 1.16f)),
        new("Buffer_Plant_Bottom_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_FW01_Plant_B_04.prefab", new Vector3(-3.8f, -1.06f, -7.6f), new Vector3(0f, 34f, 0f), new Vector3(1.14f, 1.14f, 1.14f)),
        new("Buffer_Log_Bottom_05", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Log_05.prefab", new Vector3(-1.1f, -1.04f, -5.5f), new Vector3(0f, 38f, 0f), new Vector3(1.28f, 1.28f, 1.28f)),
        new("Buffer_Grass_Bottom_06", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_M_1.prefab", new Vector3(1.9f, -1.07f, -7.2f), new Vector3(0f, -48f, 0f), new Vector3(1.36f, 1.36f, 1.36f)),
        new("Buffer_Fern_Bottom_07", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Fern_01.prefab", new Vector3(4.6f, -1.06f, -5.8f), new Vector3(0f, 28f, 0f), new Vector3(1.12f, 1.12f, 1.12f)),
        new("Buffer_Plant_Bottom_08", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_FW01_Plant_A_04.prefab", new Vector3(7.2f, -1.06f, -7.5f), new Vector3(0f, -22f, 0f), new Vector3(1.18f, 1.18f, 1.18f)),
        new("Buffer_Stump_Bottom_09", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Stump_05.prefab", new Vector3(9.9f, -1.045f, -6.1f), new Vector3(0f, 18f, 0f), new Vector3(0.95f, 0.95f, 0.95f)),
        new("Buffer_Grass_Bottom_10", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_L_3B.prefab", new Vector3(12.7f, -1.07f, -7.0f), new Vector3(0f, -38f, 0f), new Vector3(1.26f, 1.26f, 1.26f)),
        new("Buffer_Branch_Bottom_11", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_FW01_ForestDebris_Branch_04.prefab", new Vector3(13.6f, -1.04f, -5.2f), new Vector3(0f, 48f, 0f), new Vector3(1.12f, 1.12f, 1.12f)),
        new("Buffer_Flower_Bottom_12", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Flowers_02_C.prefab", new Vector3(-11.0f, -1.055f, -5.2f), new Vector3(0f, -18f, 0f), new Vector3(0.90f, 0.90f, 0.90f)),
        new("Buffer_Clover_Bottom_13", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Clover_01.prefab", new Vector3(-2.8f, -1.055f, -5.1f), new Vector3(0f, -64f, 0f), new Vector3(1.10f, 1.10f, 1.10f)),
        new("Buffer_Wood_Bottom_14", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_WoodDebris_Single_02.prefab", new Vector3(5.8f, -1.045f, -5.0f), new Vector3(0f, 72f, 0f), new Vector3(1.00f, 1.00f, 1.00f)),
        new("Buffer_Mushroom_Bottom_15", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Mushroom_B_Group_01.prefab", new Vector3(10.8f, -1.045f, -5.4f), new Vector3(0f, -24f, 0f), new Vector3(0.82f, 0.82f, 0.82f)),
        new("Buffer_Grass_Bottom_16", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_Grass_S_01.prefab", new Vector3(3.1f, -1.065f, -6.4f), new Vector3(0f, 68f, 0f), new Vector3(1.50f, 1.50f, 1.50f)),
        new("Buffer_Plant_Outer_Top_17", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_ForestPlant_B_05.prefab", new Vector3(-14.2f, -1.08f, 8.3f), new Vector3(0f, -18f, 0f), new Vector3(1.08f, 1.08f, 1.08f)),
        new("Buffer_Plant_Outer_Top_18", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_ForestPlant_03.prefab", new Vector3(14.1f, -1.08f, 8.0f), new Vector3(0f, 28f, 0f), new Vector3(1.05f, 1.05f, 1.05f)),
        new("Buffer_Plant_Outer_Bottom_17", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_ForestPlant_B_07.prefab", new Vector3(-14.4f, -1.08f, -8.1f), new Vector3(0f, 20f, 0f), new Vector3(1.06f, 1.06f, 1.06f)),
        new("Buffer_Plant_Outer_Bottom_18", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Foliage/Summer/P_fwOF_ForestPlant_04.prefab", new Vector3(14.0f, -1.08f, -8.2f), new Vector3(0f, -26f, 0f), new Vector3(1.05f, 1.05f, 1.05f)),
    };

    private static readonly VendorPlacement[] FramePlacements =
    {
        new("Frame_Tree_L_Top_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_1.prefab", new Vector3(-24.0f, -1.16f, 15.6f), new Vector3(0f, 12f, 0f), new Vector3(0.92f, 0.92f, 0.92f)),
        new("Frame_Tree_L_Top_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(-13.0f, -1.16f, 17.2f), new Vector3(0f, -18f, 0f), new Vector3(0.88f, 0.88f, 0.88f)),
        new("Frame_Tree_L_Top_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(13.4f, -1.16f, 16.8f), new Vector3(0f, 24f, 0f), new Vector3(0.86f, 0.86f, 0.86f)),
        new("Frame_Tree_L_Top_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_2.prefab", new Vector3(24.5f, -1.16f, 15.4f), new Vector3(0f, -32f, 0f), new Vector3(0.90f, 0.90f, 0.90f)),
        new("Frame_Tree_L_Bottom_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_1.prefab", new Vector3(-33.5f, -1.16f, -15.8f), new Vector3(0f, -24f, 0f), new Vector3(0.48f, 0.48f, 0.48f)),
        new("Frame_Tree_L_Bottom_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(-22.4f, -1.16f, -16.4f), new Vector3(0f, 34f, 0f), new Vector3(0.54f, 0.54f, 0.54f)),
        new("Frame_Tree_L_Bottom_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_L_1.prefab", new Vector3(22.0f, -1.16f, -16.1f), new Vector3(0f, -8f, 0f), new Vector3(0.52f, 0.52f, 0.52f)),
        new("Frame_Tree_L_Bottom_04", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Trees/Summer/P_fwOF_Tree_M_2.prefab", new Vector3(33.2f, -1.16f, -15.5f), new Vector3(0f, 18f, 0f), new Vector3(0.48f, 0.48f, 0.48f)),
        new("Frame_Rock_Top_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_01_B.prefab", new Vector3(-17.6f, -1.06f, 9.8f), new Vector3(0f, 32f, 0f), new Vector3(0.58f, 0.58f, 0.58f)),
        new("Frame_Rock_Top_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_02_B.prefab", new Vector3(-2.2f, -1.06f, 10.6f), new Vector3(0f, -18f, 0f), new Vector3(0.52f, 0.52f, 0.52f)),
        new("Frame_Rock_Top_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_03_B.prefab", new Vector3(17.0f, -1.06f, 9.8f), new Vector3(0f, 26f, 0f), new Vector3(0.56f, 0.56f, 0.56f)),
        new("Frame_Rock_Bottom_01", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_03_B.prefab", new Vector3(-19.4f, -1.06f, -9.4f), new Vector3(0f, -28f, 0f), new Vector3(0.50f, 0.50f, 0.50f)),
        new("Frame_Rock_Bottom_02", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_01_B.prefab", new Vector3(0.8f, -1.06f, -9.6f), new Vector3(0f, 20f, 0f), new Vector3(0.46f, 0.46f, 0.46f)),
        new("Frame_Rock_Bottom_03", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Rocks/P_fwOF_Rock_L_02_B.prefab", new Vector3(19.2f, -1.06f, -9.2f), new Vector3(0f, -34f, 0f), new Vector3(0.52f, 0.52f, 0.52f)),
        new("Frame_StandingStone_Top", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Standing_Stone_03.prefab", new Vector3(-7.2f, -1.03f, 6.2f), new Vector3(0f, 18f, 0f), new Vector3(0.96f, 0.96f, 0.96f)),
        new("Frame_StandingStone_Bottom", "Assets/TriForge Assets/Fantasy Worlds - Forest/Prefabs/Doodads/P_fwOF_Standing_Stone_05.prefab", new Vector3(8.0f, -1.03f, -6.5f), new Vector3(0f, -10f, 0f), new Vector3(0.76f, 0.76f, 0.76f)),
    };

    private readonly struct VendorPlacement
    {
        public VendorPlacement(
            string name,
            string path,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            Name = name;
            Path = path;
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
        }

        public string Name { get; }
        public string Path { get; }
        public Vector3 LocalPosition { get; }
        public Vector3 LocalEulerAngles { get; }
        public Vector3 LocalScale { get; }
    }
}
