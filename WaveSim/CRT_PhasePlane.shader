Shader"Murpheus/CRT/PhasePlane CRT"
{
    Properties
    {
        _PlaneWidth("Screen Width", float) = 2560.0
        _PlaneHeight("Screen Height",float) = 1440.0
        _Wavelength("Wavelength", float) = 1.0
        _Distance("Distance", float) = 5000.0
    }

CGINCLUDE
    #define twoPI 6.28318531f
    #define PI 3.14159265f  
    #include "UnityCustomRenderTexture.cginc"

        float  _PlaneWidth;
        float  _PlaneHeight;
        float  _Wavelength;
        float  _Distance;

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float result = 0;
    float2 pos = i.globalTexcoord.xy;
    pos.x *= _PlaneWidth;
    pos.y *= _PlaneHeight;
    pos.x -= _PlaneWidth * 0.5;
    pos.y -= _PlaneHeight * 0.5;
    float distSquared = _Distance * _Distance;
    float posSquared = dot(pos, pos);
    float r = sqrt(distSquared + posSquared);
    float normAmplitude = _Distance/r; // Normalised amplitude for spherical wavefront 1 = amplitude at centre
    float delta = posSquared/(r + _Distance);
    float phaseDelta = (twoPI*delta)/_Wavelength;
    return float4(phaseDelta,normAmplitude,delta,1.0);
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