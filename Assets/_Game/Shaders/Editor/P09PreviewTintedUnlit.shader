Shader "Hidden/SM/P09PreviewTintedUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Color2nd ("Second Color", Color) = (1, 1, 1, 1)
        _Color3rd ("Third Color", Color) = (1, 1, 1, 1)
        _PreviewTintStrength ("Preview Tint Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _Color2nd;
            fixed4 _Color3rd;
            float _PreviewTintStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float3 LiftToMinimumLuminance(float3 color, float minimumLuminance)
            {
                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                float scale = max(1.0, minimumLuminance / max(0.001, luminance));
                return saturate(color * scale);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sampled = tex2D(_MainTex, i.uv);
                float3 mainTint = LiftToMinimumLuminance(_Color.rgb, 0.36);
                fixed4 color;
                color.rgb = sampled.rgb * mainTint;
                color.a = sampled.a * _Color.a;
                clip(color.a - 0.01);

                float luminance = dot(sampled.rgb, float3(0.299, 0.587, 0.114));
                float shadowBand = 1.0 - smoothstep(0.16, 0.50, luminance);
                float highlightBand = smoothstep(0.56, 0.92, luminance);
                float3 shadowTint = sampled.rgb * LiftToMinimumLuminance(lerp(_Color.rgb, _Color3rd.rgb, 0.40), 0.34);
                float3 highlightTint = sampled.rgb * LiftToMinimumLuminance(lerp(_Color.rgb, _Color2nd.rgb, 0.32), 0.42);
                float3 bandTint = lerp(color.rgb, shadowTint, shadowBand * 0.42);
                bandTint = lerp(bandTint, highlightTint, highlightBand * 0.32);
                color.rgb = lerp(color.rgb, bandTint, saturate(_PreviewTintStrength));
                color.rgb = LiftToMinimumLuminance(color.rgb, 0.12);

                float3 normalWS = normalize(i.normalWS);
                float3 lightDir = normalize(float3(0.35, 0.65, 0.55));
                float diffuse = 0.68 + saturate(dot(normalWS, lightDir)) * 0.34;
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float rim = pow(1.0 - saturate(dot(normalWS, viewDir)), 3.0) * 0.10;
                color.rgb = saturate(color.rgb * diffuse + rim);
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
