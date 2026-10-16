// 节点悬停描边：把网格外扩一圈、只画背面，纯色无光照。物体本身挡住中间，只露出外圈，看起来是一圈实体描边。
// 两种外扩方式：节点（平面着色、法线不连续）由 NodeMorpher.DrawOutline 按轴放大，_Extrude = 0；
// 虫子模型等平滑网格沿法线外扩 _Extrude 米（世界空间）。支持 VR 单通道立体渲染。
Shader "Ghost/MorphOutline"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.898, 1, 0.443, 1)
        _Extrude ("Normal Extrude (m)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Front

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Extrude;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
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
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            positionWS += SafeNormalize(TransformObjectToWorldNormal(input.normalOS)) * _Extrude;
            output.positionCS = TransformWorldToHClip(positionWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // 开启 Depth Priming 时没有这个 Pass 会被丢掉（同 MorphNode）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            half fragDepth(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
