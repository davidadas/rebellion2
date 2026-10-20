Shader "Hidden/Tests/PlanetSurfaceBinding"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _CloudMap ("Cloud Map", 2D) = "black" {}
        _CloudRotationSpeed ("Cloud Rotation Speed", Float) = 0
    }

    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 position : POSITION) : SV_POSITION
            {
                return position;
            }

            float4 Frag() : SV_Target
            {
                return float4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
