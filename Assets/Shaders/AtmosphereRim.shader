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
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            float4 _Color;
            float _Power;
            float _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rim = 1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float glow = pow(rim, _Power) * _Intensity;
                return fixed4(_Color.rgb, saturate(glow));
            }
            ENDCG
        }
    }
}
