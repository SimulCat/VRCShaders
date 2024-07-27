Shader "SimuCat/Wave Surface/From Height"
{
    Properties
    {
        _MainTex ("HeightMap", 2D) = "white" {}
        
        _Color("Main Colour", Color) = (.45,.8,1,.4)
        _ColorBase("Base Colour", Color) = (.2,.3,.4,1)
        _ColourScale("Scale Colour Range",Range(0.1,1.0)) = 0.5
    
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

        fixed4 _Color;
        fixed4 _ColorBase;
        float  _ColourScale;
        half _Glossiness;
        half _Metallic;
        

        struct Input
        {
            float2 uv_MainTex;
        };

        void verts (inout appdata_full vertices) 
        {
            float3 p = vertices.vertex.xyz;
            p.y = tex2Dlod(_MainTex, float4(vertices.texcoord.xy, 0, 0)).x;
            vertices.vertex.xyz = p;
        }

        void surf (Input i, inout SurfaceOutputStandard o)
        {
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // Albedo comes from a texture tinted by color
            float hgt = tex2D(_MainTex, i.uv_MainTex).x;
            fixed4 c = _Color;
            float lvl = hgt * _ColourScale;

            c = lerp(_ColorBase, _Color, lvl);
            
            float3 duv = float3(_MainTex_TexelSize.xy, 0);
            float delta = _MainTex_TexelSize.x*2;

            o.Albedo =  fixed4(c.rgb,1);
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            float v1 = tex2D(_MainTex, i.uv_MainTex - duv.xz).x;
            float v2 = tex2D(_MainTex, i.uv_MainTex + duv.xz).x;
            float v3 = tex2D(_MainTex, i.uv_MainTex - duv.zy).x;
            float v4 = tex2D(_MainTex, i.uv_MainTex + duv.zy).x;
            float d1 = v1 - v2;
            float d2 = v3 - v4;
            o.Normal = normalize(float3(d1,delta, d2));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
