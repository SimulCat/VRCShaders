Shader"Simulation/Display from Wave CRT"
{
    Properties
    {
        _MainTex ("CRT Texture", 2D) = "grey" {}

        _DisplayMode("Display Mode", float) = 0

        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _PhaseSpeed("Animation Speed", float) = 0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha
        OneMinusSrcAlpha
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
            float4 _MainTex_TexelSize;

            float _DisplayMode;
            float4 _Color;
            float4 _ColorNeg;
            float4 _ColorVel;
            float4 _ColorFlow;
            float _PhaseSpeed;

            static const float Tau = 6.28318531f;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                            // sample the texture
                fixed4 sample = tex2D(_MainTex, i.uv);
                float2 pos = i.uv;
                float2 phasor = float2(0,0);
                float amplitude = sample.z;
                float ampSq = sample.z;
                float value = 0;

                if (_DisplayMode < 0)
                {
                    col = _ColorNeg ;
                    col.a = 0.33;
                    return col;
                }

                if (displayMode < 4 && _PhaseSpeed > 0)
                {
                    float tphi = (1 - frac(_PhaseSpeed * _Time.y)) * Tau;
                    float sinPhi = sin(tphi);
                    float cosPhi = cos(tphi);
                    phasor.x = sample.x * cosPhi - sample.y * sinPhi;
                    phasor.y = sample.x * sinPhi + sample.y * cosPhi;
                }
                else
                    phasor = sample.xy;

                if (_DisplayMode < 2)
                {
                    value = phasor.x;
                    if (_DisplayMode > 0.1)
                    {
                        value *= value;
                        col = lerp(_ColorNeg, _Color, alpha);
                    }
                    else
                    {
                        col = lerp(_ColorNeg, _Color, alpha);
                        alpha = (alpha + 1);
                    }
                    col.a = value; //      alpha;
                }
                else if (_DisplayMode < 3.9)
                {
                    alpha = phasor.y;
                    if (_DisplayMode > 2.1)
                    {
                        alpha *= alpha;
                        col = lerp(_ColorNeg, _ColorVel, alpha);
                    }
                    else
                    {
                        col = lerp(_ColorNeg, _ColorVel, alpha);
                        alpha = (alpha + 1);
                    }
                    col.a = clamp(alpha, 0.3, 1);
                }
                else
                {
                    alpha = (phasor.x * phasor.x) + (phasor.y * phasor.y);
                    col = lerp(_ColorNeg, _ColorFlow, alpha);
                    col.a = clamp(alpha, 0.3, 1);
                }
                return col;
            }
            ENDCG
        }
    }
}
