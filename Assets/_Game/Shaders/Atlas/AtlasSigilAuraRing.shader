Shader "SM/Atlas/SigilAuraRing"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.5, 0.25, 0.28)
        _EmissionColor ("Emission Color", Color) = (1, 0.5, 0.25, 1)
        _PulseOffset ("Pulse Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Blend One One
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
                float3 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _PulseOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half radial = saturate(length(input.positionOS.xz));
                half pulse = 0.72h + 0.28h * sin(_Time.y * 1.8h + radial * 5.0h + _PulseOffset);
                half edge = smoothstep(0.18h, 0.88h, radial);
                half alpha = saturate(_BaseColor.a * pulse * edge);
                return half4(_EmissionColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
