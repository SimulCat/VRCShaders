Shader"SimulCat/Wave/Display Phase from CRT Opaque"
{
    Properties
    {
        _MainTex ("CRT Texture", 2D) = "grey" {}

        _ShowReal("Show Real", float) = 1
        _ShowImaginary("Show Imaginary", float) = 0
        _ShowSquare("Show Square", float) = 0

        _ScaleAmplitude("Scale Amplitude", Range(0.1, 120)) = 50
        _ScaleEnergy("Scale Energy", Range(0.1, 100)) = 50
        _Brightness("Display Brightness", Range(0,2)) = 1

        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _Frequency("Frequency", float) = 0
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

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
		        UNITY_VERTEX_INPUT_INSTANCE_ID

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

		        UNITY_VERTEX_INPUT_INSTANCE_ID
		        UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _ScaleAmplitude;
            float _ScaleEnergy;
            float _Brightness;

            float _ShowCRT;
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
		        UNITY_SETUP_INSTANCE_ID(v);
		        UNITY_INITIALIZE_OUTPUT(v2f, o);
		        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
        		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                bool displaySquare = round(_ShowSquare);
                bool displayReal = _ShowReal > 0;
                bool displayIm = _ShowImaginary > 0;                       
                fixed4 col = _ColorNeg;

                if (!(displayReal || displayIm))
                {
                    return col;
                }
                            // sample the texture
                float4 sample = tex2D(_MainTex, i.uv);
                float2 pos = i.uv;
                float2 phasor = float2(1,0);
                float value = 0;
                if (displayIm && displayReal)
                {
                    if (displaySquare)
                        value = sample.w * _ScaleEnergy * _ScaleEnergy;
                    else
                        value = sample.z * _ScaleAmplitude;
                    value *= _Brightness;
                    col = lerp(_ColorNeg, _ColorFlow, value);
                    return col;
                }

                // To show wave movement, rotate phase vector, no need to recalculate pattern, this allows CRT to calculate once, then leave static;
                float tphi = (1 - frac(_Frequency * _Time.y)) * Tau;
                float sinPhi = sin(tphi);
                float cosPhi = cos(tphi);
                phasor.x = sample.x * cosPhi - sample.y * sinPhi;
                phasor.y = sample.x * sinPhi + sample.y * cosPhi;

                value = displayReal ? phasor.x : phasor.y;
                if (displaySquare)
                {
                    value *= _ScaleEnergy;
                    value *= value;
                }
                else
                    value *= _ScaleAmplitude;
                value *= _Brightness;
                col = lerp(_ColorNeg, _ShowReal ? _Color : _ColorVel, value);
                return col;
            }
            ENDCG
        }
    }
}
