Shader "Custom/SelectiveOutlineshader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Zwrite Off Cull Off

        Pass
        {
            Name "SelectiveOutline"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;
            float _Thickness;

            float _DepthSampleScale;
            float _DepthThreshold;

            float _NormalsSampleScale;
            float _NormalsThreshold;

            TEXTURE2D(_NormalsTex);
            SAMPLER(sampler_NormalsTex);
            float4 _NormalsTex_ST;
            float2 _NormalsTex_TexelSize;

            TEXTURE2D(_DepthTex);
            SAMPLER(sampler_DepthTex);
            float4 _DepthTex_ST;
            float2 _DepthTex_TexelSize;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            /*
                cf. https://roystan.net/articles/outline-shader/
            */
            float GetDepthEdge (float2 uv, float2 texelSize, float scale)
            {
                float halfScaleFloor = floor(scale * 0.5); // scale increments goes: 0,1,1,2,2,etc.
                float halfScaleCeil = ceil(scale * 0.5); // scale increments goes: 1,1,2,2,3,etc.

                float2 bottomLeftUV = uv - (texelSize * halfScaleFloor); // moves 1px down left every 2 scale increment, start idle
                float2 topLeftUV = uv + (texelSize * float2(halfScaleFloor, halfScaleCeil)); // alternates 1px up 1px left each scale increment
                float2 topRightUV = uv + (texelSize * halfScaleCeil); // moves 1px top right every 2 scale increment, start active
                float2 bottomRightUV = uv + (texelSize * float2(halfScaleCeil, halfScaleFloor)); // alternates 1px left 1px down each scale increment

                float depth = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, uv).r;
                float depth0 = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, bottomLeftUV).r;
                float depth1 = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, topRightUV).r;
                float depth2 = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, topLeftUV).r;
                float depth3 = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, bottomRightUV).r;

                float depthDiff0 = depth1 - depth0;
                float depthDiff1 = depth3 - depth2;

                float depthEdge = sqrt(pow(depthDiff0, 2) + pow(depthDiff1, 2));

                float threshold = _DepthThreshold * depth; // scale threshold with original depth
                return 1 - step(depthEdge, threshold);
            }

            float GetNormalEdge (float2 uv, float2 texelSize, float scale) {
                float halfScaleFloor = floor(scale * 0.5); // scale increments goes: 0,1,1,2,2,etc.
                float halfScaleCeil = ceil(scale * 0.5); // scale increments goes: 1,1,2,2,3,etc.

                float2 bottomLeftUV = uv - (texelSize * halfScaleFloor); // moves 1px down left every 2 scale increment, start idle
                float2 topLeftUV = uv + (texelSize * float2(halfScaleFloor, halfScaleCeil)); // alternates 1px up 1px left each scale increment
                float2 topRightUV = uv + (texelSize * halfScaleCeil); // moves 1px top right every 2 scale increment, start active
                float2 bottomRightUV = uv + (texelSize * float2(halfScaleCeil, halfScaleFloor)); // alternates 1px left 1px down each scale increment

                float3 normal0 = SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, bottomLeftUV).rgb;
                float3 normal1 = SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, topRightUV).rgb;
                float3 normal2 = SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, topLeftUV).rgb;
                float3 normal3 = SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, bottomRightUV).rgb;

                float3 normalDiff0 = normal1 - normal0;
                float3 normalDiff1 = normal3 - normal2;

                float normalEdge = sqrt(dot(normalDiff0, normalDiff0) + dot(normalDiff1, normalDiff1));
                return step(_NormalsThreshold, normalEdge);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                float3 normal = SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, uv);
                float mask = step(0.01, dot(normal, normal));

                float normalEdge = GetNormalEdge(uv, _NormalsTex_TexelSize, _NormalsSampleScale * _Thickness);
                float depthEdge = GetDepthEdge(uv, _DepthTex_TexelSize, _DepthSampleScale * _Thickness) * mask;

                return max(normalEdge, depthEdge) * _Color;
                // note that alpha is ignored here

                // return SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, uv);
                // return SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, uv);
            }
            ENDHLSL
        }

    }
}

