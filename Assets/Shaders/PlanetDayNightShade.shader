Shader "Custom/PlanetDayNightShade"
{
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (0.80, 0.46, -0.38, 0.0)
        _NightBrightness ("Night Brightness", Range(0.0, 1.0)) = 0.045
        _TerminatorStart ("Terminator Start", Range(-1.0, 1.0)) = -0.10
        _TerminatorEnd ("Full Daylight", Range(-1.0, 1.0)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend DstColor Zero
        ZWrite Off
        ZTest LEqual
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDirection;
                float _NightBrightness;
                float _TerminatorStart;
                float _TerminatorEnd;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float sunlight = dot(
                    normalize(input.normalWS),
                    normalize(_SunDirection.xyz)
                );
                float daylight = smoothstep(_TerminatorStart, _TerminatorEnd, sunlight);
                float brightness = lerp(_NightBrightness, 1.0, daylight);
                return half4(brightness, brightness, brightness, 1.0);
            }
            ENDHLSL
        }
    }
}
