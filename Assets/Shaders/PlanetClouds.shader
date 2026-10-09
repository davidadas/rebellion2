Shader "Custom/PlanetClouds"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _PoleFadeStart ("Pole Fade Start", Range(0.0, 1.0)) = 0.965
        _PoleFadeEnd ("Pole Fade End", Range(0.0, 1.0)) = 0.998
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float pole : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _PoleFadeStart;
                float _PoleFadeEnd;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.pole = abs(normalize(input.normalOS).y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float poleFade = 1.0 - smoothstep(
                    _PoleFadeStart,
                    _PoleFadeEnd,
                    input.pole
                );
                cloud.a *= poleFade;
                return cloud;
            }
            ENDHLSL
        }
    }
}
