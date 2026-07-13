// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TreeWind.shader — copas de árbol (hojas/agujas) con VIENTO:
//  balanceo de vértices por _Time (más arriba, más se mueve),
//  alpha cutout doble cara, e iluminación URP (luna + linterna).
//  Es la versión de GrassFade SIN el fade de distancia del pasto
//  (si no, los árboles se desvanecerían al alejarse).
// ============================================================
Shader "Folklore/TreeWind"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        _WindStrength ("Wind Strength", Float) = 0.55
        _WindSpeed ("Wind Speed", Float) = 1.0
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // GLOBAL (lo sube la Luz Mala al atacar): 1 = viento normal, >1 = tormenta.
            float _TreeWindGust;

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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                // viento: la copa se balancea más arriba (uv.y ~ altura), con fase por
                // posición mundial → cada árbol se mueve distinto. Dos ejes + una racha lenta.
                float h = saturate(IN.uv.y);
                float phase = posWS.x * 0.15 + posWS.z * 0.15;
                float gust = 0.6 + 0.4 * sin(_Time.y * 0.35 + phase);          // rachas lentas
                float g = max(1.0, _TreeWindGust);   // 1 normal; sube MUCHO cuando ataca la Luz Mala
                float amp = _WindStrength * h * gust * g;
                float swayX = sin(_Time.y * _WindSpeed + phase) * amp;
                float swayZ = cos(_Time.y * _WindSpeed * 0.8 + phase * 1.3) * amp * 0.6;
                // ráfaga RÁPIDA y exagerada extra solo durante el ataque (g>1)
                float fastAmp = _WindStrength * h * (g - 1.0) * 0.6;
                swayX += sin(_Time.y * _WindSpeed * 3.5 + phase * 2.0) * fastAmp;
                swayZ += cos(_Time.y * _WindSpeed * 4.2 + phase * 2.4) * fastAmp * 0.7;
                posWS.x += swayX;
                posWS.z += swayZ;

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

                float3 n = normalize(IN.normalWS);

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                half3 col = albedo.rgb * (mainLight.color * ndl + SampleSH(n));

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

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
