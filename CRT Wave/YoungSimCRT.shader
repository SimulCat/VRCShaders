Shader "SimuCat/Young/CRT Simulation"
{

    Properties
    {
        // _ViewSelection("Show A=0, A^2=1, E=2",Range(0.0,2.0)) = 0.0
        _Lambda("Lambda Pixels", float) = 49.64285714
        _LeftPx("Left Edge",float) = 50

        _SlitCount("Number of Slits",float) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"
    
float _Lambda;
float _LeftPx;

int _SlitCount;
float _SlitPitch;
float _SlitWidth;
float _Scale;

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
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));
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
    output.xy = phasor;
    output.z = phaseAmp;
    output.w = ampSq; 
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