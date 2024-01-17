Shader"Phase/Demo (No Texture)"
{
    Properties
    {
        _mmHigh("Frame Height (mm)",float) = 1000
        _mmWide("Frame Width (mm)",float) = 2000
        _LambdaPx("Lambda Pixels", float) = 49.64285714
        _NumSources("Num Sources",float) = 2
        _SlitPitchPx("Slit Pitch",float) = 448
        _SlitWidePx("Slit Width", Range(1.0,80.0)) = 12.0
        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _DisplayMode("Display Mode", float) = 0
        _PhaseSpeed("Animation Speed", float) = 0
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1

    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        ZWrite Off
        Blend SrcAlpha
        OneMinusSrcAlpha
        LOD 100

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
                float4 uv0 : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 pos : TEXCOORD0;
            };

            float _mmHigh;
            float _mmWide;
            float _LambdaPx;
            int _NumSources;
            float _SlitPitchPx;
            float _SlitWidePx;
            float4 _Color;
            float4 _ColorNeg;
            float4 _ColorVel;
            float4 _ColorFlow;
            float _DisplayMode;
            float _PhaseSpeed;
            float _Scale;
            static const float Tau = 6.28318531f;
            static const float PI = 3.14159265f;
            static const float lambdaNominal = 50;
            
            float2 sourcePhasor(float2 delta)
            {
                float rPixels = length(delta);
                float rLambda = rPixels / _LambdaPx;
                float tphi = 1 - frac(_PhaseSpeed * _Time.y);
                float rPhi = (rLambda + tphi) * Tau;
                float amp = _Scale * _LambdaPx / max(_LambdaPx, rPixels);
                float2 result = float2(cos(rPhi), sin(rPhi));
                return result * amp;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.pos = float2(v.uv0.x * _mmWide, v.uv0.y * _mmHigh);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (0, 0, 0, 1);
                float2 phasor = float2(0, 0);
                int slitWidthCount = (int) (max(1.0, _SlitWidePx));
                int sourceCount = round(_NumSources);
                float sourceY = ((_NumSources - 1) * +_SlitPitchPx) * 0.5 + (_SlitWidePx * 0.25);
                float2 delta = float2(i.pos.x * _Scale, 0.0);
                float yScaled = (i.pos.y - _mmHigh / 2.0) * _Scale;
                for (int nAperture = 0; nAperture < sourceCount; nAperture++)
                {
                    float slitY = sourceY;
                    float2 phaseAmp = float2(0, 0);
                    for (int pxCount = 0; pxCount < slitWidthCount; pxCount++)
                    {
                        delta.y = abs(yScaled - slitY);
                        phaseAmp += sourcePhasor(delta);
                        slitY -= 1;
                    }
                    phasor += phaseAmp;
                    sourceY -= _SlitPitchPx;
                }
                
                float alpha = 0;
                if (_DisplayMode < 2)
                {
                    alpha = phasor.x;
                    if (_DisplayMode > 0.1)
                    {
                        alpha *= alpha;
                        col = lerp(_ColorNeg, _Color, alpha);
                    }
                    else
                    {
                        col = lerp(_ColorNeg, _Color, alpha);
                        alpha = (alpha + 1);
                    }
                    col.a = clamp(alpha, 0.3, 1); //      alpha;
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
