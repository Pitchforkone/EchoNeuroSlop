Shader "Echo/EdgeDetection"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "EchoEdgeDetection"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            // Screen texture (scene color)
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            // Echo data arrays (set from C# via Shader.SetGlobal*)
            #define MAX_ECHOES 24
            int _EchoCount;
            float4 _EchoPositions[MAX_ECHOES]; // xyz = world position
            float  _EchoRadii[MAX_ECHOES];     // current radius
            float4 _EchoColors[MAX_ECHOES];    // rgb = color, a = fade (0..1)

            // Tuning
            float _EdgeThickness;       // texel offset multiplier (default ~1.0)
            float _DepthThreshold;      // depth edge sensitivity (default ~0.1)
            float _NormalThreshold;     // normal edge sensitivity (default ~0.4)
            float _EdgeIntensity;       // overall brightness of edges (default ~2.0)

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Fullscreen triangle trick
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            // Reconstruct world position from depth
            float3 ReconstructWorldPos(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                // NDC: x,y from UV, z from depth
                float4 ndc = float4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y = -ndc.y;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                return worldPos.xyz / worldPos.w;
            }

            // Sample depth at offset (in texels)
            float SampleDepthAt(float2 uv, float2 offset)
            {
                float2 texelSize = _ScreenParams.zw - 1.0; // 1/width, 1/height — but use _ScreenSize
                float2 sampleUV = uv + offset * (_ScreenParams.zw - float2(1,1));
                // Correct: _ScreenParams.z = 1+1/width, _ScreenParams.w = 1+1/height
                // So texel size = 1/_ScreenParams.x, 1/_ScreenParams.y
                return SampleSceneDepth(uv + offset * float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y));
            }

            // Sample normals at offset (in texels)
            float3 SampleNormalAt(float2 uv, float2 offset)
            {
                float2 sampleUV = uv + offset * float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);
                return SampleSceneNormals(sampleUV);
            }

            // Sobel edge detection on depth
            float SobelDepth(float2 uv, float thickness)
            {
                // 3x3 Sobel kernel
                float d00 = SampleDepthAt(uv, float2(-thickness, -thickness));
                float d10 = SampleDepthAt(uv, float2( 0,         -thickness));
                float d20 = SampleDepthAt(uv, float2( thickness, -thickness));
                float d01 = SampleDepthAt(uv, float2(-thickness,  0));
                float d21 = SampleDepthAt(uv, float2( thickness,  0));
                float d02 = SampleDepthAt(uv, float2(-thickness,  thickness));
                float d12 = SampleDepthAt(uv, float2( 0,          thickness));
                float d22 = SampleDepthAt(uv, float2( thickness,  thickness));

                float sobelX = d00 + 2.0 * d01 + d02 - d20 - 2.0 * d21 - d22;
                float sobelY = d00 + 2.0 * d10 + d20 - d02 - 2.0 * d12 - d22;

                return sqrt(sobelX * sobelX + sobelY * sobelY);
            }

            // Edge detection on normals (difference from center)
            float NormalEdge(float2 uv, float thickness)
            {
                float3 nc = SampleNormalAt(uv, float2(0, 0));
                float3 n0 = SampleNormalAt(uv, float2( thickness, 0));
                float3 n1 = SampleNormalAt(uv, float2(-thickness, 0));
                float3 n2 = SampleNormalAt(uv, float2(0,  thickness));
                float3 n3 = SampleNormalAt(uv, float2(0, -thickness));

                float edge = 0;
                edge += 1.0 - saturate(dot(nc, n0));
                edge += 1.0 - saturate(dot(nc, n1));
                edge += 1.0 - saturate(dot(nc, n2));
                edge += 1.0 - saturate(dot(nc, n3));

                return edge * 0.25;
            }

            // Compute echo influence at a given world position
            float4 ComputeEchoInfluence(float3 worldPos)
            {
                float4 result = float4(0, 0, 0, 0); // rgb = accumulated color, a = max influence

                for (int i = 0; i < _EchoCount; i++)
                {
                    float3 echoPos = _EchoPositions[i].xyz;
                    float echoRadius = _EchoRadii[i];
                    float3 echoColor = _EchoColors[i].rgb;
                    float echoFade = _EchoColors[i].a;

                    float dist = distance(worldPos, echoPos);

                    // Inside the echo radius?
                    if (dist < echoRadius)
                    {
                        // Fade based on distance from center (closer = brighter)
                        float distFade = 1.0 - saturate(dist / echoRadius);
                        distFade *= distFade; // quadratic falloff

                        float influence = distFade * echoFade;

                        result.rgb += echoColor * influence;
                        result.a = max(result.a, influence);
                    }
                }

                return result;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Scene color (with Point Light contribution)
                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);

                // Reconstruct world position for this pixel
                float3 worldPos = ReconstructWorldPos(uv);

                // Check if any echo pulse covers this pixel
                float4 echoInfluence = ComputeEchoInfluence(worldPos);

                // No echo influence — return scene as-is
                if (echoInfluence.a < 0.001)
                    return sceneColor;

                // Compute edges
                float thickness = max(1.0, _EdgeThickness);
                float depthEdge = SobelDepth(uv, thickness);
                float normalEdge = NormalEdge(uv, thickness);

                // Combine: depth edges for large discontinuities, normal edges for surface angles
                float depthContrib = step(_DepthThreshold, depthEdge);
                float normalContrib = step(_NormalThreshold, normalEdge);
                float edge = saturate(depthContrib + normalContrib);

                // Edge color = echo color * edge strength * intensity
                float3 edgeColor = echoInfluence.rgb * edge * _EdgeIntensity * echoInfluence.a;

                // Additive blend: scene + edge glow
                return float4(sceneColor.rgb + edgeColor, 1.0);
            }
            ENDHLSL
        }
    }
}
