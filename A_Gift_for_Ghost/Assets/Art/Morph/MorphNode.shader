// 变形节点用的 URP 着色器：无光照，按法线做一点固定明暗让方块有体积感。
// 颜色是逐实例属性，配合 Graphics.RenderMeshInstanced 一次画完全部节点。支持 VR 单通道立体渲染。
Shader "Ghost/MorphNode"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        _ShadeStrength ("Shade Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
        UNITY_INSTANCING_BUFFER_END(Props)

        float _ShadeStrength;
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                // 固定方向的半兰伯特，顶面亮、侧面略暗
                float3 lightDir = normalize(float3(-0.4, 0.8, -0.45));
                float shade = dot(normalize(input.normalWS), lightDir) * 0.5 + 0.5;
                color.rgb *= lerp(1.0, shade, _ShadeStrength);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        // 深度预渲染 / 深度纹理需要这个 Pass，否则开启 Depth Priming 时节点会消失
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
