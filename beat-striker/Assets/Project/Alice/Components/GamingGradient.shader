Shader "UI/GamingGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Speed ("Speed", Float) = 1.0
        _GradientWidth ("Gradient Width", Range(0.1, 5)) = 0.5
        _HueStart ("Hue Start", Range(0, 1)) = 0.0
        _HueRange ("Hue Range", Range(0, 1)) = 1.0
        _Saturation ("Saturation", Range(0,1)) = 0.9
        _Brightness ("Brightness", Range(0,2)) = 1.0
        _NeonIntensity ("Neon Intensity", Range(1,3)) = 2.0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float _Speed;
            float _GradientWidth;
            float _HueStart;
            float _HueRange;
            float _Saturation;
            float _Brightness;
            float _NeonIntensity;
            
            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            
            // HSVからRGBへの変換
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // テクスチャサンプリング
                fixed4 col = tex2D(_MainTex, i.texcoord);
                
                // 時間で色相を流す + 横位置でグラデーション
                float timeOffset = _Time.y * _Speed;
                float positionGradient = i.texcoord.x * _GradientWidth;
                float t = frac(timeOffset + positionGradient);
                
                // 色相の範囲を指定の範囲内に制限
                float hue = _HueStart + t * _HueRange;
                hue = frac(hue); // 0～1の範囲にループ
                
                float3 rgb = hsv2rgb(float3(hue, _Saturation, _Brightness));
                
                // ネオン効果
                rgb *= _NeonIntensity;
                
                // 元の色を完全に置き換え、アルファだけ保持
                col.rgb = rgb * _Color.rgb;
                col.a *= i.color.a;
                
                return col;
            }
            ENDCG
        }
    }
}
