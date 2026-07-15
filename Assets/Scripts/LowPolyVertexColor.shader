// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LowPolyVertexColor.shader — shader URP que LEE los VERTEX-COLORS
//  de la malla (que URP/Lit ignora). Para packs low-poly/PSX que
//  colorean por vértice (ej. StarkCrafts PSX). Iluminado (luz
//  principal + linterna/additional lights + niebla). Cull Off.
// ============================================================
Shader "Folklore/LowPolyVertexColor"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        // --- PSX (0 = apagado). El perro los prende para el look PS1 ---
        _PsxSnap ("PSX Vertex Snap (0=off, ~80=fuerte)", Float) = 0
        _PsxColorLevels ("PSX Color Levels (0=off, ~24)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
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
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _PsxSnap;
                float _PsxColorLevels;
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
                float4 positionCS : SV_POSITION;
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
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = wp;
                OUT.positionCS = TransformWorldToHClip(wp);
                // PSX vertex snapping: la posición salta a una grilla de baja resolución
                // (el "temblor" clásico de PS1). Solo si _PsxSnap > 0.
                if (_PsxSnap > 0.0)
                {
                    float4 c = OUT.positionCS;
                    c.xyz /= c.w;
                    c.xy = round(c.xy * _PsxSnap) / _PsxSnap;
                    c.xyz *= c.w;
                    OUT.positionCS = c;
                }
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * IN.color.rgb; // ← vertex color

                float3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                half3 col = albedo * (mainLight.color * ndl + SampleSH(n));

                #if defined(_ADDITIONAL_LIGHTS)
                    // OJO: el macro LIGHT_LOOP_BEGIN referencia una variable llamada
                    // EXACTAMENTE 'inputData' (Forward+). No renombrar.
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    uint cnt = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(cnt)
                        Light l = GetAdditionalLight(lightIndex, IN.positionWS);
                        half nl = saturate(dot(n, l.direction)) * 0.5 + 0.5;
                        col += albedo * l.color * (l.distanceAttenuation * l.shadowAttenuation) * nl;
                    LIGHT_LOOP_END
                #endif

                col = MixFog(col, IN.fogFactor);
                // PSX: poca profundidad de color (bandas). Solo si _PsxColorLevels > 0.
                if (_PsxColorLevels > 0.0)
                    col = floor(col * _PsxColorLevels) / _PsxColorLevels;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
