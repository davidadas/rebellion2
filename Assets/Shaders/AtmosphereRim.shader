Shader "Custom/AtmosphereRim"
{
    Properties
    {
        _Color ("Color", Color) = (0.32, 0.55, 1.0, 1.0)
        _Power ("Fresnel Power", Float) = 2.5
        _Intensity ("Intensity", Float) = 1.6
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
        Blend SrcAlpha One
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirectionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Power;
                float _Intensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float rim = 1.0 - saturate(
                    dot(normalize(input.normalWS), normalize(input.viewDirectionWS))
                );
                float glow = pow(rim, _Power) * _Intensity;
                return half4(_Color.rgb, saturate(glow));
            }
            ENDHLSL
        }
    }
}
