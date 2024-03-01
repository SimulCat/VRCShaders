Shader"SimulCat/CRT/Ballistic Grating"
{
    /*

   */
    Properties
    {
        _NumSources("Num Sources",float) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _SamplesPerSlit("Samples per Slit", Range(1,100)) = 7
        _MomentumToPhase("Momentum to Phase", float) = 1
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1

        _MomentumTex2D("Momentum Map", 2D ) = "black" {}
        _MapLength("Momentum Scale", float ) = 1
        _ApertureTex2D("Aperture Map", 2D ) = "black" {}
        _GratingWidth("Grating Width", float ) = 1
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"

    #define M(U) tex2D(_MomentumTex2D, float2(U))
    

        float _NumSources;
        float _SlitPitch;
        float _SlitWidth;
        float _Scale;
        float _SamplesPerSlit;
        float _MomentumToPhase;
        sampler2D _MomentumTex2D;
        float _MapLength;
        sampler2D _ApertureTex2D;
        float _GratingWidth;
/*
float hash12(float2 p)
{
	float3 p3  = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float hash13(float3 p3)
{
	p3  = frac(p3 * .1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}
        
float applyGratingToPoint(uint xPix, uint yPix, uint seed)
{
    float xDelta, yDelta;
    float result = 0.0;
    float resultx, resulty;
    float baseX = (float)xPix;
    float baseY = (float)yPix;
    if ((xPix >= pixelsWide) || (yPix >= pixelsHigh))
        return 0.0;
    int i = 0;
    while (i++ < 10000)
    {
        xDelta = RandomSourcePosition(gratingColumns, gratingColPitchPx, apertureWidthPx,  seed++);
        yDelta = RandomSourcePosition(gratingRows, gratingRowPitchPx, apertureHeightPx, seed++);
        result += LerpPixelIntensity(baseX + xDelta, baseY + yDelta);
    }
    return result;
}
*/


float sampleDistribution(float2 pixelDeltaPos)
{
    float theta = abs(atan2(pixelDeltaPos.y,pixelDeltaPos.x));
    float pTransVerse = sin(theta)*_MomentumToPhase;
    float singleSlitValueSq = 1;
    float manySlitValueSq = 1;
    float dX = 1;
    float ssTheta = dX * _SlitWidth;
    if (pTransVerse != 0)
    {
        singleSlitValueSq = sin(ssTheta) / ssTheta;
        singleSlitValueSq *= singleSlitValueSq;
    }
    if (_NumSources > 1)
    {
        float dSinqd = sin(dX * _SlitPitch);
        if (dSinqd == 0)
            manySlitValueSq = _NumSources;
        else
            manySlitValueSq = sin(_NumSources * dX * _SlitPitch) / dSinqd;
        manySlitValueSq *= manySlitValueSq;
    }
    return manySlitValueSq * singleSlitValueSq;
}

// Each point in the reciprocal lattice has a 
float4 frag(v2f_customrendertexture i) : SV_Target
{
    float4 height = float4(0,0,0,-1);
    float2 pos = i.globalTexcoord.xy;
    float4 output = float4(1, 1, 0,1);
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));

    int sourceCount = round(_NumSources);
    int sampleCount = 0;
    float particleRate = 0;
    float pixScale = 1 / _Scale;
    int samplesPerSlit = max(1,round(_SamplesPerSlit));


    float sourceY = ((_NumSources - 1) * +_SlitPitch) * 0.5 + (_SlitWidth * 0.25);
    float2 delta = float2(xPixel*_Scale,0.0);
    float apertureDelta = max(1.0,_SlitWidth)/samplesPerSlit;
    float yScaled = (yPixel - _CustomRenderTextureHeight / 2.0)*_Scale;

    for (int nAperture = 0; nAperture < sourceCount; nAperture++)
    {
        float slitY = sourceY;
        float apertureRate = 0;
        for (int apertureStep = 0; apertureStep < samplesPerSlit; apertureStep++)
        {
             delta.y = abs(yScaled-slitY);
             apertureRate += sampleDistribution(delta);
             slitY -= apertureDelta;
             sampleCount++;
        }
        particleRate += apertureRate;
        sourceY -= _SlitPitch;
    }
    //particleRate *= 1.0/sampleCount;
    height.r = particleRate;
    return height;
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