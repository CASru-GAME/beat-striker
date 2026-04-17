Shader "Custom/GridLightEffect"
{
    Properties
    {
        _BaseColor ("Base Color (床の色)", Color) = (0.5, 0.5, 0.55, 1)
        _LineColor ("Line Color (線の色)", Color) = (0.1, 0.1, 0.1, 1)
        _GridSize ("Grid Size (網目の細かさ)", Float) = 15.0
        _LineThickness ("Line Thickness (線の太さ)", Range(0.01, 0.2)) = 0.05
        
        _LightColor ("Light Color (光の色)", Color) = (0.8, 0.9, 1.0, 1)
        _LightRadius ("Light Radius (光の大きさ)", Float) = 0.3
        _LightIntensity ("Light Intensity (光の強さ)", Float) = 1.5
        _Speed ("Move Speed (動く速さ)", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BaseColor;
            float4 _LineColor;
            float _GridSize;
            float _LineThickness;
            
            float4 _LightColor;
            float _LightRadius;
            float _LightIntensity;
            float _Speed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // --- グリッド（網目）を描画する計算 ---
                float2 gridUV = frac(i.uv * _GridSize);
                float lineX = step(gridUV.x, _LineThickness) + step(1.0 - _LineThickness, gridUV.x);
                float lineY = step(gridUV.y, _LineThickness) + step(1.0 - _LineThickness, gridUV.y);
                float isLine = saturate(lineX + lineY);
                float3 col = lerp(_BaseColor.rgb, _LineColor.rgb, isLine);

                // --- 動く光を描画する計算 ---
                float time = _Time.y * _Speed;
                // 円を描くように光の座標を動かす
                float2 lightPos = float2(sin(time) * 0.25 + 0.5, cos(time) * 0.25 + 0.5); 
                
                // 光の中心からの距離を測る
                float dist = distance(i.uv, lightPos);
                // 距離に応じて光をグラデーションで減衰させる
                float light = smoothstep(_LightRadius, 0.0, dist);
                
                // 床の色に光を加算する
                col += _LightColor.rgb * light * _LightIntensity;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
