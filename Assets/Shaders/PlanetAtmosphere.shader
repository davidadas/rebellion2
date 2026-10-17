Shader "Custom/PlanetAtmosphere"
{
    Properties
    {
        _PlanetRadiusRatio ("Planet Radius Ratio", Range(0.8, 0.999)) = 0.9615385
        _RayleighColor ("Rayleigh Color", Color) = (0.16, 0.38, 0.85, 1.0)
        _RayleighStrength ("Rayleigh Strength", Range(0.0, 32.0)) = 8.0
        _MieColor ("Mie Color", Color) = (1.0, 0.82, 0.58, 1.0)
        _MieStrength ("Mie Strength", Range(0.0, 16.0)) = 0.9
        _RayleighScaleHeight ("Rayleigh Scale Height", Range(0.01, 1.0)) = 0.22
        _MieScaleHeight ("Mie Scale Height", Range(0.01, 1.0)) = 0.10
        _MieAnisotropy ("Mie Anisotropy", Range(0.0, 0.95)) = 0.76
        _SunIntensity ("Sun Intensity", Range(0.0, 32.0)) = 5.0
        _Exposure ("Exposure", Range(0.1, 16.0)) = 1.5
        _SunDirection ("Sun Direction", Vector) = (0.80, 0.46, -0.38, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define VIEW_STEP_COUNT 12
            #define LIGHT_STEP_COUNT 6

            static const float ATMOSPHERE_PI = 3.14159265359;
            static const float OUTER_RADIUS = 1.0;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 rayDirectionOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _PlanetRadiusRatio;
                half4 _RayleighColor;
                float _RayleighStrength;
                half4 _MieColor;
                float _MieStrength;
                float _RayleighScaleHeight;
                float _MieScaleHeight;
                float _MieAnisotropy;
                float _SunIntensity;
                float _Exposure;
                float4 _SunDirection;
            CBUFFER_END

            float2 RaySphereIntersections(float3 rayOrigin, float3 rayDirection, float radius)
            {
                float projectedOrigin = dot(rayOrigin, rayDirection);
                float originOffset = dot(rayOrigin, rayOrigin) - radius * radius;
                float discriminant = projectedOrigin * projectedOrigin - originOffset;
                if (discriminant < 0.0)
                    return float2(-1.0, -1.0);

                float root = sqrt(discriminant);
                return float2(-projectedOrigin - root, -projectedOrigin + root);
            }

            float2 AtmosphereDensity(float3 positionOS)
            {
                float atmosphereThickness = max(1.0 - _PlanetRadiusRatio, 0.0001);
                float altitude = saturate(
                    (length(positionOS) - _PlanetRadiusRatio) / atmosphereThickness
                );
                return float2(
                    exp(-altitude / max(_RayleighScaleHeight, 0.0001)),
                    exp(-altitude / max(_MieScaleHeight, 0.0001))
                );
            }

            float InterleavedGradientNoise(float2 pixelPosition)
            {
                return frac(
                    52.9829189
                    * frac(dot(pixelPosition, float2(0.06711056, 0.00583715)))
                );
            }

            bool IsPlanetOccludingSun(float3 positionOS, float3 sunDirectionOS)
            {
                float2 planetHits = RaySphereIntersections(
                    positionOS,
                    sunDirectionOS,
                    _PlanetRadiusRatio
                );
                return planetHits.x > 0.0001;
            }

            float2 IntegrateLightOpticalDepth(
                float3 positionOS,
                float3 sunDirectionOS,
                float sampleOffset
            )
            {
                if (IsPlanetOccludingSun(positionOS, sunDirectionOS))
                    return float2(-1.0, -1.0);

                float2 atmosphereHits = RaySphereIntersections(
                    positionOS,
                    sunDirectionOS,
                    OUTER_RADIUS
                );
                float pathLength = max(atmosphereHits.y, 0.0);
                float stepLength = pathLength / LIGHT_STEP_COUNT;
                float2 opticalDepth = 0.0;

                [unroll]
                for (int index = 0; index < LIGHT_STEP_COUNT; index++)
                {
                    float distanceAlongRay = (index + sampleOffset) * stepLength;
                    opticalDepth += AtmosphereDensity(
                        positionOS + sunDirectionOS * distanceAlongRay
                    ) * stepLength;
                }

                return opticalDepth;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz * 2.0;
                output.rayDirectionOS = -GetObjectSpaceNormalizeViewDir(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 rayOrigin = normalize(input.positionOS) * (OUTER_RADIUS - 0.0001);
                float3 rayDirection = normalize(input.rayDirectionOS);
                float3 sunDirection = TransformWorldToObjectDir(
                    normalize(_SunDirection.xyz),
                    true
                );

                float2 atmosphereHits = RaySphereIntersections(
                    rayOrigin,
                    rayDirection,
                    OUTER_RADIUS
                );
                float pathLength = max(atmosphereHits.y, 0.0);
                float2 planetHits = RaySphereIntersections(
                    rayOrigin,
                    rayDirection,
                    _PlanetRadiusRatio
                );
                if (planetHits.x > 0.0001)
                    pathLength = min(pathLength, planetHits.x);

                float stepLength = pathLength / VIEW_STEP_COUNT;
                float2 viewOpticalDepth = 0.0;
                float3 rayleighScattering = 0.0;
                float3 mieScattering = 0.0;
                float3 betaRayleigh = _RayleighColor.rgb * _RayleighStrength;
                float3 betaMie = _MieColor.rgb * _MieStrength;
                float viewSampleOffset = InterleavedGradientNoise(input.positionHCS.xy);

                [unroll]
                for (int index = 0; index < VIEW_STEP_COUNT; index++)
                {
                    float distanceAlongRay = (index + viewSampleOffset) * stepLength;
                    float3 samplePosition = rayOrigin + rayDirection * distanceAlongRay;
                    float2 localDensity = AtmosphereDensity(samplePosition);
                    viewOpticalDepth += localDensity * stepLength;

                    float lightSampleOffset = frac(
                        viewSampleOffset + index * 0.61803398875
                    );
                    float2 lightOpticalDepth = IntegrateLightOpticalDepth(
                        samplePosition,
                        sunDirection,
                        lightSampleOffset
                    );
                    if (lightOpticalDepth.x < 0.0)
                        continue;

                    float3 extinction =
                        betaRayleigh * (viewOpticalDepth.x + lightOpticalDepth.x)
                        + betaMie * (viewOpticalDepth.y + lightOpticalDepth.y);
                    float3 transmittance = exp(-extinction);
                    rayleighScattering += localDensity.x * transmittance * stepLength;
                    mieScattering += localDensity.y * transmittance * stepLength;
                }

                float scatteringAngle = dot(rayDirection, sunDirection);
                float rayleighPhase = 3.0
                    * (1.0 + scatteringAngle * scatteringAngle)
                    / (16.0 * ATMOSPHERE_PI);
                float g = _MieAnisotropy;
                float mieDenominator = max(
                    1.0 + g * g - 2.0 * g * scatteringAngle,
                    0.0001
                );
                float miePhase = (1.0 - g * g)
                    / (4.0 * ATMOSPHERE_PI * pow(mieDenominator, 1.5));
                float3 radiance = _SunIntensity
                    * (
                        rayleighScattering * betaRayleigh * rayleighPhase
                        + mieScattering * betaMie * miePhase
                    );
                radiance = 1.0 - exp(-radiance * _Exposure);

                float3 viewTransmittance = exp(
                    -(
                        betaRayleigh * viewOpticalDepth.x
                        + betaMie * viewOpticalDepth.y
                    )
                );
                float opacity = saturate(1.0 - dot(viewTransmittance, float3(0.2126, 0.7152, 0.0722)));
                return half4(radiance, opacity);
            }
            ENDHLSL
        }
    }
}
