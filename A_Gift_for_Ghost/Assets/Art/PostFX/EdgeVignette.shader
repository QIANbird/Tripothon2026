// 流动压暗边：全屏后处理，由 URP Full Screen Pass Renderer Feature 调用（docs/tasks/screen-edge-vignette.md）
// 屏幕四周一圈不规则、缓慢蠕动的边；亮处压暗、暗处微微发白。支持 VR 单通道立体渲染（每只眼各自算遮罩）
Shader "Ghost/EdgeVignette"
{
    Properties
    {
        [Header(Shape)]
        _Width ("Edge Width", Range(0.05, 1.5)) = 0.55
        _Falloff ("Falloff", Range(0.3, 4)) = 1.6
        _Corner ("Corner Roundness", Range(0.01, 0.6)) = 0.25

        [Header(Flow)]
        _NoiseScale ("Noise Scale", Range(0.5, 12)) = 3
        _NoiseAmount ("Noise Amount", Range(0, 0.5)) = 0.12
        _FlowSpeed ("Flow Speed", Range(0, 1)) = 0.15

        [Header(Color)]
        _DarkColor ("Dark Color (on bright)", Color) = (0.05, 0.05, 0.05, 1)
        _DarkIntensity ("Dark Intensity", Range(0, 1)) = 0.85
        _LightColor ("Light Color (on dark)", Color) = (0.85, 0.85, 0.85, 1)
        _LightIntensity ("Light Intensity", Range(0, 1)) = 0.25
        _Pivot ("Brightness Pivot", Range(0, 1)) = 0.5
        _PivotSoftness ("Pivot Softness", Range(0.01, 0.5)) = 0.2

        [Header(Blur)]
        _BlurAmount ("Blur Amount", Range(0, 0.03)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "EdgeVignette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 提供 Vert、Varyings、_BlitTexture（Full Screen Pass 把当前画面绑到这里）
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Width, _Falloff, _Corner;
                float _NoiseScale, _NoiseAmount, _FlowSpeed;
                float4 _DarkColor, _LightColor;
                float _DarkIntensity, _LightIntensity, _Pivot, _PivotSoftness;
                float _BlurAmount;
            CBUFFER_END

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float3 SampleColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            // 感知亮度（0 黑 – 1 白）。线性色彩空间下先转回近似 sRGB，让 _Pivot = 0.5 对应视觉上的中灰
            float PerceivedLuma(float3 c)
            {
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                #if !defined(UNITY_COLORSPACE_GAMMA)
                l = sqrt(saturate(l));
                #endif
                return l;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 src = SampleColor(uv);

                // 到最近屏幕边的距离，以半屏高为单位（宽高比修正后，暗边在任何比例下都贴着四条边）
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 q = abs(uv - 0.5) * 2.0;
                float dx = (1.0 - q.x) * aspect;
                float dy = 1.0 - q.y;
                // smooth min：角落处两条边平滑合成圆角
                float k = _Corner;
                float edgeDist = -k * log(exp(-dx / k) + exp(-dy / k));

                // 流动：两层反向漂移的噪声扰动边界，边缘不规则地蠕动而不是整体呼吸
                float2 pos = float2((uv.x - 0.5) * aspect, uv.y - 0.5) * _NoiseScale;
                float t = _Time.y * _FlowSpeed;
                float n = ValueNoise(pos + float2(t, t * 0.6)) * 0.65
                        + ValueNoise(pos * 2.3 + float2(-t * 1.3, t * 0.9)) * 0.35;
                edgeDist += (n - 0.5) * 2.0 * _NoiseAmount;

                float mask = saturate(1.0 - edgeDist / _Width);
                mask = pow(smoothstep(0.0, 1.0, mask), _Falloff);

                // 中心区直接输出原画面
                UNITY_BRANCH
                if (mask <= 0.001)
                    return half4(src, 1);

                // 边缘虚化：按遮罩强度在一圈 8 个点上取样
                float3 col = src;
                UNITY_BRANCH
                if (_BlurAmount > 0.0)
                {
                    float r = _BlurAmount * mask;
                    float2 o = float2(r / aspect, r);
                    float3 sum = src;
                    sum += SampleColor(uv + o * float2( 1,  0));
                    sum += SampleColor(uv + o * float2(-1,  0));
                    sum += SampleColor(uv + o * float2( 0,  1));
                    sum += SampleColor(uv + o * float2( 0, -1));
                    sum += SampleColor(uv + o * float2( 0.7,  0.7));
                    sum += SampleColor(uv + o * float2(-0.7,  0.7));
                    sum += SampleColor(uv + o * float2( 0.7, -0.7));
                    sum += SampleColor(uv + o * float2(-0.7, -0.7));
                    col = sum / 9.0;
                }

                // 背景亮度用一小片区域的平均值判断，避免细节处黑白翻转闪烁
                float2 lo = float2(0.03 / aspect, 0.03);
                float luma = (PerceivedLuma(col)
                            + PerceivedLuma(SampleColor(uv + lo * float2( 1,  1)))
                            + PerceivedLuma(SampleColor(uv + lo * float2(-1,  1)))
                            + PerceivedLuma(SampleColor(uv + lo * float2( 1, -1)))
                            + PerceivedLuma(SampleColor(uv + lo * float2(-1, -1)))) * 0.2;

                // bright：0 = 暗背景（往白推），1 = 亮背景（往黑压）
                float bright = smoothstep(_Pivot - _PivotSoftness, _Pivot + _PivotSoftness, luma);
                float3 target = lerp(_LightColor.rgb, _DarkColor.rgb, bright);
                float strength = lerp(_LightIntensity, _DarkIntensity, bright);

                // 在感知（sRGB）空间里混合，否则线性空间下压暗看起来只到中灰
                float a = mask * strength;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                float3 outCol = SRGBToLinear(lerp(LinearToSRGB(col), LinearToSRGB(target), a));
                #else
                float3 outCol = lerp(col, target, a);
                #endif
                return half4(outCol, 1);
            }
            ENDHLSL
        }
    }
}
