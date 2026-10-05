// 写实模型用的 URP 着色器：简单光照（主光源兰伯特 + 环境光），带"从下往上、按方块噪声"的抖动溶解显现。
// 溶解用 clip，物体始终不透明，没有透明排序问题，Quest 上也便宜。支持 VR 单通道立体渲染。
// _Reveal 0 = 完全隐藏，1 = 完全显现；高度和过渡带与 NodeMorpher 的节点缩小一致，模型出现的地方节点同时消失。
Shader "Ghost/RevealLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Reveal ("Reveal", Range(0, 1)) = 1
        _RevealBottom ("Reveal Bottom (world Y)", Float) = 0
        _RevealHeight ("Reveal Height", Float) = 1
        _RevealBand ("Reveal Band", Range(0.01, 1)) = 0.15
        _NoiseScale ("Noise Cell Size (m)", Float) = 0.015
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Reveal;
            float _RevealBottom;
            float _RevealHeight;
            float _RevealBand;
            float _NoiseScale;
        CBUFFER_END

        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        // 高度 h 处在 _Reveal * (1 + band) 越过 h 时开始出现，越过 h + band 时完全出现；噪声按世界空间方块取值
        void RevealClip(float3 positionWS)
        {
            float h = saturate((positionWS.y - _RevealBottom) / max(_RevealHeight, 1e-4));
            float n = Hash13(floor(positionWS / max(_NoiseScale, 1e-4)));
            clip(_Reveal * (1.0 + _RevealBand) - h - _RevealBand * n - 1e-4);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                RevealClip(input.positionWS);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // 双面渲染时背面翻转法线，叶片背面也能正确受光
                float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                Light light = GetMainLight();
                half3 lighting = SampleSH(normalWS) + light.color * saturate(dot(normalWS, light.direction));
                return half4(albedo.rgb * lighting, 1.0);
            }
            ENDHLSL
        }

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
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                RevealClip(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
