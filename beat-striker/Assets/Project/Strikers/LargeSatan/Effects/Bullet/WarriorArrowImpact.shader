Shader "BeatStriker/WarriorArrowImpact" {
    Properties {
        [HDR] _Color ("Emission", Color) = (5, 0.05, 0.1, 1)
        _Style ("Glow 0 / ring 1 / shard 2", Float) = 0
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Style;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes v) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2 - 1;
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target {
                float r = length(i.uv);
                float glow = pow(saturate(1 - r), 2.5);
                float ring = exp(-abs(r - 0.7) * 42) * saturate(1 - r);
                float shard = pow(saturate(1 - abs(i.uv.x)), 3) * saturate(1 - abs(i.uv.y));
                float alpha = _Style < 0.5 ? glow : (_Style < 1.5 ? ring * 4 : shard);
                return half4(_Color.rgb * i.color.rgb, alpha * i.color.a * _Color.a);
            }
            ENDHLSL
        }
    }
}
