using UnityEngine;

namespace SM.Unity;

internal static class BattlePresentationMaterialFactory
{
    private static readonly string[] ShaderSearchOrder =
    {
        "Universal Render Pipeline/Unlit",
        "Unlit/Color",
        "Sprites/Default",
        "Universal Render Pipeline/Lit",
        "Standard",
        "Legacy Shaders/Diffuse",
        "Hidden/Internal-Colored"
    };

    public static Material Create(Color color)
    {
        var shader = ResolveShader();
        var material = new Material(shader)
        {
            name = $"BattlePresentation_{shader.name}"
        };
        ApplyColor(material, color);
        return material;
    }

    public static void ApplyColor(Material? material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static Shader ResolveShader()
    {
        foreach (var shaderName in ShaderSearchOrder)
        {
            var shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        throw new System.InvalidOperationException("No runtime presentation shader could be resolved for battle visuals.");
    }
}
