Shader "UI/Crisp Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _StencilComp ("Stencil comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil operation", Float) = 0
        _StencilWriteMask ("Stencil write mask", Float) = 255
        _StencilReadMask ("Stencil read mask", Float) = 255
        _ColorMask ("Color mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use alpha clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half4 _Color;
            half4 _TextureSampleAdd;
            float4 _ClipRect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = UnityObjectToClipPos(input.positionOS);
                output.positionOS = input.positionOS.xy;
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            // Catmull-Rom interpolation retains narrow highlights while smoothing enlarged texels.
            float4 CubicWeights(float value)
            {
                float squared = value * value;
                float cubed = squared * value;
                return float4(
                    -0.5 * value + squared - 0.5 * cubed,
                    1.0 - 2.5 * squared + 1.5 * cubed,
                    0.5 * value + 2.0 * squared - 1.5 * cubed,
                    -0.5 * squared + 0.5 * cubed
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 pixel = input.uv * _MainTex_TexelSize.zw - 0.5;
                float2 first = floor(pixel) - 1.0;
                float4 horizontal = CubicWeights(frac(pixel.x));
                float4 vertical = CubicWeights(frac(pixel.y));
                float4 color = 0;
                [unroll]
                for (int row = 0; row < 4; row++)
                {
                    [unroll]
                    for (int column = 0; column < 4; column++)
                    {
                        float2 uv = (first + float2(column, row) + 0.5) * _MainTex_TexelSize.xy;
                        half4 sampleColor = tex2D(_MainTex, uv) + _TextureSampleAdd;
                        // Filter premultiplied samples so transparent edges cannot darken the image.
                        sampleColor.rgb *= sampleColor.a;
                        color += sampleColor * horizontal[column] * vertical[row];
                    }
                }
                color.a = saturate(color.a);
                color.rgb = clamp(color.rgb, 0, color.a);
                color.rgb *= input.color.rgb * input.color.a;
                color.a *= input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(input.positionOS, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
