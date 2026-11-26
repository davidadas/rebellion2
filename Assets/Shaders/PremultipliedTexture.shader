Shader "Custom/PremultipliedTexture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "HDRenderPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "UI"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
            };

            sampler2D _MainTex;
            half4 _Color;
            float4 _ClipRect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = UnityObjectToClipPos(input.positionOS);
                output.positionOS = input.positionOS.xy;
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sampleColor = tex2D(_MainTex, input.uv);
                half4 color = half4(
                    sampleColor.rgb * input.color.rgb * input.color.a,
                    sampleColor.a * input.color.a
                );
                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(input.positionOS, _ClipRect);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
}
