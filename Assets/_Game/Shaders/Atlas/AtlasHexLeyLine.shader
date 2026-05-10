Shader "SM/Atlas/HexLeyLine"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.78, 0.34, 0.45)
        _EmissionColor ("Emission Color", Color) = (1, 0.78, 0.34, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.52
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color = lerp(_BaseColor.rgb, _EmissionColor.rgb, 0.35);
                return half4(color, saturate(_BaseColor.a * _Alpha));
            }
            ENDHLSL
        }
    }
}
