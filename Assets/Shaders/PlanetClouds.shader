Shader "Custom/PlanetClouds"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _PoleFadeStart ("Pole Fade Start", Range(0.0, 1.0)) = 0.992
        _PoleFadeEnd ("Pole Fade End", Range(0.0, 1.0)) = 0.9998
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float pole : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _PoleFadeStart;
            float _PoleFadeEnd;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.pole = abs(normalize(input.normal).y);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 cloud = tex2D(_MainTex, input.uv);
                float poleFade = 1.0 - smoothstep(
                    _PoleFadeStart,
                    _PoleFadeEnd,
                    input.pole
                );
                cloud.a *= poleFade;
                return cloud;
            }
            ENDCG
        }
    }
}
