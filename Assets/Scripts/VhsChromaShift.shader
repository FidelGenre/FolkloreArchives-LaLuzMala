// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  VhsChromaShift.shader — fullscreen VHS effect.
//  Uniform horizontal RGB channel offset across the WHOLE screen
//  (like a mis-aligned VHS tape), NOT the radial lens chromatic
//  aberration URP ships (which only shows at the edges). Plus
//  optional scanlines and a per-line horizontal jitter/wobble.
//  Driven by VhsChromaShiftFeature (a ScriptableRendererFeature).
// ============================================================
Shader "Hidden/Folklore/VhsChromaShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "VhsChromaShift"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Blit.hlsl gives us the fullscreen-triangle Vert/Varyings, _BlitTexture
            // and sampler_LinearClamp used by URP's RenderGraph AddBlitPass.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ChromaOffset;     // horizontal R/B offset, in UV (same everywhere)
            float _ScanlineStrength; // 0 = none
            float _ScanlineCount;    // density of scanlines
            float _Jitter;           // per-scanline horizontal wobble, in UV

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(41.0, 289.0))) * 45758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // per-scanline horizontal jitter (tape wobble), changes over time
                if (_Jitter > 0.0)
                {
                    float lineId = floor(uv.y * 240.0);
                    float t = floor(_Time.y * 24.0);
                    uv.x += (hash(float2(lineId, t)) - 0.5) * _Jitter;
                }

                // UNIFORM chroma shift: same offset over the whole screen
                float2 ro = float2(_ChromaOffset, 0.0);
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + ro).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - ro).b;
                half3 col = half3(r, g, b);

                // scanlines
                if (_ScanlineStrength > 0.0)
                {
                    float s = 0.5 + 0.5 * sin(uv.y * _ScanlineCount);
                    col *= 1.0 - _ScanlineStrength * s;
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
