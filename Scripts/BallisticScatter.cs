using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BallisticScatter : UdonSharpBehaviour
{
    [SerializeField]
    CustomRenderTexture simCRT;
    bool iHaveParticleCRT = false;
    [SerializeField]
    Material matSim = null;
    [SerializeField]
    string texName = "_MomentumMap";
    [SerializeField]
    bool iHaveMatSim = false;
    [SerializeField]
    MeshRenderer particleMeshRend = null;
    [SerializeField]
    Material matParticleFlow = null;
    [SerializeField]
    bool ihaveParticleFlow = false;
    
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    [SerializeField, Tooltip("Planck Value for simulation; lambda=h/p")]
    private float planckSim = 12;


    [SerializeField,Range(1,17)]
    private int slitCount = 2;          // _SlitCount("Num Sources", float)
    [SerializeField]
    private float slitPitch = 45f;        // _SlitPitch("Slit Pitch", float)
    [SerializeField]
    private float slitWidth = 12f;        // _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
    [SerializeField, Range(1, 5)]
    private float simScale;
    //[SerializeField]
    private Color displayColor = Color.cyan;
    [SerializeField]
    private float maxMomentum = 10;
    [SerializeField]
    private float particleMomentum = 1;       // _ParticleK("pi*p/h", float) = 0.26179939

    //[SerializeField]
    //private float particleK = 0.26179939f;       // _ParticleK("pi*p/h", float) = 0.26179939
    [SerializeField] bool crtUpdateRequired = false;
    [SerializeField] bool gratingUpdateRequired = false;

    private void setGratingParams(Material mat)
    {
        mat.SetFloat("_SlitCount", slitCount);
        mat.SetFloat("_SlitWidth", slitWidth);
        mat.SetFloat("_SlitPitch", slitPitch);
        mat.SetFloat("_Scale", simScale);
    }
    public void SetGrating(int numSlits, float widthSlit, float pitchSlits, float momentumMax)
    {
        Debug.Log(string.Format("{0} SetGrating: #slit1={1} width={2} pitch={3}", gameObject.name,  numSlits, widthSlit, pitchSlits));

        bool isChanged = numSlits != slitCount || widthSlit != slitWidth || pitchSlits != slitPitch;
        isChanged |= maxMomentum != momentumMax;
        slitCount = numSlits;
        maxMomentum = momentumMax;
        slitWidth = widthSlit;
        slitPitch = pitchSlits;
        gratingUpdateRequired = isChanged;
        if (isChanged)
        {
            if (iHaveMatSim) setGratingParams(matSim);
            if (ihaveParticleFlow) setGratingParams(matParticleFlow);
        }
    }

    public float PlanckSim
    {
        get => planckSim;
        set
        {
            if(planckSim != value)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            planckSim = value;
        }
    }
    public float SimScale
    {
        get => simScale;
        set
        {
            if (value != simScale)
                crtUpdateRequired = true;
            simScale = value;
            if (iHaveMatSim)
                matSim.SetFloat("_Scale", simScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_Scale", simScale);
        }
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            if (value != slitCount)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitCount = value;
            if (iHaveMatSim)
                matSim.SetFloat("_SlitCount", slitCount);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitCount", slitCount);
        }
    }
    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            if (value != slitWidth)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitWidth = value;
            if (iHaveMatSim)
                matSim.SetFloat("_SlitWidth", slitWidth);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitWidth", slitCount);
        }
    }

    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            if (value != slitPitch)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitPitch = value;
            if (iHaveMatSim)
                matSim.SetFloat("_SlitPitch", slitPitch);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitPitch", slitPitch);

        }
    }

    public Color lerpColour(float frac)
    {
        return spectrumColour(Mathf.Lerp(700, 400, frac));
    }

    public Color spectrumColour(float wavelength, float gamma = 0.8f)
    {
        Color result = Color.white;
        if (wavelength >= 380 & wavelength <= 440)
        {
            float attenuation = 0.3f + 0.7f * (wavelength - 380.0f) / (440.0f - 380.0f);
            result.r = Mathf.Pow(((-(wavelength - 440) / (440 - 380)) * attenuation), gamma);
            result.g = 0.0f;
            result.b = Mathf.Pow((1.0f * attenuation), gamma);
        }

        else if (wavelength >= 440 & wavelength <= 490)
        {
            result.r = 0.0f;
            result.g = Mathf.Pow((wavelength - 440f) / (490f - 440f), gamma);
            result.b = 1.0f;
        }
        else if (wavelength >= 490 & wavelength <= 510)
        {
            result.r = 0.0f;
            result.g = 1.0f;
            result.b = Mathf.Pow(-(wavelength - 510f) / (510f - 490f), gamma);
        }
        else if (wavelength >= 510 & wavelength <= 580)
        {
            result.r = Mathf.Pow((wavelength - 510f) / (580f - 510f), gamma);
            result.g = 1.0f;
            result.b = 0.0f;
        }
        else if (wavelength >= 580f & wavelength <= 645f)
        {
            result.r = 1.0f;
            result.g = Mathf.Pow(-(wavelength - 645f) / (645f - 580f), gamma);
            result.b = 0.0f;
        }
        else if (wavelength >= 645 & wavelength <= 750)
        {
            float attenuation = 0.3f + 0.7f * (750 - wavelength) / (750 - 645);
            result.r = Mathf.Pow(1.0f * attenuation, gamma);
            result.g = 0.0f;
            result.b = 0.0f;
        }
        else
        {
            result.r = 0.0f;
            result.g = 0.0f;
            result.b = 0.0f;
            result.a = 0.1f;
        }
        return result;
    }


    public float ParticleMomentum
    {
        get => particleMomentum;
        set
        {
            crtUpdateRequired |= value != particleMomentum;
            particleMomentum = value;
            float particleP = particleMomentum / planckSim;
            if (iHaveMatSim)
            {
                matSim.SetFloat("_ParticleP", particleMomentum);
            }
            if (ihaveParticleFlow)
            {
                matParticleFlow.SetFloat("_ParticleP", particleMomentum);
            }
        }
    }

    public Color DisplayColor
    {
        get => displayColor;
        set
        {
            displayColor = value;
            if (iHaveParticleCRT)
                matSim.SetColor("_Color", displayColor);
            if (ihaveParticleFlow)
                matParticleFlow.SetColor("_Color", displayColor);
        }
    }

    public bool ValidMaterial(Material theMaterial, string thePropertyName)
    {
        return (theMaterial != null) && theMaterial.HasProperty(thePropertyName); 
    }

    private float sampleDistribution(float spatialK)
    {

        float slitPhase = spatialK * slitWidth;

        float apertureProbSq = Mathf.Abs(slitPhase) > 0.000001f ? Mathf.Sin(slitPhase) / slitPhase : 1.0f;
        apertureProbSq *= apertureProbSq;
        float multiSlitProbSq = 1f;
        if (slitCount > 1)
        {
            float gratingPhase = spatialK * slitPitch;
            if (slitCount == 2)
                multiSlitProbSq = Mathf.Cos(gratingPhase) * 2;
            else
            {
                float sinGrPhase = Mathf.Sin(gratingPhase);
                multiSlitProbSq = (Mathf.Abs(sinGrPhase) < 0.000001f) ? slitCount : Mathf.Sin(slitCount * gratingPhase) / sinGrPhase;
            }
            multiSlitProbSq *= multiSlitProbSq;
        }
        return multiSlitProbSq * apertureProbSq;
    }
   // [SerializeField]
    private float[] gratingFourierSq;
   // [SerializeField]
    private float[] probIntegral;
    //[SerializeField]
    private float[] probabilityLookup;

    private void GenerateSamples()
    {
        if (gratingFourierSq == null || gratingFourierSq.Length < pointsWide)
        {
            gratingFourierSq = new float[pointsWide];
            probIntegral = new float[pointsWide];
        }
        float impulse;
        float prob;
        float pi_h = Mathf.PI / planckSim;
        float probIntegralSum = 0;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxMomentum * i) / pointsWide;

            prob = sampleDistribution(impulse * pi_h);
            gratingFourierSq[i] = prob;
            probIntegral[i] = probIntegralSum;
            probIntegralSum += prob;
        }
        // Scale (Normalize?) Integral to Width of Distribution
        float normScale = pointsWide / probIntegral[pointsWide-1];
        for (int nPoint = 0; nPoint < pointsWide; nPoint++)
            probIntegral[nPoint] *= normScale;
        //probIntegral[pointsWide] = pointsWide;
    }

    private void GenerateReverseLookup()
    {
        if (probabilityLookup == null || probabilityLookup.Length < pointsWide)
            probabilityLookup = new float[pointsWide];
        // Scale prob distribution to be 0 to pointsWide at max;
        int indexAbove = 0;
        int indexBelow;
        float vmin;
        float vmax = 0;
        float frac;

        for (int i = 0; i < pointsWide; i++)
        {
            while ((vmax <= i) && (indexAbove < pointsWide - 1))
            {
                indexAbove++;
                vmax = probIntegral[indexAbove];
            }
            vmin = vmax; indexBelow = indexAbove;
            while ((indexBelow > 0) && (vmin > i))
            {
                indexBelow--;
                vmin = probIntegral[indexBelow];
            }
            if (indexBelow >= indexAbove)
                probabilityLookup[i] = vmax;
            else
            {
                frac = Mathf.InverseLerp(vmin, vmax, i);
                probabilityLookup[i] = Mathf.Lerp(indexBelow, indexAbove, frac);
            }
        }
    }
    public bool CreateTextures()
    {
        GenerateSamples();

        Color[] texData = new Color[pointsWide + pointsWide];

        if (iHaveMatSim)
        {
            var tex = new Texture2D(pointsWide * 2, 1, TextureFormat.RGBAFloat, 0, true);

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float impulse;
            for (int i = 0; i < pointsWide; i++)
            {
                impulse = (maxMomentum * i) / pointsWide;

                float sample = gratingFourierSq[i];
                float integral = probIntegral[i];
                texData[pointsWide + i] = new Color(sample, integral, impulse, 1f);
                texData[pointsWide - i] = new Color(sample, -integral, -impulse, 1f);
            }
            texData[0] = new Color(0, -probIntegral[pointsWide-1], -1, 1f);

            // Normalize
            //float total = texData[pointsWide-1].g;
            //for (int i = 0;i < pointsWide; i++)
            //    texData[i].g /= total;
            matSim.SetFloat("_MapMaxP", maxMomentum); // "Map max momentum", float ) = 1
            matSim.SetFloat("_MapSum", probIntegral[pointsWide-1]); // "Map Summed probability", float ) = 1
            tex.SetPixels(0, 0, pointsWide * 2, 1, texData, 0);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            matSim.SetTexture(texName, tex);
        }
        if (ihaveParticleFlow)
        {
            GenerateReverseLookup();
            texData = new Color[pointsWide];
            for (int i = 0; i < pointsWide; i++)
            {
                float sample = gratingFourierSq[i];
                float reverse = probabilityLookup[i];
                texData[i] = new Color(sample, reverse, 0, 1f);
            }
           // var tex = new Texture2D(pointsWide, 1, TextureFormat.RGBAFloat, 0, true);
           // tex.SetPixels(0, 0, pointsWide, 1, texData, 0);
           // tex.filterMode = FilterMode.Bilinear;
           // tex.wrapMode = TextureWrapMode.Clamp;
           // tex.Apply();

            matParticleFlow.SetFloat("_MapMaxP", maxMomentum); // "Map max momentum", float ) = 1
            //matParticleFlow.SetTexture(texName, tex);
        }
        Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    private void Update()
    {
        if (gratingUpdateRequired)
        {
            CreateTextures();
            crtUpdateRequired = true;
            gratingUpdateRequired = false;
        }
        if (crtUpdateRequired)
        {
            crtUpdateRequired = false;
            if (iHaveParticleCRT)
                simCRT.Update(1);
        }
    }

    void Start()
    {
        iHaveParticleCRT = simCRT != null;
        if (iHaveParticleCRT)
            matSim = simCRT.material;
        iHaveMatSim = ValidMaterial(matSim, texName);
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
        }
        ihaveParticleFlow = ValidMaterial(matParticleFlow, texName);
    }
}
