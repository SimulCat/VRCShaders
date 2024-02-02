Shader "Phase/Wave Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _WaveMesh ("Wave Mesh", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _UseHeight("Use Sfc Height",Range(0.0,1)) = 1    
        _UseVelocity("Use Sfc Velocity",Range(0.0,1)) = 0
        _UseSquare("Square Amplitude",Range(0.0,1)) = 1    
        _K("WaveNumber K",Range(0.001,1)) = 0.1
        _Displacement("Height Scale", Range(0, 0.1)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alpha addshadow fullforwardshadows vertex:disp


        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 5.0

        sampler2D _WaveMesh;
        float4 _WaveMesh_TexelSize;

        struct Input
        {
            float2 uv_WaveMesh;
        };

        half _Glossiness;
        half _Metallic;
        float _Displacement;
        fixed4 _Color;

        float _UseHeight;
        float _UseVelocity;
        float _UseSquare;
        float _K;
        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void disp (inout appdata_full vertexData) 
        {
                float hgt = 0;
                float vel = 0;

            float3 p = vertexData.vertex.xyz;
            if (_UseHeight > 0.5)
            {
                hgt = tex2Dlod(_WaveMesh, float4(vertexData.texcoord.xy, 0, 0)).r;
                if (_UseSquare)
                    hgt *= hgt;
            }
            if (_UseVelocity > 0.5)
            {
                vel = tex2Dlod(_WaveMesh, float4(vertexData.texcoord.xy, 0, 0)).g / _K;
                if (_UseSquare)
                    vel *= vel;
            }
            p.y = (hgt + vel) * _Displacement;
            vertexData.vertex.xyz = p;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_WaveMesh, IN.uv_WaveMesh) * _Color;
            float3 duv = float3(_WaveMesh_TexelSize.xy, 0);
            float delta = _WaveMesh_TexelSize.x*2;
            float lvl = (c.r + 1)/2.0;
            o.Albedo =  _Color * lvl;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
            float v1 = tex2D(_WaveMesh, IN.uv_WaveMesh - duv.xz).r;
            float v2 = tex2D(_WaveMesh, IN.uv_WaveMesh + duv.xz).r;
            float v3 = tex2D(_WaveMesh, IN.uv_WaveMesh - duv.zy).r;
            float v4 = tex2D(_WaveMesh, IN.uv_WaveMesh + duv.zy).r;
            float d1 = _Displacement * (v1 - v2);
            float d2 = _Displacement * (v3 - v4);
            o.Normal = normalize(float3(d1,delta, d2));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
