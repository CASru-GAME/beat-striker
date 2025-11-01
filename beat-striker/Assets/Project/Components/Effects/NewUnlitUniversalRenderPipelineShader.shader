Shader "Custom/URP_FresnelGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.3, 0.8, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 3
        _Emission ("Emission", Range(0,10)) = 4
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        // 光らせたいので加算。普通の透過にしたければここを変える
        Blend One One
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            float4 _Color;
            float _FresnelPower;
            float _Emission;
            float _Alpha;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // 位置
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                // 法線をワールドへ
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                // カメラへのベクトル
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 法線とビューのなす角 → 正面なら1、斜めなら0に近づく
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);

                // Fresnel: 斜めほど大きくなる（正面0、斜め1）
                float f = 1.0 - saturate(dot(n, v));
                f = pow(f, _FresnelPower);   // 値を上げると“角だけ”になる

                // 斜めほどコクっと光る色
                float3 col = _Color.rgb * f * _Emission;

                // アルファも同じfを使えば、正面は透明・角で見える
                return half4(col, f * _Alpha);
            }
            ENDHLSL
        }
    }
}
