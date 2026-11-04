Shader "BeatStriker/DemonMeleeEnergy" {
    Properties {
        [HDR] _RimColor ("Rim emission", Color) = (5, 0.6, 12, 1)
        _CoreColor ("Dark core", Color) = (0.015, 0.002, 0.04, 1)
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _RimColor;
            half4 _CoreColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };
            Varyings vert(Attributes v) {
                Varyings o;
                float pulse = sin(_Time.y * 34 + v.positionOS.y * 25) * 0.025;
                v.positionOS.xyz += v.normalOS * pulse;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i) : SV_Target {
                float rim = pow(1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), 2.2);
                float veins = pow(saturate(sin(i.positionOS.y * 38 + sin(i.positionOS.x * 20 + _Time.y * 17) * 2 + _Time.y * 26)), 18);
                return half4(_CoreColor.rgb + _RimColor.rgb * (rim + veins * 0.55), 0.78 + rim * 0.22);
            }
            ENDHLSL
        }
    }
}
