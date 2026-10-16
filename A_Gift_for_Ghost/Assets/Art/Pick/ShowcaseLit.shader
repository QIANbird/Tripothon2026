// 结束界面彩椒展示用的着色器：自带一盏固定的主光（兰伯特 + 环境光），不受场景灯光影响，没有溶解。
// VR 里彩椒摆在结束界面面板前，面板和黑幕是 ZTest Always 的透明 UI，后面还可能有更近的植株：
//   Queue = Overlay，画在所有 UI 之后；
//   第一个 Pass 把彩椒覆盖到的像素深度清成最远（ZTest Always + SV_Depth），第二个 Pass 正常深度测试画颜色，
//   彩椒不被场景挡住，自身前后遮挡仍然正确。PC 的专用相机里同样适用。支持 VR 单通道立体渲染。
Shader "Ghost/ShowcaseLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.42, 0.42, 0.45, 1)
        _KeyColor ("Key Light Color", Color) = (0.85, 0.83, 0.8, 1)
        _KeyDir ("Key Light Direction (view: x right, y up, z toward viewer)", Vector) = (-0.5, 0.6, 0.65, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _Ambient;
            half4 _KeyColor;
            float4 _KeyDir;
        CBUFFER_END
        ENDHLSL

        // 先清深度：URP 先画 SRPDefaultUnlit 再画 UniversalForward
        Pass
        {
            Name "DepthPunch"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Always
            ZWrite On
            ColorMask 0

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

            half4 frag(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                #if UNITY_REVERSED_Z
                depth = 0.0;
                #else
                depth = 1.0;
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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
                float3 normalVS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 光照在观察空间算：主光固定在观察者左上前方，彩椒怎么转、VR 里从哪边看，亮面都朝着玩家
                output.normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalVS = normalize(input.normalVS) * (isFrontFace ? 1.0 : -1.0);
                // 观察空间 z 轴指向观察者
                half3 lighting = _Ambient.rgb + _KeyColor.rgb * saturate(dot(normalVS, normalize(_KeyDir.xyz)));
                return half4(albedo.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
