Shader "Custom/SelectiveOutlineshader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Zwrite Off Cull Off

        Pass
        {
            Name "SelectiveOutline"

            HLSLPROGRAM

            float _Scale;
            float _Threshold;

            #pragma vertex Vert
            #pragma fragment SelectiveOutline

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


            float4 SelectiveOutline (Varyings i) : SV_Target
            {
                // float3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord).rrr;

                /*
                cf. https://roystan.net/articles/outline-shader
                */

                float halfScaleFloor = floor(_Scale * 0.5); // scale increments goes: 0,1,1,2,2,etc.
                float halfScaleCeil = ceil(_Scale * 0.5); // scale increments goes: 1,1,2,2,3,etc.

                float2 texelSize = float2(_BlitTexture_TexelSize.x, _BlitTexture_TexelSize.y);

                float2 bottomLeftUV = i.texcoord - (texelSize * halfScaleFloor); // moves 1px down left every 2 scale increment, start idle
                float2 topLeftUV = i.texcoord + (texelSize * float2(halfScaleFloor, halfScaleCeil)); // alternates 1px up 1px left each scale increment
                float2 topRightUV = i.texcoord + (texelSize * halfScaleCeil); // moves 1px top right every 2 scale increment, start active
                float2 bottomRightUV = i.texcoord + (texelSize * float2(halfScaleCeil, halfScaleFloor)); // alternates 1px left 1px down each scale increment

                float depth0 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, bottomLeftUV).r;
                float depth1 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, topRightUV).r;
                float depth2 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, bottomRightUV).r;
                float depth3 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, topLeftUV).r;

                float depthFiniteDifference0 = depth1 - depth0;
                float depthFiniteDifference1 = depth3 - depth2;
                float edgeDepth = sqrt(pow(depthFiniteDifference0, 2) + pow(depthFiniteDifference1, 2));

                // float c = depthFiniteDifference0;
                float c = step(edgeDepth, _Threshold * depth0);

                // float3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
                // return float4(color, 1);

                return float4(c, c, c, 1);
            }


            ENDHLSL
        }
    }
}