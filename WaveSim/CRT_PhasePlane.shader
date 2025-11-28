Shader"SimulCat/CRT/PhasePlane CRT"
{
    Properties
    {
        _PlaneWidth("Screen Width mm", float) = 2560.0
        _PlaneHeight("Screen Height mm",float) = 1440.0
        _Wavelength("Wavelength (mm)", float) = 1.0
        _Distance("Distance (mm)", float) = 5000.0
    }

CGINCLUDE

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
    pos.x -= _PlaneWidth/2.0;
    pos.y -= _PlaneHeight/2.0;
    float r = length(pos);
    float delta = r*sin(atan2(r,_Distance));
    float phase = delta/_Wavelength;
    float normAmplitude = _Distance/(_Distance+delta);

    return float4(delta,normAmplitude,phase,frac(phase));
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