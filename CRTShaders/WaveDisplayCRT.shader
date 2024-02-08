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
                float4 sample = tex2D(_MainTex, i.uv);
                float2 pos = i.uv;
                float2 phasor = float2(0,0);
                float amplitude = sample.z;
                float ampSq = sample.z;
                float value = 0;
                fixed4 col = _ColorNeg;
                int displayMode = round(_DisplayMode);
                // If showing phase, rotate phase vector, no need to recalculate pattern, this allows CRT to calculate once, then leave alone;
                if (displayMode < 4 && _PhaseSpeed > 0)
                {
                    float tphi = (1 - frac(_PhaseSpeed * _Time.y)) * Tau;
                    float sinPhi = sin(tphi);
                    float cosPhi = cos(tphi);
                    phasor.x = sample.x * cosPhi - sample.y * sinPhi;
                    phasor.y = sample.x * sinPhi + sample.y * cosPhi;
                }
                else
                    phasor = sample.xy; // Just use the calculated phase
                
                switch (displayMode)
                {
                    case 0: // Just x component
                        value = phasor.x;
                        col = lerp(_ColorNeg, _Color, value);
                        col.a = clamp(value+1, 0.3,1);
                        return col;

                    case 1: // x component squared
                        value = phasor.x * phasor.x;
                        col = lerp(_ColorNeg, _Color, value);
                        col.a = value+0.25;
                        return col;

                    case 2: // Vertical velocity
                        value = phasor.y;
                        col = lerp(_ColorNeg, _ColorVel, value);
                        col.a = clamp(value+1,0.3,1);
                        return col;

                    case 3: // Surface kinetic energy from speed of mass rise/fall
                        value = phasor.y * phasor.y;
                        col = lerp(_ColorNeg, _ColorVel, value);
                        col.a = value+0.25;
                        return col;

                    case 4: // Combined Amplitude (phasor length)
                        value = amplitude;
                        col = lerp(_ColorNeg, _ColorFlow, value);
                        col.a = clamp(value+1, 0.3,1);
                        return col;
                    case 5: // Combined Amplitude Squared (Momentum/ Energy transport)
                        value = ampSq;
                        col = lerp(_ColorNeg, _ColorFlow, value);
                        col.a = value+0.25,0,1;
                        return col;

                    default:
                        col = _ColorNeg;
                        col.a = 0.33;
                        return col;
                        break;
                }
                return col;
            }
            ENDCG
        }
    }
}
