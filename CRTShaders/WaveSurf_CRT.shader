Shader "SimulCat/Wave Surface/From Phase CRT"
{
    Properties
    {
        _PhaseSurface ("Surface Phase", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _WaveStrength("WaveStrength", Range(0, 0.1)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alpha addshadow fullforwardshadows vertex:disp


        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 5.0

        sampler2D _PhaseSurface;
        float4 _PhaseSurface_TexelSize;

        struct Input
        {
            float2 uv_PhaseSurface;
        };

        half _Glossiness;
        half _Metallic;
        float _WaveStrength;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void disp (inout appdata_full vertexData) 
        {
            float hgt = 0;

            float3 p = vertexData.vertex.xyz;
            hgt = tex2Dlod(_PhaseSurface, float4(vertexData.texcoord.xy, 0, 0)).a;
            p.y = smoothstep(-16,16, hgt) * _WaveStrength;
            vertexData.vertex.xyz = p;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_PhaseSurface, IN.uv_PhaseSurface);
            float3 duv = float3(_PhaseSurface_TexelSize.xy, 0);
            float delta = _PhaseSurface_TexelSize.x*2;

            o.Albedo =  fixed4(c.rgb,1);
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
            float v1 = smoothstep(-16,16,tex2D(_PhaseSurface, IN.uv_PhaseSurface - duv.xz).a);
            float v2 = smoothstep(-16,16,tex2D(_PhaseSurface, IN.uv_PhaseSurface + duv.xz).a);
            float v3 = smoothstep(-16,16,tex2D(_PhaseSurface, IN.uv_PhaseSurface - duv.zy).a);
            float v4 = smoothstep(-16,16,tex2D(_PhaseSurface, IN.uv_PhaseSurface + duv.zy).a);
            float d1 = _WaveStrength * (v1 - v2);
            float d2 = _WaveStrength * (v3 - v4);
            o.Normal = normalize(float3(d1,delta, d2));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
