// Contorno tipo "casco invertido" para las ratas: expande el mesh a lo largo de sus normales y
// CULLEA las caras de adelante (Cull Front) -> solo se ven las caras traseras, que quedan como un
// borde de color alrededor del modelo. Unlit, URP.
Shader "Custom/RatOutline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.15, 1)
        _Width ("Width", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            float4 _Color;
            float  _Width;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 p = IN.positionOS.xyz + normalize(IN.normalOS) * _Width;
                OUT.positionHCS = TransformObjectToHClip(p);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
