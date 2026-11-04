Shader "BeatStriker/DemonRushMana" {
    Properties { [HDR] _Color ("Mana",Color)=(2.5,.015,6,1) }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Color;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float3 local:TEXCOORD2;};
            V vert(A v) {
                V o;
                float wave=sin(v.positionOS.y*21-_Time.y*19+v.positionOS.x*12)*.035;
                v.positionOS.xyz+=v.normalOS*wave;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.local=v.positionOS.xyz;o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            half4 frag(V i):SV_Target {
                float rim=pow(1-saturate(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS)))),3);
                float veins=pow(saturate(sin(i.local.y*37-_Time.y*26+sin(i.local.x*19+i.local.z*17)*2)),24);
                return half4(_Color.rgb,(rim*.45+veins*.08)*saturate((i.local.y+.6)*2));
            }
            ENDHLSL
        }
    }
}
