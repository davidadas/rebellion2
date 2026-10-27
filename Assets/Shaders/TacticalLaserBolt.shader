Shader "Rebellion/Tactical Laser Bolt"
{
    Properties
    {
        [HDR] _Color ("Beam Color", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "HDRenderPipeline"
            "IgnoreProjector" = "True"
        }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma require cubearray
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float transverseDistance = abs(input.uv.y * 2.0 - 1.0);
                float endpointDistance = min(input.uv.x, 1.0 - input.uv.x);
                float endpointFade = smoothstep(0.0, 0.09, endpointDistance);

                float halo = pow(saturate(1.0 - transverseDistance), 2.2) * endpointFade;
                float body = (1.0 - smoothstep(0.16, 0.34, transverseDistance)) * endpointFade;
                float core = (1.0 - smoothstep(0.035, 0.105, transverseDistance))
                    * smoothstep(0.015, 0.12, endpointDistance);

                half3 beam = _Color.rgb * (halo * 1.4 + body * 4.8);
                beam += half3(1.0, 0.96, 0.9) * core * 9.0;
                beam *= input.color.rgb * input.color.a;
                return half4(
                    beam,
                    saturate(halo + body + core) * input.color.a
                );
            }
            ENDHLSL
        }
    }
}
