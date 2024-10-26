Shader "Custom/ViewSpaceNormals"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Zwrite Off Cull Off

        Pass
        {
            Name "ViewSpaceNormals"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment ViewSpaceNormals

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


            float4 ViewSpaceNormals (Varyings i) : SV_Target
            {
                // float3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord).rgb;
                float3 color = SAMPLE_TEXTURE2D(_MainTexture, sampler_LinearClamp, i.texcoord).rgb;

                return float4(color, 1);
            }


            ENDHLSL
        }
    }
}