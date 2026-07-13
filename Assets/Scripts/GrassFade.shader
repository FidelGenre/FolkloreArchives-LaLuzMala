// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  GrassFade.shader — URP terrain-detail grass with a DISTANCE
//  DITHER FADE near the cull edge, so grass fades in through a
//  screen-door dither instead of popping in when it crosses the
//  detailObjectDistance boundary. Same cull distance = same FPS,
//  just no hard pop. Also does a light wind sway + alpha cutout.
//
//  Lighting: main light + AMBIENT + ADDITIONAL LIGHTS (Forward+
//  cluster loop) so the flashlight - which is a spot/additional
//  light - actually illuminates the grass at night (otherwise it
//  reads as black, since the moon is nearly off).
//
//  Fade range is driven by two GLOBAL floats (set from script so
//  they can follow day/night detail distance without touching the
//  material): _GrassFadeStart / _GrassFadeEnd (metres from camera).
//  If _GrassFadeEnd <= 0 the fade is disabled (grass fully opaque).
// ============================================================
Shader "Folklore/GrassFade"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindSpeed ("Wind Speed", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            // lighting keywords so additional lights (the flashlight) reach the grass
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // GLOBALS (set via Shader.SetGlobalFloat) - shared across all grass
            // materials so the fade follows the current detailObjectDistance.
            float _GrassFadeStart;
            float _GrassFadeEnd;
            float _TreeWindGust;   // lo sube la Luz Mala al atacar: 1 normal, >1 tormenta
            // per-mode colour multiplier (day sets a warm, dark, burnt tint so the sun
            // doesn't wash the grass out and it reads drier). All-zero = treated as
            // white (no change).
            half4 _GrassTintMul;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                float _WindStrength;
                float _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ordered 4x4 Bayer matrix for the screen-door dither
            static const float _Bayer[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                // wind: sway the top of the blade (uv.y ~ height), phased by world pos.
                // _TreeWindGust (global) lo sube la Luz Mala al atacar → tormenta.
                float h = saturate(IN.uv.y);
                float g = max(1.0, _TreeWindGust);
                float phase = posWS.x * 0.25 + posWS.z * 0.25;
                float amp = _WindStrength * h * g;
                posWS.x += sin(_Time.y * _WindSpeed + phase) * amp;
                posWS.z += cos(_Time.y * _WindSpeed * 0.8 + posWS.z * 0.2) * amp * 0.5;
                // ráfaga rápida extra durante el ataque (g>1)
                float fastAmp = _WindStrength * h * (g - 1.0) * 0.6;
                posWS.x += sin(_Time.y * _WindSpeed * 3.5 + phase * 2.0) * fastAmp;

                OUT.positionWS = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = tex * _BaseColor * IN.color;
                clip(albedo.a - _Cutoff);

                // DISTANCE DITHER FADE: 1 near the camera, 0 at/after _GrassFadeEnd.
                // Screen-door dither so it fades gradually per-pixel instead of popping.
                if (_GrassFadeEnd > 0.0)
                {
                    float dist = distance(IN.positionWS, _WorldSpaceCameraPos);
                    float fade = saturate((_GrassFadeEnd - dist) / max(0.001, _GrassFadeEnd - _GrassFadeStart));
                    int2 pix = int2(fmod(IN.positionHCS.xy, 4.0)); // SV_POSITION.xy = pixel coords in frag
                    clip(fade - _Bayer[pix.y * 4 + pix.x]);
                }

                float3 n = normalize(IN.normalWS);

                // main light (moon) + ambient - soft wrapped diffuse so grass never
                // reads pure black in the lit areas
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                half3 col = albedo.rgb * (mainLight.color * ndl + SampleSH(n));

                // ADDITIONAL LIGHTS (the flashlight) - Forward+ cluster loop
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                        half ndl2 = saturate(dot(n, light.direction)) * 0.5 + 0.5;
                        col += albedo.rgb * light.color * (light.distanceAttenuation * light.shadowAttenuation) * ndl2;
                    LIGHT_LOOP_END
                #endif

                // per-mode tint (día = oscuro/quemado; noche = blanco)
                half3 tint = (_GrassTintMul.r + _GrassTintMul.g + _GrassTintMul.b > 0.0) ? _GrassTintMul.rgb : half3(1.0, 1.0, 1.0);
                col *= tint;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
