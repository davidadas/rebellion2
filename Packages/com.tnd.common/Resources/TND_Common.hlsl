#ifndef TND_COMMON_HLSL
#define TND_COMMON_HLSL

static uint unity_StereoEyeIndex;
#include "UnityInstancing.hlsl"

#if !defined(TND_USE_TEXARRAYS)
    #define TEXTURE2D       Texture2D
    #define RWTEXTURE2D     RWTexture2D
    #define COORD(pos)      (pos)
    #define UV(uv)          (uv)
    #define UNITY_XR_ASSIGN_VIEW_INDEX(idx)
#else
    #define TEXTURE2D       Texture2DArray
    #define RWTEXTURE2D     RWTexture2DArray
    #if defined(STEREO_INSTANCING_ON)
        #define COORD(pos)  uint3(pos, unity_StereoEyeIndex)
        #define UV(uv)      float3(uv, unity_StereoEyeIndex)
        #define UNITY_XR_ASSIGN_VIEW_INDEX(idx)     unity_StereoEyeIndex = idx
    #else
        #define COORD(pos)  uint3(pos, 0)
        #define UV(uv)      float3(uv, 0)
        #define UNITY_XR_ASSIGN_VIEW_INDEX(idx)
    #endif
#endif

#if defined(SHADER_API_PSSL)
#define SV_VERTEXID         S_VERTEX_ID
#define SV_POSITION         S_POSITION
#define SV_TARGET0          S_TARGET_OUTPUT0
#define SV_TARGET1          S_TARGET_OUTPUT1
#define SV_TARGET2          S_TARGET_OUTPUT2
#define SV_TARGET3          S_TARGET_OUTPUT3
#define SV_DEPTH            S_DEPTH_OUTPUT
#endif

uniform float4 _BlitScaleBias;

struct VertexIn
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOut
{
    float4 position : SV_POSITION;
    float2 texCoord : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

float4 GetFullScreenTriangleVertexPosition(uint vertexID)
{
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    float4 pos = float4(uv * 2.0 - 1.0, 0.5, 1.0);
    return pos;
}

float2 GetFullScreenTriangleTexCoord(uint vertexID)
{
#if (SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3) && !(SHADER_API_SWITCH || SHADER_API_SWITCH2)
    return float2((vertexID << 1) & 2, vertexID & 2);
#else
    return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
#endif
}

VertexOut VertMain(VertexIn input)
{
    VertexOut output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.position = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.texCoord = GetFullScreenTriangleTexCoord(input.vertexID);
    return output;
}

VertexOut VertMainScaleBias(VertexIn input)
{
    VertexOut output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.position = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.texCoord = GetFullScreenTriangleTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
    return output;
}

#endif  // TND_COMMON_HLSL
