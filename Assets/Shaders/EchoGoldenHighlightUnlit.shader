Shader "Echo/GoldenHighlightUnlit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        
        [Header(Golden Highlight)]
        _GoldColor ("Gold Color", Color) = (1.0, 0.84, 0.0, 1.0)
        _GoldIntensity ("Gold Intensity", Range(0, 5)) = 2.0
        _ShimmerSpeed ("Shimmer Speed", Range(0, 10)) = 3.0
        _ShimmerScale ("Shimmer Scale", Range(0.1, 10)) = 2.0
        _ShimmerStrength ("Shimmer Strength", Range(0, 1)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.1, 5)) = 2.0
        _HighlightBlend ("Highlight Blend", Range(0, 1)) = 0.7
        
        [Header(Ambient)]
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Echo data arrays (set from C# via Shader.SetGlobal*)
            #define MAX_ECHOES 24
            int _EchoCount;
            float4 _EchoPositions[MAX_ECHOES];
            float  _EchoRadii[MAX_ECHOES];
            float4 _EchoColors[MAX_ECHOES];

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _GoldColor;
                float _GoldIntensity;
                float _ShimmerSpeed;
                float _ShimmerScale;
                float _ShimmerStrength;
                float _FresnelPower;
                float _HighlightBlend;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            // Compute echo influence at a given world position
            float ComputeEchoInfluence(float3 worldPos)
            {
                float totalInfluence = 0;

                for (int i = 0; i < _EchoCount; i++)
                {
                    float3 echoPos = _EchoPositions[i].xyz;
                    float echoRadius = _EchoRadii[i];
                    float echoFade = _EchoColors[i].a;

                    float dist = distance(worldPos, echoPos);

                    if (dist < echoRadius)
                    {
                        float distFade = 1.0 - saturate(dist / echoRadius);
                        distFade *= distFade;
                        float influence = distFade * echoFade;
                        totalInfluence = max(totalInfluence, influence);
                    }
                }

                return saturate(totalInfluence);
            }

            // Simple hash function for noise
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Value noise
            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash(i + float3(0, 0, 0)), hash(i + float3(1, 0, 0)), f.x),
                         lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                         lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // FBM noise for shimmer
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // Sample base texture
                float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = baseMapColor.rgb * _BaseColor.rgb;

                // Get echo influence
                float echoInfluence = ComputeEchoInfluence(input.positionWS);

                // Calculate fresnel for edge glow
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Calculate shimmer using FBM noise
                float time = _Time.y * _ShimmerSpeed;
                float3 noisePos = input.positionWS * _ShimmerScale + float3(time, time * 0.7, time * 0.3);
                float shimmer = fbm(noisePos);
                shimmer = lerp(1.0, shimmer, _ShimmerStrength);

                // Golden highlight with shimmer
                float3 goldHighlight = _GoldColor.rgb * _GoldIntensity * shimmer;
                goldHighlight *= (1.0 + fresnel * 2.0);

                // Secondary shimmer
                float3 noisePos2 = input.positionWS * _ShimmerScale * 0.5 + float3(time * 1.3, -time * 0.5, time * 0.8);
                float shimmer2 = fbm(noisePos2);
                goldHighlight += _GoldColor.rgb * _GoldIntensity * 0.3 * shimmer2 * fresnel;

                // Base color with ambient
                float3 finalColor = albedo * _AmbientStrength;

                // Blend golden highlight based on echo influence
                float highlightAmount = echoInfluence * _HighlightBlend;
                finalColor = lerp(finalColor, finalColor + goldHighlight, highlightAmount);
                finalColor += goldHighlight * echoInfluence * (1.0 - _HighlightBlend);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            float4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                return float4(normalWS * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
