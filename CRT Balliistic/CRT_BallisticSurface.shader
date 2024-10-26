Shader "SimuCat/Ballistic/Surface From CRT"
{
    Properties
    {
        _MainTex ("Surface Probability", 2D) = "grey" {}
        _Color("Colour", Color) = (.45,.8,1,.4)
        _ColorBase("Base Colour", Color) = (.45,.8,1,.4)

        _ScaleHeight("Scale Height", Range(.001, 2)) = 1
        _ScaleIntensity("Scale Intensity", Range(.01, 5)) = 1

        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        Cull Off
        LOD 100
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard addshadow fullforwardshadows vertex:verts

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        half _Glossiness;
        half _Metallic;

        fixed4 _Color;
        fixed4 _ColorBase;

        float _ScaleHeight;
        float _ScaleIntensity;

        struct Input
        {
            float2 uv_MainTex;
        };


        float sampleHeight(float4 crtSample)
        {
            return crtSample.x * _ScaleHeight;
        }

        float sampleColour(float4 crtSample)
        {
            return crtSample.x * _ScaleIntensity;
        }

        void verts (inout appdata_full vertices) 
        {
            float3 p = vertices.vertex.xyz;
            p.y = sampleHeight(tex2Dlod(_MainTex, float4(vertices.texcoord.xy, 0, 0)));
            vertices.vertex.xyz = p;
        }

        void surf (Input i, inout SurfaceOutputStandard o)
        {
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // Albedo comes from a texture tinted by color
            float4 crtSample = tex2D(_MainTex, i.uv_MainTex);
            float hgt = sampleHeight(crtSample);
            float bright = sampleColour(crtSample);
            float3 duv = float3(_MainTex_TexelSize.xy, 0);
            float delta = _MainTex_TexelSize.x*2;
            fixed4 c = lerp(_ColorBase,_Color,bright);
            o.Albedo =  fixed4(c.rgb,1);
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            float v1 = sampleHeight(tex2D(_MainTex, i.uv_MainTex - duv.xz));
            float v2 = sampleHeight(tex2D(_MainTex, i.uv_MainTex + duv.xz));
            float v3 = sampleHeight(tex2D(_MainTex, i.uv_MainTex - duv.zy));
            float v4 = sampleHeight(tex2D(_MainTex, i.uv_MainTex + duv.zy));
            float d1 = v1 - v2;
            float d2 = v3 - v4;
            o.Normal = normalize(float3(d1,delta, d2));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
