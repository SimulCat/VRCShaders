Shader"SimulCat/CRT/Display Surface Opaque"
{
    Properties
    {
        _MainTex ("CRT Texture", 2D) = "grey" {}
        _IdleTex ("Idle Wallpaper", 2D) = "grey" {}
        _IdleColour ("Idle Shade",color) = (0.5,0.5,0.5,1)

        _ShowReal("Show Real", float) = 1
        _ShowImaginary("Show Imaginary", float) = 0
        _ShowSquare("Show Square", float) = 0

        _ScaleAmplitude("Scale Amplitude", Range(0.1, 5)) = 1
        _ScaleEnergy("Scale Energy", Range(0.1, 5)) = 1

        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _Frequency("Wave Frequency", float) = 0
    }

    SubShader
    {
        Tags {"RenderType"="Opaque"}
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

            sampler2D _IdleTex;
            float4 _IdleTex_ST;
            float4 _IdleColour;

            float _ScaleAmplitude;
            float _ScaleEnergy;

            float _ShowReal;
            float _ShowImaginary;
            float _ShowSquare;
            
            float4 _Color;
            float4 _ColorNeg;
            float4 _ColorVel;
            float4 _ColorFlow;
            float _Frequency;

            static const float Tau = 6.28318531f;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                if (_ShowReal <= 0 && _ShowImaginary <= 0)
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                else
                    o.uv = TRANSFORM_TEX(v.uv, _IdleTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                bool displaySquare = round(_ShowSquare);
                bool displayReal = _ShowReal > 0;
                bool displayIm = _ShowImaginary > 0;                       
                fixed4 col = _ColorNeg;

                if (!(displayReal || displayIm))
                {
                    fixed4 sample = tex2D(_IdleTex, i.uv);
                    col.rgb = sample.rgb * _IdleColour;
                    col.a = sample.r * _IdleColour.a;
                    return col;
                }
                            // sample the texture
                float4 sample = tex2D(_MainTex, i.uv);
                float2 pos = i.uv;
                float2 phasor;

                // If showing phase, rotate phase vector, no need to recalculate pattern, this allows CRT to calculate once, then leave alone;
                if (displayIm && displayReal)
                {
                    float value;
                    if (displaySquare)
                        value = sample.w * _ScaleEnergy;
                    else
                        value = sample.z * _ScaleAmplitude;
                    col = lerp(_ColorNeg, _ColorFlow, value);
                    return col;
                }

                if (_Frequency > 0 )
                {
                    float tphi = (1 - frac(_Frequency * _Time.y)) * Tau;
                    float sinPhi = sin(tphi);
                    float cosPhi = cos(tphi);
                    phasor.x = sample.x * cosPhi - sample.y * sinPhi;
                    phasor.y = sample.x * sinPhi + sample.y * cosPhi;
                }
                else
                    phasor = sample.xy;

                if (displayReal)
                {
                    if (displaySquare)
                        phasor.x *= (phasor.x * _ScaleEnergy);
                    else
                        phasor.x *= _ScaleAmplitude;
                    col = lerp(_ColorNeg, _Color, phasor.x);
                    return col;
                }
                // else (displayIm)
                if (displaySquare)
                    phasor.y *= (phasor.y * _ScaleEnergy);
                else
                    phasor.y *= _ScaleAmplitude;
                col = lerp(_ColorNeg, _ColorVel, phasor.y);
                return col;
            }
            ENDCG
        }
    }
}
