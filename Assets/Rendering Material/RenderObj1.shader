Shader "RenderObj1"
{

    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }
        SubShader
        {
            Tags { "RenderType" = "Opaque" }
            LOD 200

            //Cull Off

            CGPROGRAM

            //#pragma surface surf Standard vertex:vert addshadow
            //#pragma surface surf Standard vertex:vert fullforwardshadows
            #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
            //#pragma surface surf Standard vertex:vert

            //#pragma multi_compile_instancing //don't need in surface shader
            #pragma target 4.5

            #include "UnityCG.cginc"




        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            //float4 tangent : TANGENT;
            float4 color : COLOR;
            float4 texcoord : TEXCOORD0;
            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;
            uint vId : SV_VertexID;
            uint iId: SV_InstanceID;
        };





        struct Input
        {
            float2 uv_MainTex;
        };


        struct vertData
        {
            float3 pos;
            float2 uvs;
            float3 norms;
        };

    #ifdef SHADER_API_D3D11

        StructuredBuffer<vertData> vertsBuff;
        StructuredBuffer<int> triBuff;

    #endif


        float4x4 _LocalToWorld;
        float4x4 _WorldToLocal;

        void vert(inout appdata v)
        {

    #ifdef SHADER_API_D3D11

            int vertsBuff_Index = triBuff[(v.iId * 6) + v.vId];
            vertData vData = vertsBuff[vertsBuff_Index];

            v.vertex = float4(vData.pos, 1.0);
            v.normal.xyz = vData.norms;
            v.texcoord = float4(vData.uvs, 0., 0.);
            v.color = float4(1.,0.,0.,1.);


    #endif

            //probem here
            // Transform modification
            //unity_ObjectToWorld = _LocalToWorld;
            //unity_WorldToObject = _WorldToLocal;
        }

        fixed4 _Color;
        sampler2D _MainTex;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {

            float4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            o.Albedo = c.rgb;


        }

        ENDCG
        }
            FallBack "Diffuse"


}