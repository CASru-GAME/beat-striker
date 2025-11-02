Shader "Custom/RainbowGradient" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 2)) = 1.0
        _Speed ("Speed", Range(0, 5)) = 1.0
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _HueOffset ("Hue Offset", Range(0, 1)) = 0.0
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Intensity;
            float _Speed;
            float _Saturation;
            float _HueOffset;
            
            // HSV to RGB変換
            float3 hsv2rgb(float3 c) {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                // UVのX座標（0-1）を使ってグラデーションを生成
                float gradient = i.uv.x;
                
                // 時間を使ってアニメーション
                float timeOffset = _Time.y * _Speed;
                
                // 虹色の色相（0-1）を生成（色相オフセットを追加）
                float hue = fmod(gradient + timeOffset + _HueOffset, 1.0);
                
                // HSV色空間で色を生成（SaturationとValueを調整）
                float3 hsv = float3(hue, _Saturation, _Intensity);
                float3 rgb = hsv2rgb(hsv);
                
                // アルファチャンネル（エッジでフェードアウト）
                float alpha = 1.0;
                // 左右の端でフェードアウト
                alpha *= smoothstep(0.0, 0.1, i.uv.x) * smoothstep(1.0, 0.9, i.uv.x);
                // 上下の端でも少しフェードアウト
                alpha *= smoothstep(0.0, 0.2, i.uv.y) * smoothstep(1.0, 0.8, i.uv.y);
                
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}

