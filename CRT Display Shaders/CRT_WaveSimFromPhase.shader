Shader"SimulCat/CRT/Simulate Waves"
{
    /*
    A CRT to my mind is a cross between a regular shader and a compute shader.
    Unlike a compute shader, the output can be linked directly to the game engine graphics as a render texture similar to a camera render texture.
    
    Like a compute shader, it is run independently of the graphics frame cadence and needs to be explicitly output from code, you can decide what to do on each pass, and even what zone of the texture needs updating (this example does neither).

    Maintaining state: It is a halfway-house between a compute shader and a graphics shader, the latter being stateless in that it can be set to be double-buffered so that the main "texture" (data) state 

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
        _LambdaPx("Lambda Pixels", float) = 49.64285714
        _NumSources("Num Sources",float) = 2
        _SlitPitchPx("Slit Pitch",float) = 448
        _SlitWidePx("Slit Width", Range(1.0,40.0)) = 12.0
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1
        _SourceResolution("Source Resolution",Range(0.1,5)) = 0.5

        _OutputRaw("Generate Raw Output", float) = 0
        _DisplayMode("Display Mode", float) = 0

        _Color("Colour Wave", color) = (1, 1, 0, 0)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _ColorVel("Colour Velocity", color) = (0, 0.3, 1, 0)
        _ColorFlow("Colour Flow", color) = (1, 0.3, 0, 0)
        _Frequency("Wave Frequency", float) = 0
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"
    

float _LambdaPx;
int _NumSources;
float _SlitPitchPx;
float _SlitWidePx;
float _Scale;
float _SourceResolution;

float _OutputRaw;
float _DisplayMode;

float4 _Color;
float4 _ColorNeg;
float4 _ColorVel;
float4 _ColorFlow;
float _Frequency;

static const float Tau = 6.28318531f;
   
float2 sourcePhasor(float2 delta)
{
    float rPixels = length(delta);
    float rLambda = rPixels/_LambdaPx;
    float rPhi = rLambda*Tau;
    float amp = _Scale*_LambdaPx/max(_LambdaPx,rPixels);
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
    float slitWidePx = max(1.0,_SlitWidePx);
    float apertureAtRes = slitWidePx/_SourceResolution;
    int slitWidthCount = max(1,round(apertureAtRes/_SourceResolution));
    float stepDelta = slitWidePx/slitWidthCount;
    int sourceCount = round(_NumSources);
    int phasorCount = 0;
    float pixScale = 1 / _Scale;
    
    float sourceY = ((_NumSources - 1) * +_SlitPitchPx) * 0.5 + (_SlitWidePx * 0.25);
    float2 delta = float2(xPixel*_Scale,0.0);
    float yScaled = (yPixel - _CustomRenderTextureHeight / 2.0)*_Scale;
    for (int nAperture = 0; nAperture < sourceCount; nAperture++)
    {
        float slitY = sourceY;
        float2 phaseAmp = float2(0, 0);
        for (int apertureStep = 0; apertureStep < slitWidthCount; apertureStep++)
        {
             delta.y = abs(yScaled-slitY);
             phaseAmp += sourcePhasor(delta);
             slitY -= stepDelta;
             phasorCount++;
        }
        phasor += phaseAmp;
        sourceY -= _SlitPitchPx;
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

    /* The output is a raw 2D texture able to be used on a material, to use the rendertexture output in a standard shader, this part takes the data point and generates a color that can be written to the      output buffer.
        accordng to the selected display mode.
     - now set a colour for the output point so the final rendertexture can show up in a standard shader. the output is RGB (colour) a is the (signed) wave amplitude (e.g. for surface shader).
    float alpha = 0;
    */
   float phaseAmp = length(phasor); 
   float ampSq = phaseAmp * phaseAmp;
   if (_OutputRaw >= 0.5)
   {
       output.xy = phasor;
       output.z = phaseAmp;
       output.w = ampSq; 
       return output;
   }
    int displayMode = round(_DisplayMode);
    if (displayMode < 4 && _Frequency > 0)
    {
        float2 sample = phasor;
        float tphi = (1 - frac(_Frequency * _Time.y)) * Tau;
        float sinPhi = sin(tphi);
        float cosPhi = cos(tphi);
        phasor.x = sample.x * cosPhi - sample.y * sinPhi;
        phasor.y = sample.x * sinPhi + sample.y * cosPhi;
    }

   /*
     Generate required false colour representation of user's choive of wave component
     < 0 (Nothing)
     0 = x
     1 = x squared

     2 = y 
     3 = y squared

     4 = total amplitude 
     5 = total amp squared
   */
   
   float value = 0;
    if (displayMode < 2)
    {
        value = phasor.x;
        if (displayMode == 1)
        {
            value *= value;
            output = lerp(_ColorNeg, _Color, value);
            output.a = value; //      alpha;
        }
        else
        {
            value = value * 0.5 + 0.5;
            output = lerp(_ColorNeg, _Color, value);
            output.a =value; //      alpha;
        }
    }
    else if (displayMode < 4)
    {
        value = phasor.y;
        if (displayMode == 3)
        {
            value *= value;
            output = lerp(_ColorNeg, _ColorVel, value);
            output.a = value; //      alpha;
        }
        else
        {
            value = value * 0.5 + 0.5;
            output = lerp(_ColorNeg, _ColorVel, value);
            output.a = value; //      alpha;
        }
    }
    else if (displayMode == 5)
    {
        value = ampSq;
        output = lerp(_ColorNeg, _ColorFlow, value);
        output.a = clamp(value, 0.2,1);
    }
    else
    {
        value = phaseAmp * 0.5 + 0.5;
        output = lerp(_ColorNeg, _ColorFlow, value);
        output.a = value;
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