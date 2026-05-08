Shader "Hidden/SM/P09PreviewTintedUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * _Color;
                clip(color.a - 0.01);

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
