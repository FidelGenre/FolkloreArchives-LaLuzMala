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
            float _PosterizeLevels;  // PSX: niveles de color por canal (ej. 32). <=1 = off
            float _DitherStrength;   // PSX: fuerza del dither Bayer 4x4 (0 = off)

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(41.0, 289.0))) * 45758.5453);
            }

            // matriz Bayer 4x4 para el dither ordenado (patrón de puntitos PSX)
            static const float _BayerP[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

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

                // PSX: DITHER Bayer + POSTERIZAR (bandas de color retro PS1/PS2).
                // Se hace en espacio PERCEPTUAL (sqrt) para que los oscuros tengan más
                // niveles y NO se aplasten a negro (de noche si no, no se ve nada).
                if (_PosterizeLevels > 1.5)
                {
                    int2 pix = int2(fmod(input.positionCS.xy, 4.0)); // coord de pixel en pantalla
                    float d = _BayerP[pix.y * 4 + pix.x] - 0.5;      // -0.5 .. +0.5
                    half3 p = sqrt(saturate(col));
                    p = saturate(p + d * (_DitherStrength / _PosterizeLevels));
                    p = floor(p * _PosterizeLevels + 0.5) / _PosterizeLevels;
                    col = p * p;
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
