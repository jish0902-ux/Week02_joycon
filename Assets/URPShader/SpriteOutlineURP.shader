Shader "SpriteOutlineURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0,8)) = 2

        _Emission ("Emission", Range(0,5)) = 0
        _HighlightLerp ("Highlight Lerp (0~1)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags{
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _Emission;
                float  _HighlightLerp;
                float4 _MainTex_TexelSize; // (1/w,1/h,w,h)
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float alpha = baseCol.a;

                // 빠른 4탭 확장(Outline): 텍셀 크기 * 두께(px) * 하이라이트 보간
                float thickness = _OutlineThickness * _HighlightLerp;
                float2 texel = _MainTex_TexelSize.xy * max(thickness, 0);

                float aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x, 0)).a;
                float aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x, 0)).a;
                float aU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0,  texel.y)).a;
                float aD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, -texel.y)).a;

                float maxN = max(max(aL, aR), max(aU, aD));
                // 현재 픽셀이 비어 있고 주변이 채워져 있으면 외곽선
                float outlineMask = step(0.001, maxN) * (1 - step(0.001, alpha));

                // 발광(볼륨 블룸과 함께 쓰면 하이라이트가 눈에 잘 띔)
                float emission = _Emission * _HighlightLerp;

                float3 rgb = baseCol.rgb + (_OutlineColor.rgb * outlineMask) + emission;
                float   a  = saturate(alpha + _OutlineColor.a * outlineMask);

                return float4(rgb, a);
            }
            ENDHLSL
        }
    }
}
