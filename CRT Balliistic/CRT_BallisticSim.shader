Shader"SimulCat/Ballistic/Prob Density CRT"
{
    Properties
    {
        _Color("Colour Wave", color) = (1, 1, 1, 1)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Brightness("Display Brightness", Range(0,10)) = 1

        _SlitCount("Num Sources",Integer) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _BeamWidth("Grating Width", float) = 1
        _GratingOffset("Grating X Offset", float) = 0
        _ShowBeam("Show Beam", float) = 1
        _SamplesPerSlit("Samples per Slit", Integer) = 16
        _ParticleP("Particle Momentum", float) = 1
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1
        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1
        _MapMaxI("Map max integral", float ) = 1
    }

CGINCLUDE

    #include "UnityCustomRenderTexture.cginc"

    #define M(U) tex2D(_MomentumMap, float2(U))
    
        float4 _Color;
        float4 _ColorNeg;
        float  _Brightness;

        int _SlitCount;
        float _SlitPitch;
        float _SlitWidth;
        float _BeamWidth;
        float _GratingOffset;
        float _ShowBeam;
        int _SamplesPerSlit;
        float _ParticleP;
        float _Scale;
        sampler2D _MomentumMap;
        float4 _MomentumMap_TexelSize;
        float _MapMaxP;
        float _MapMaxI;

// Sample the distribution of cumulative probability
float4 sampleDistribution(float momentum)
{
    float mometumFrac = clamp((momentum/_MapMaxP)*0.5,-0.5,0.5);
    return M(float2(mometumFrac+.5,0.5));
}

float sampleEdges (float xDelta, float yDelta, float slitHalf)
{
    int isAfterGrating = int(xDelta >= 0);
    xDelta = abs(xDelta);

    float thetaTop = atan2(yDelta - slitHalf,xDelta);
    float momentumTop = sin(thetaTop) * _ParticleP;
    float4 probTop = sampleDistribution(momentumTop);

    float thetaLwr = atan2(yDelta + slitHalf,xDelta);
    float momentumLwr = sin(thetaLwr) * _ParticleP;
    float4 probLwr = sampleDistribution(momentumLwr);

    return isAfterGrating*(abs(probTop.y - probLwr.y)/_MapMaxI);
}

float4 fragBallistic(v2f_customrendertexture i) : SV_Target
{
    float result = 0;
    float2 pos = i.globalTexcoord.xy;
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));
    float yOffsetPx = yPixel - _CustomRenderTextureHeight / 2.0;
    _SlitCount = max(_SlitCount,1);
    float apertureY = (_SlitCount - 1) * _SlitPitch *0.5;
    float halfBeam = max(_BeamWidth,apertureY)*.5;

    float yPos = yOffsetPx * _Scale;
    float xDelta = (xPixel - _GratingOffset) * _Scale;
    float scaleWidth = (_CustomRenderTextureWidth * _Scale);
    float halfAperture = _SlitWidth * 0.5;
    for (int nAperture = 0; nAperture < _SlitCount; nAperture++)
    {
        float yDelta = yPos - apertureY;
        float2 delta = float2(xDelta,yDelta);
        float dist = (scaleWidth - length(delta))/scaleWidth;
        //result.g += dist; 
        result += sampleEdges (xDelta, yDelta, halfAperture);
        apertureY -= _SlitPitch;
    }
    // Check before aperture
    float before = _ShowBeam * ((_GratingOffset > 0 && xDelta <= 0) ? 2.0 : 0);
    result = result + before*(1.0 - smoothstep(halfBeam,halfBeam*1.3,abs(yPos)));
    float3 col = lerp(_ColorNeg,_Color,result*_Brightness).rgb;

    return float4(col,result*_Brightness+0.2);
}


float4 frag(v2f_customrendertexture i) : SV_Target
{
    float2 pos = i.globalTexcoord.xy;
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));

    _SlitCount = max(1,_SlitCount);
    _SamplesPerSlit = max(1,_SamplesPerSlit);
    int edgeCount = 2;
    int sampleCount = 0;
    float particleRate = 0;
    float pixScale = 1 / _Scale;

    float slitIncrement = _SamplesPerSlit <= 1 ? 0 : _SlitWidth/(_SamplesPerSlit - 1);


    float sourceY = ((_SlitCount - 1) * _SlitPitch + _SlitWidth) * 0.5;
    float2 delta = float2(xPixel * _Scale,0.0);
    float yPos = (yPixel - _CustomRenderTextureHeight / 2.0) * _Scale;
    float pixelDistance = 0;
    for (int nAperture = 0; nAperture < _SlitCount; nAperture++)
    {
        float slitY = sourceY;
        float apertureRate = 0;
        float apertureDistance = 0;
        for (int apertureStep = 0; apertureStep < _SamplesPerSlit; apertureStep++)
        {
             delta.y = abs(yPos-slitY);
             apertureDistance += length(delta);
             apertureRate += sampleDistribution(delta);//,apertureDistance);
             slitY -= slitIncrement;
        }
        particleRate += apertureRate/_SamplesPerSlit;
        pixelDistance += apertureDistance;
        sourceY -= _SlitPitch;
    }
    float invCount = 0.1/_SlitCount;
    particleRate *= invCount;
    pixelDistance *= invCount/_CustomRenderTextureWidth;
    return float4(lerp(_ColorNeg,_Color,particleRate).rgb,particleRate+.25);
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
            #pragma fragment fragBallistic
            ENDCG
        }

        Pass
        {
            Name "Ballistic"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment fragBallistic
            ENDCG
        }
    }
}