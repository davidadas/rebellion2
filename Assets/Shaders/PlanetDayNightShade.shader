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
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend DstColor Zero
        ZWrite Off
        ZTest LEqual
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
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            float4 _SunDirection;
            float _NightBrightness;
            float _TerminatorStart;
            float _TerminatorEnd;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float sunlight = dot(
                    normalize(input.worldNormal),
                    normalize(_SunDirection.xyz)
                );
                float daylight = smoothstep(_TerminatorStart, _TerminatorEnd, sunlight);
                float brightness = lerp(_NightBrightness, 1.0, daylight);
                return fixed4(brightness, brightness, brightness, 1.0);
            }
            ENDCG
        }
    }
}
