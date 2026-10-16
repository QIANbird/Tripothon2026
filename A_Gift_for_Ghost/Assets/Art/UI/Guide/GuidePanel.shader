// 教学弹窗（TutorialGuidePopup）的底板：无光照、半透明、双面的圆角矩形，不用贴图（UV 上算圆角距离场，边缘抗锯齿）。
// _Aspect = 宽 / 高，_Radius = 圆角半径占高度的比例。支持 VR 单通道立体渲染。
Shader "Ghost/GuidePanel"
{
    Properties
    {
        _Color ("Color", Color) = (0.851, 0.851, 0.851, 0.85)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Aspect ("Aspect (width / height)", Float) = 1
        _Radius ("Corner Radius (fraction of height)", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Alpha;
                float _Aspect;
                float _Radius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 以高度为 1 的坐标系：x ∈ [-aspect/2, aspect/2]，y ∈ [-1/2, 1/2]
                float aspect = max(_Aspect, 1e-3);
                float2 p = (input.uv - 0.5) * float2(aspect, 1.0);
                float r = clamp(_Radius, 0.0, 0.5);
                float2 q = abs(p) - (float2(aspect, 1.0) * 0.5 - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float aa = max(fwidth(d), 1e-5);
                half4 c = _Color;
                c.a *= saturate(0.5 - d / aa) * _Alpha;
                return c;
            }
            ENDHLSL
        }
    }
}
