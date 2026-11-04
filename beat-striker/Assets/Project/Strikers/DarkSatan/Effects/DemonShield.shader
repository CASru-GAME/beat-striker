Shader "BeatStriker/DemonShield" {
    Properties {
        [HDR] _ShieldColor ("Shield color", Color) = (0.55, 0.22, 2.0, 1)
        _CenterOpacity ("Center opacity", Range(0,1)) = 0.10
        _RimOpacity ("Rim opacity", Range(0,1)) = 0.8
        _RimPower ("Rim power", Range(0.5,8)) = 2.2
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
            float4 _ShieldColor;
            float _CenterOpacity, _RimOpacity, _RimPower;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
            Varyings vert(Attributes v) {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target {
                float rim = pow(1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), _RimPower);
                return half4(_ShieldColor.rgb * (0.45 + rim * 1.7), saturate(_CenterOpacity + rim * _RimOpacity));
            }
            ENDHLSL
        }
    }
}
