Shader "SimuCat/Ballistic/Display From CRT"
{
    Properties
    {
        _MainTex ("Surface Probability", 2D) = "grey" {}

        _Color("Colour", Color) = (.45,.8,1,.4)
        _ColorBase("Base Colour", Color) = (.45,.8,1,.4)

        _ScaleIntensity("Scale Intensity", Range(.01, 5)) = 1
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _ColorBase;

            float _ScaleIntensity;

            struct Input
            {
                float2 uv_MainTex;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }


            float sampleColour(float4 crtSample)
            {
                return crtSample.x * _ScaleIntensity;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = _ColorBase;
                float4 crtSample = tex2D(_MainTex, i.uv);
                float bright = sampleColour(crtSample);
                col.rgb = lerp(_ColorBase,_Color,bright).rgb;
                return col;
            }
            ENDCG
        }
    }
}
