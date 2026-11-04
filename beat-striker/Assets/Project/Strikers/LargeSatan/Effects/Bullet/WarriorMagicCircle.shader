Shader "BeatStriker/WarriorMagicCircle" {
    Properties {
        [HDR] _Color ("Circle emission", Color) = (5, 0.025, 0.08, 1)
        _RotationSpeed ("Rotation speed", Float) = 0.3
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _RotationSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }
            float stroke(float distance, float width, float aa) {
                return 1.0 - smoothstep(width, width + aa, abs(distance));
            }
            float segmentDistance(float2 p, float2 a, float2 b) {
                float2 edge = b - a;
                return length(p - a - edge * saturate(dot(p - a, edge) / dot(edge, edge)));
            }
            float2 foldSector(float radius, float angle, float count) {
                float width = 6.2831853 / count;
                float folded = angle - width * floor(angle / width + 0.5);
                return radius * float2(cos(folded), sin(folded));
            }
            half4 frag(Varyings i) : SV_Target {
                float2 p = i.uv;
                float radius = length(p);
                float aa = max(fwidth(radius), 0.002);
                float angle = atan2(p.y, p.x);
                float phase = _Time.y * _RotationSpeed;
                float2 blade = foldSector(radius, angle + phase, 8.0);
                float bladeDistance = min(segmentDistance(blade, float2(0.28, 0), float2(0.67, 0.085)),
                    segmentDistance(blade, float2(0.67, 0.085), float2(0.59, 0)));
                bladeDistance = min(bladeDistance, segmentDistance(blade, float2(0.59, 0), float2(0.67, -0.085)));
                bladeDistance = min(bladeDistance, segmentDistance(blade, float2(0.67, -0.085), float2(0.28, 0)));
                float blades = stroke(bladeDistance, 0.007, aa);
                float polygon = stroke(blade.x - 0.705, 0.006, aa);
                float rim = stroke(radius - 0.93, 0.011, aa) + stroke(radius - 0.895, 0.004, aa);
                float2 rune = foldSector(radius, angle - phase * 0.65, 16.0);
                float runeDistance = min(segmentDistance(rune, float2(0.77, -0.045), float2(0.855, -0.045)),
                    segmentDistance(rune, float2(0.78, 0.035), float2(0.85, 0.035)));
                runeDistance = min(runeDistance, segmentDistance(rune, float2(0.77, -0.045), float2(0.85, 0.035)));
                runeDistance = min(runeDistance, segmentDistance(rune, float2(0.81, -0.015), float2(0.81, 0.06)));
                float runes = stroke(runeDistance, 0.005, aa);
                float c = cos(-phase), s = sin(-phase);
                float2 seal = mul(float2x2(c, -s, s, c), p);
                float diamond = stroke(abs(seal.x) + abs(seal.y) - 0.27, 0.007, aa);
                float needle = stroke(segmentDistance(seal, float2(0, -0.21), float2(0, 0.21)), 0.007, aa);
                float wings = stroke(segmentDistance(float2(abs(seal.x), seal.y), float2(0, 0.19), float2(0.09, 0.04)), 0.007, aa);
                float halo = exp(-abs(radius - 0.91) * 40.0) * 0.11;
                float intensity = saturate(blades + polygon + rim + runes + diamond + needle + wings) + halo;
                return half4(_Color.rgb, intensity * _Color.a);
            }
            ENDHLSL
        }
    }
}
