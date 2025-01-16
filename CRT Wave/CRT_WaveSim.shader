Shader"SimulCat/Wave/Phase Map CRT"
{
    /*
    A CRT is a cross between a regular shader and a compute shader.
    The output can be linked directly to the game engine graphics as a render texture similar to a camera render texture.
    
    Like a compute shader, it can be set to run independently of the graphics frame cadence allowing it to be explicitly invoked from code.
    From code, you can choose what pass to invoke, and what zone of the texture needs updating (this example does not implement zones).

    Maintaining state: While graphics shaders are stateless, the CRT can be set to be double-buffered so that the main "texture" (data) state persists from one pass to the next.

    This example is intended for dual use. First, the output can be used directly with a standard shader
    to do wave calculation across a surface and is able to be used with a standard fragment shader running on a material. This produces static output unless the CRT shader is output (e.g. with new phase to animate the waves.)

    Second, it can be set to output the raw phasor data so that custom shaders can animate or switch the display. Recommended if using lots of source data array has many points, meaning lots of summing across the source for each poin i the CRT mesh.

    To use: Pick a wave mesh resolution (best less than desired wavelength 
            it will work below this, but lots of brain damaging aliasing), 
        
            Create a render texture, but instead of the familiar camera render texture "Create->Custom Render Texture".
    
        This CustomRenderTexture defines the 
            Texture Dimension (use 2D) 
            Size ("pixels" the number of points x,y)
            Color Format ("For this experimental work 4x32Bit floats, use R32G32B32A32_SFLOAT")
            Now it might seem odd, but to hook this particular shader to the CRT you now need a material with this shader on it. 
            As I understand it, function of the material is to allow the CRT's properties to be managed and output.
            To manage the system your code basically needs to have references to the CRT and it's material.
   */
    Properties
    {
        _Lambda("Wavelength", float) = 49.64285714
        _SlitCount("Num Sources",float) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", float) = 12.0
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1
        _SourceResolution("Source Resolution",Range(0.1,5)) = 0.5
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"
    

float _Lambda;
int _SlitCount;
float _SlitPitch;
float _SlitWidth;
float _Scale;
float _SourceResolution;


static const float Tau = 6.28318531f;
   
float2 sourcePhasor(float2 delta)
{
    float rPixels = length(delta);
    float rLambda = rPixels/_Lambda;
    float rPhi = rLambda*Tau;
    float amp = _Scale*_Lambda/max(_Lambda,rPixels);
    float2 result = float2(cos(rPhi),sin(rPhi));
    return result * amp;
}

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float2 pos = i.globalTexcoord;
    float4 output = float4(1, 1, 0,1);
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));

    float2 phasor = float2(0,0);
    float slitWidePx = max(1.0,_SlitWidth);
    float apertureAtRes = slitWidePx/_SourceResolution;
    int slitWidthCount = max(1,round(apertureAtRes/_SourceResolution));
    float stepDelta = slitWidePx/slitWidthCount;
    int sourceCount = round(_SlitCount);
    int phasorCount = 0;
    float pixScale = 1 / _Scale;
    
    float apertureTop = (max(sourceCount - 1.0,0) * _SlitPitch * 0.5) + _SlitWidth * 0.5;
    float2 delta = float2(xPixel*_Scale,0.0);
    float yScaled = (yPixel - _CustomRenderTextureHeight / 2.0)*_Scale;
    for (int nAperture = 0; nAperture < sourceCount; nAperture++)
    {
        float slitY = apertureTop;
        float2 phaseAmp = float2(0, 0);
        for (int apertureStep = 0; apertureStep < slitWidthCount; apertureStep++)
        {
             delta.y = abs(yScaled-slitY);
             phaseAmp += sourcePhasor(delta);
             slitY -= stepDelta;
             phasorCount++;
        }
        phasor += phaseAmp;
        apertureTop -= _SlitPitch;
    }
    phasor *= 1.0/phasorCount;
    /*
       Our mesh point now has a summed phasor that can be used to derive:
         a) Phasor magnitude overall surface amplitude.
         b) Surface displacement (or field strength) x amplitude 
         c) Surface vertical velocity (or inductive force) y amplitude
         d) Squared magnitude is the overall energy transport (i.e. momentum).
         e) x and y squared separate the potential and kinetic energy components.
    */

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