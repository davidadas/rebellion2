Shader "Custom/PlanetSurface"
{
    Properties
    {
        _BaseMap ("Planet Albedo", 2D) = "white" {}
        _BaseColor ("Planet Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _CloudMap ("Cloud Coverage", 2D) = "black" {}
        _CloudColor ("Cloud Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _CloudOpacity ("Cloud Opacity", Range(0.0, 1.0)) = 1.0
        _CloudRotationSpeed ("Cloud Rotation Speed", Float) = 0.3333333
        _CloudRotationOffset ("Cloud Rotation Offset", Float) = 0.0
        _CloudAltitude ("Cloud Altitude", Range(0.0, 0.04)) = 0.012
        _SurfaceRelief ("Surface Relief", Range(0.0, 8.0)) = 1.25
        _OceanRelief ("Ocean Relief", Range(0.0, 4.0)) = 0.25
        _SurfaceReliefRadius ("Surface Relief Radius", Range(0.5, 4.0)) = 1.5
        _CloudRelief ("Cloud Relief", Range(0.0, 12.0)) = 3.0
        _CloudShadowStrength ("Cloud Shadow Strength", Range(0.0, 1.0)) = 0.18
        _CloudSelfShadowStrength ("Cloud Self Shadow Strength", Range(0.0, 1.0)) = 0.28
        _CloudSilverLining ("Cloud Silver Lining", Range(0.0, 2.0)) = 0.18
        _CityLightColor ("City Light Color", Color) = (1.0, 0.38, 0.08, 1.0)
        _CityLightIntensity ("City Light Intensity", Range(0.0, 4.0)) = 1.4
        _NightLightColor ("Night Light Color", Color) = (0.55, 0.68, 1.0, 1.0)
        _NightLightStrength ("Night Light Strength", Range(0.0, 0.25)) = 0.10
        _SunDirection ("Sun Direction", Vector) = (0.80, 0.46, -0.38, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }
        ZWrite On
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CloudMap);
            SAMPLER(sampler_CloudMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;
                float4 _CloudMap_TexelSize;
                half4 _BaseColor;
                half4 _CloudColor;
                float _CloudOpacity;
                float _CloudRotationSpeed;
                float _CloudRotationOffset;
                float _CloudAltitude;
                float _SurfaceRelief;
                float _OceanRelief;
                float _SurfaceReliefRadius;
                float _CloudRelief;
                float _CloudShadowStrength;
                float _CloudSelfShadowStrength;
                float _CloudSilverLining;
                half4 _CityLightColor;
                float _CityLightIntensity;
                half4 _NightLightColor;
                float _NightLightStrength;
                float4 _SunDirection;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.normalOS = normalize(input.normalOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half SurfaceLuminance(half3 color)
            {
                return dot(color, half3(0.2126, 0.7152, 0.0722));
            }

            half OceanCoverage(half3 color)
            {
                half blueDominance = color.b - max(color.r, color.g);
                half cyanDominance = min(color.g, color.b) - color.r;
                half chroma = max(color.r, max(color.g, color.b))
                    - min(color.r, min(color.g, color.b));
                half waterColor = blueDominance + max(cyanDominance, 0.0) * 0.35;
                half saturationGate = smoothstep(0.015, 0.09, chroma);
                return smoothstep(0.008, 0.055, waterColor) * saturationGate;
            }

            float3x3 CotangentFrame(float3 normalWS, float3 positionWS, float2 uv)
            {
                float3 positionDerivativeX = ddx(positionWS);
                float3 positionDerivativeY = ddy(positionWS);
                float2 uvDerivativeX = ddx(uv);
                float2 uvDerivativeY = ddy(uv);
                float3 perpendicularY = cross(positionDerivativeY, normalWS);
                float3 perpendicularX = cross(normalWS, positionDerivativeX);
                float3 tangent = perpendicularY * uvDerivativeX.x
                    + perpendicularX * uvDerivativeY.x;
                float3 bitangent = perpendicularY * uvDerivativeX.y
                    + perpendicularX * uvDerivativeY.y;
                float inverseLength = rsqrt(max(dot(tangent, tangent), dot(bitangent, bitangent)));
                return float3x3(
                    tangent * inverseLength,
                    bitangent * inverseLength,
                    normalWS
                );
            }

            half SampleSurfaceHeight(float2 uv)
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
                return SurfaceLuminance(color);
            }

            float3 ResolveSurfaceNormalWS(Varyings input, half ocean)
            {
                float2 offset = _BaseMap_TexelSize.xy * _SurfaceReliefRadius;
                half heightLeft = SampleSurfaceHeight(input.uv - float2(offset.x, 0.0));
                half heightRight = SampleSurfaceHeight(input.uv + float2(offset.x, 0.0));
                half heightDown = SampleSurfaceHeight(input.uv - float2(0.0, offset.y));
                half heightUp = SampleSurfaceHeight(input.uv + float2(0.0, offset.y));
                float relief = lerp(_SurfaceRelief, _OceanRelief, ocean);
                float3 normalTS = normalize(
                    float3(
                        (heightLeft - heightRight) * relief,
                        (heightDown - heightUp) * relief,
                        1.0
                    )
                );
                float3x3 tangentFrame = CotangentFrame(
                    normalize(input.normalWS),
                    input.positionWS,
                    input.uv
                );
                return normalize(mul(normalTS, tangentFrame));
            }

            float2 DirectionToCloudUv(float3 direction)
            {
                float longitude = atan2(direction.x, -direction.z);
                float latitude = asin(clamp(direction.y, -1.0, 1.0));
                return float2(
                    longitude * 0.15915494309189535 + 0.5,
                    latitude * 0.3183098861837907 + 0.5
                );
            }

            half SampleCloudCoverage(float2 uv)
            {
                return saturate(
                    SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, uv).a
                    * _CloudOpacity
                    * _CloudColor.a
                );
            }

            float3 ResolveCloudNormalWS(
                float3 direction,
                float2 cloudUv,
                float3 eastOS,
                float3 northOS
            )
            {
                float2 offset = _CloudMap_TexelSize.xy * 1.5;
                half heightLeft = SampleCloudCoverage(cloudUv - float2(offset.x, 0.0));
                half heightRight = SampleCloudCoverage(cloudUv + float2(offset.x, 0.0));
                half heightDown = SampleCloudCoverage(cloudUv - float2(0.0, offset.y));
                half heightUp = SampleCloudCoverage(cloudUv + float2(0.0, offset.y));
                float3 cloudNormalOS = normalize(
                    direction
                    + eastOS * (heightLeft - heightRight) * _CloudRelief
                    + northOS * (heightDown - heightUp) * _CloudRelief
                );
                return normalize(TransformObjectToWorldNormal(cloudNormalOS));
            }

            half ResolveCityLights(float2 uv, half3 surfaceColor, half ocean, half night)
            {
                float2 cityCoordinate = uv * float2(900.0, 450.0);
                float2 cityCell = floor(cityCoordinate);
                float2 cellPosition = frac(cityCoordinate) - 0.5;
                float random = frac(
                    sin(dot(cityCell, float2(12.9898, 78.233))) * 43758.5453
                );
                float pointLightShape = smoothstep(0.16, 0.015, length(cellPosition));
                float settlement = step(0.992, random);
                half greenLand = saturate(
                    (surfaceColor.g - surfaceColor.b * 0.45 - surfaceColor.r * 0.10) * 3.0
                );
                half ice = smoothstep(0.58, 0.90, SurfaceLuminance(surfaceColor));
                return pointLightShape
                    * settlement
                    * greenLand
                    * (1.0 - ocean)
                    * (1.0 - ice)
                    * night;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half ocean = OceanCoverage(surface.rgb);
                float3 surfaceNormalWS = ResolveSurfaceNormalWS(input, ocean);
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 sunDirectionWS = normalize(_SunDirection.xyz);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 nightLight = _NightLightColor.rgb * _NightLightStrength;

                float angle = radians(
                    _CloudRotationOffset + _CloudRotationSpeed * _Time.y
                );
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                float3 direction = normalize(input.normalOS);
                float3 cloudDirection = float3(
                    cosine * direction.x + sine * direction.z,
                    direction.y,
                    -sine * direction.x + cosine * direction.z
                );
                float3 viewDirectionOS = normalize(
                    TransformWorldToObjectDir(viewDirectionWS)
                );
                cloudDirection = normalize(
                    cloudDirection + viewDirectionOS * _CloudAltitude
                );
                float2 cloudUv = DirectionToCloudUv(cloudDirection);
                half cloudCoverage = SampleCloudCoverage(cloudUv);

                float3 eastOS = float3(-cloudDirection.z, 0.0, cloudDirection.x);
                if (dot(eastOS, eastOS) < 0.0001)
                    eastOS = float3(1.0, 0.0, 0.0);
                else
                    eastOS = normalize(eastOS);
                float3 northOS = normalize(cross(eastOS, cloudDirection));
                float3 cloudNormalWS = ResolveCloudNormalWS(
                    cloudDirection,
                    cloudUv,
                    eastOS,
                    northOS
                );

                float3 sunDirectionOS = normalize(TransformWorldToObjectDir(sunDirectionWS));
                float2 cloudLightDirection = float2(
                    dot(sunDirectionOS, eastOS),
                    dot(sunDirectionOS, northOS)
                );
                float2 cloudLightOffset = cloudLightDirection
                    * _CloudMap_TexelSize.xy
                    * 14.0;
                half upstreamCloud = SampleCloudCoverage(cloudUv + cloudLightOffset);
                half groundShadowCloud = SampleCloudCoverage(cloudUv - cloudLightOffset * 1.8);
                half cloudSelfShadow = 1.0 - upstreamCloud * _CloudSelfShadowStrength;
                half surfaceSunlight = saturate(dot(surfaceNormalWS, sunDirectionWS));
                half surfaceShadow = 1.0
                    - groundShadowCloud * _CloudShadowStrength;
                half3 litSurface = surface.rgb
                    * (
                        nightLight
                        + mainLight.color
                            * surfaceSunlight
                            * surfaceShadow
                    );

                half cloudSunlight = saturate(dot(cloudNormalWS, sunDirectionWS));
                half cloudRim = pow(
                    1.0 - saturate(dot(cloudNormalWS, viewDirectionWS)),
                    3.0
                );
                half cloudEdge = smoothstep(0.04, 0.42, cloudCoverage)
                    * (1.0 - smoothstep(0.52, 0.96, cloudCoverage));

                half3 cloudLighting = _CloudColor.rgb
                    * (
                        nightLight * 1.35
                        + mainLight.color * cloudSunlight * cloudSelfShadow
                    );
                cloudLighting += _CloudColor.rgb
                    * mainLight.color
                    * cloudEdge
                    * cloudRim
                    * cloudSunlight
                    * _CloudSilverLining;

                half3 combined = lerp(
                    litSurface,
                    cloudLighting,
                    cloudCoverage
                );
                float sunlight = dot(normalize(input.normalWS), sunDirectionWS);
                half night = 1.0 - smoothstep(-0.15, 0.10, sunlight);
                half cityLights = ResolveCityLights(input.uv, surface.rgb, ocean, night)
                    * _CityLightIntensity
                    * (1.0 - cloudCoverage);
                half3 emission = _CityLightColor.rgb * cityLights;
                return half4(combined + emission, 1.0);
            }
            ENDHLSL
        }
    }
}
