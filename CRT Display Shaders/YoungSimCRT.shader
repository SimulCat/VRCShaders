Shader "SimulCat/Young/CRT Simulation"
{

    Properties
    {
        // _ViewSelection("Show A=0, A^2=1, E=2",Range(0.0,2.0)) = 0.0
        _Lambda("Lambda Pixels", float) = 49.64285714
        _LeftPx("Left Edge",float) = 50
        _RightPx("Right Edge",float) = 1964
        _UpperEdge("Upper Edge",float) = 972
        _LowerEdge("Lower Edge",float) = 76

        _SlitCount("Number of Slits",float) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1

        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)

        _OutputRaw("Generate Raw Output", float) = 0
        _DisplayMode("Display Mode", float) = 0
        _Frequency("Wave Frequency", float) = 0
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"
    
//#define A(U)  tex2D(_SelfTexture2D, float2(U))

float _Lambda;
float _LeftPx;
float _RightPx;
float _UpperEdge;
float _LowerEdge;
int _SlitCount;
float _SlitPitch;
float _SlitWidth;
float _Scale;

float4 _Color;
float4 _ColorNeg;
float4 _ColorVel;
float4 _ColorFlow;

float _OutputRaw;
float _DisplayMode;

float _Frequency;
static const float Tau = 6.28318531f;
static const float PI = 3.14159265f;
   
float2 sourcePhasor(float2 delta)
{
    float rPixels = length(delta);
    float rLambda = rPixels/_Lambda;
    float rPhi = rLambda * Tau;
    float amp = _Scale*_Lambda/max(_Lambda,rPixels);
    float2 result = float2(cos(rPhi),sin(rPhi));
    return result * amp;
}

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float2 pos = i.globalTexcoord;
    float4 output = float4(1, 0, 1,1);
    int displayMode = round(_DisplayMode);
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));
    bool isInMargin = (xPixel >= _LeftPx) && (xPixel <= _RightPx);
    bool isInHeadFoot = (yPixel >= _LowerEdge) && (yPixel <= _UpperEdge);
    float2 phasor = float2(0,0);
    int slitWidthCount = (int) (max(1.0, _SlitWidth));
    int sourceCount = round(_SlitCount);
    float pixScale = 1 / _Scale;
    float sourceY = ((_SlitCount - 1) * +_SlitPitch) * 0.5 + (_SlitWidth * 0.25);
    float2 delta = float2(abs(xPixel-_LeftPx)*_Scale,0.0);
    float yScaled = (yPixel - _CustomRenderTextureHeight / 2.0)*_Scale;
    for (int nAperture = 0; nAperture < sourceCount; nAperture++)
    {
        float slitY = sourceY;
        float2 phaseAmp = float2(0, 0);
        for (int pxCount = 0; pxCount < slitWidthCount; pxCount++)
        {
             delta.y = abs(yScaled-slitY);
             phaseAmp += sourcePhasor(delta);
             slitY -= 1;
        }
        phasor += phaseAmp;
        sourceY -= _SlitPitch;
    }

   float phaseAmp = length(phasor); 
   float ampSq = phaseAmp * phaseAmp;
   if (_OutputRaw >= 0.5)
   {
       output.xy = phasor;
       output.z = phaseAmp;
       output.w = ampSq; 
       return output;
   }

    if (isInMargin && isInHeadFoot)
    {
        if (displayMode < 4 && _Frequency > 0)
        {
            float2 sample = phasor;
            float tphi = (1 - frac(_Frequency * _Time.y)) * Tau;
            float sinPhi = sin(tphi);
            float cosPhi = cos(tphi);
            phasor.x = sample.x * cosPhi - sample.y * sinPhi;
            phasor.y = sample.x * sinPhi + sample.y * cosPhi;
        }
        
        float value = 0;
        if (displayMode < 2)
        {
            value = phasor.x;
            if (displayMode == 1)
            {
                value *= value;
                output = lerp(_ColorNeg, _Color, value);
            }
            else
            {
                output = lerp(_ColorNeg, _Color, value);
                value = (value + 1);
            }
            output.a = clamp(value, 0.2, 1); //      value;
        }
        else if (displayMode < 4)
        {
            value = phasor.y;
            if (displayMode == 3)
            {
                value *= value;
                output = lerp(_ColorNeg, _ColorVel, value);
            }
            else
            {
                output = lerp(_ColorNeg, _ColorVel, value);
                value = (value + 1);
            }
            output.a = clamp(value, 0.2, 1);
        }
        else if (displayMode == 5)
        {
            value = ampSq;
            output = lerp(_ColorNeg, _ColorFlow, value);
            output.a = clamp(value, 0.2, 1);
        }
        else
        {
            value = phaseAmp * 0.5 + 0.5;
            output = lerp(_ColorNeg, _ColorFlow, value);
            output.a = value;
        }
    }
    else
    {
        output = _ColorNeg;
        output.a = 0.33;
    }
    return output;
}

ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Off
        Pass
        {
            Name "Update"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
    }
}