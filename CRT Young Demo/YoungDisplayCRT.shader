Shader"SimulCat/Young/CRT Display Mode"
{
    Properties
    {
        _MainTex ("CRT Texture", 2D) = "grey" {}
        _IdleTex ("Idle Wallpaper", 2D) = "grey" {}
        _IdleColour ("Idle Shade",color) = (0.5,0.5,0.5,1)

       _DisplayMode("Display Mode", float) = 0

       _ScaleAmplitude("Scale Amplitude", Range(0.1, 10)) = 5
       _ScaleEnergy("Scale Energy", Range(0.1, 10)) = 5
       _Brightness("Display Brightness", Range(0.0,1.0)) = 1

        _LeftPx("Left Edge",float) = 50
        _RightPx("Right Edge",float) = 1964
        _UpperEdge("Upper Edge",float) = 972
        _LowerEdge("Lower Edge",float) = 76

        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _Frequency("Wave Frequency", float) = 0
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

            sampler2D _IdleTex;
            float4 _IdleTex_ST;
            float4 _IdleTex_TexelSize;

            float4 _IdleColour;

            float _LeftPx;
            float _RightPx;
            float _UpperEdge;
            float _LowerEdge;


            float _DisplayMode;
            float _ScaleAmplitude;
            float _ScaleEnergy;

            float _Brightness;
            
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
                if (_DisplayMode >= 0)
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                else
                    o.uv = TRANSFORM_TEX(v.uv, _IdleTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = _ColorNeg * _Brightness;

                if (_DisplayMode < 0)
                {
                    fixed4 sample = tex2D(_IdleTex, i.uv);
                    col.rgb = sample.rgb * _IdleColour;
                    col.a = sample.r * _IdleColour.a * _Brightness;
                    return col;
                }

                float4 sample = tex2D(_MainTex, i.uv);
                float2 pos = i.uv;
                float2 phasor = sample.xy;
                float amplitude = sample.z;
                float ampSq = sample.w;
                float value = 0;

                int xPixel = (int)(floor(pos.x * _MainTex_TexelSize.z));
                int yPixel = (int)(floor(pos.y * _MainTex_TexelSize.w));


                if ((xPixel < _LeftPx) || (xPixel > _RightPx) || (yPixel < _LowerEdge) || (yPixel > _UpperEdge))
                {
                    col = _ColorNeg;
                    col.a = 0.33 * _Brightness;
                    return col;
                }

                int displayMode = round(_DisplayMode);
                bool displaySquare = displayMode == 1 || displayMode == 3 || displayMode == 5;
                bool displayReal =   displayMode < 2 || displayMode > 3;
                bool displayIm =  displayMode >= 2;                         

                            // sample the texture


                if (displayIm && displayReal)
                {
                    if (displaySquare)
                        value = sample.w * _ScaleEnergy * _ScaleEnergy;
                    else
                        value = sample.z * _ScaleAmplitude;
                    value *= _Brightness;
                    col = lerp(_ColorNeg, _ColorFlow, value);
                    col.a = _Brightness * (displaySquare ? value+0.33 : clamp(value, .25,1));
                    return col;
                }

                // If showing phase, rotate phase vector, no need to recalculate pattern, this allows CRT to calculate once, then leave alone;
                if (_Frequency > 0)
                {
                    float tphi = (1 - frac(_Frequency * _Time.y)) * Tau;
                    float sinPhi = sin(tphi);
                    float cosPhi = cos(tphi);
                    phasor.x = sample.x * cosPhi - sample.y * sinPhi;
                    phasor.y = sample.x * sinPhi + sample.y * cosPhi;
                }

                value = displayReal ? phasor.x : phasor.y;
                if (displaySquare)
                {
                    value *= _ScaleEnergy;
                    value *= value;
                }
                else
                    value *= _ScaleAmplitude;

                value *= _Brightness;
                col = lerp(_ColorNeg, displayReal ? _Color : _ColorVel, value);
                col.a = _Brightness * ((displaySquare) ? value +0.33 : clamp(value + 1, 0.3, 1));
                return col;
            }
            ENDCG
        }
    }
}
