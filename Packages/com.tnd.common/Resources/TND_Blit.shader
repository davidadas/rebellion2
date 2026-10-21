// Simple procedural blit shader
Shader "Hidden/TND/Common/Blit"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass    // 0 - with only 2D texture input support
        {
            Name "Blit"

            HLSLPROGRAM
            #pragma vertex VertMainScaleBias
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #include "TND_Common.hlsl"

            uniform Texture2D _MainTex;

            SamplerState s_point_clamp_sampler;

            float4 main(VertexOut i) : SV_TARGET0
            {
                return _MainTex.SampleLevel(s_point_clamp_sampler, i.texCoord, 0);
            }

            ENDHLSL
        }

        Pass    // 1 - with texture array input support
        {
            Name "Blit"

            HLSLPROGRAM
            #pragma vertex VertMainScaleBias
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _MainTex;

            SamplerState s_point_clamp_sampler;

            float4 main(VertexOut i) : SV_TARGET0
            {
                return _MainTex.SampleLevel(s_point_clamp_sampler, UV(i.texCoord), 0);
            }

            ENDHLSL
        }

        Pass    // 2 - blit depth with texture array input support
        {
            Name "Blit Depth"

            HLSLPROGRAM
            #pragma vertex VertMainScaleBias
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _DepthTex;

            SamplerState s_point_clamp_sampler;

            float main(VertexOut i) : SV_DEPTH
            {
                return _DepthTex.SampleLevel(s_point_clamp_sampler, UV(i.texCoord), 0).r;
            }

            ENDHLSL
        }

        Pass    // 3 - blit depth to color with texture array input support
        {
            Name "Blit Depth to Color"

            HLSLPROGRAM
            #pragma vertex VertMainScaleBias
            #pragma fragment main
            #pragma target 3.5
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile __ TND_USE_TEXARRAYS

            #include "TND_Common.hlsl"

            uniform TEXTURE2D _DepthTex;

            SamplerState s_point_clamp_sampler;

            float4 main(VertexOut i) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return _DepthTex.SampleLevel(s_point_clamp_sampler, UV(i.texCoord), 0).rrrr;
            }

            ENDHLSL
        }
    }
}
