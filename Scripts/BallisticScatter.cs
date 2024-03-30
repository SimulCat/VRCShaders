
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using static UnityEngine.ParticleSystem;

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
    bool matSimIsValid = false;
    [SerializeField]
    Material matDisplay = null;
    
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
    [SerializeField]
    private Color displayColor = Color.cyan;
    [SerializeField]
    private float maxMomentum = 10;
    [SerializeField]
    private float particleMomentum = 1;       // _ParticleK("pi*p/h", float) = 0.26179939

    [SerializeField]
    private float particleK = 0.26179939f;       // _ParticleK("pi*p/h", float) = 0.26179939
    [SerializeField] bool crtUpdateRequired = false;
    [SerializeField] bool gratingUpdateRequired = false;

    //[SerializeField]
    //float[] probs;
    Color[] texData;
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
            if (matSimIsValid) 
            {
                matSim.SetFloat("_SlitCount", slitCount);
                matSim.SetFloat("_SlitWidth", slitWidth);
                matSim.SetFloat("_SlitPitch", slitPitch);
                matSim.SetFloat("_Scale", simScale);
            }
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
            if (matSimIsValid)
                matSim.SetFloat("_Scale", simScale);
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
            if (matSimIsValid)
                matSim.SetFloat("_SlitCount", slitCount);
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
            if (matSimIsValid)
                matSim.SetFloat("_SlitWidth", slitWidth);

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
            if (matSimIsValid)
                matSim.SetFloat("_SlitPitch", slitPitch);

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
            particleK = Mathf.PI * particleP;
            if (matSimIsValid)
            {
                matSim.SetFloat("_ParticleK", particleK);
                matSim.SetFloat("_ParticleP", particleMomentum);
            }
        }
    }

    public Color DisplayColor
    {
        get => displayColor;
        set
        {
            displayColor = value;
            if (matDisplay != null)
                matDisplay.SetColor("_Color", displayColor);
        }
    }

    public void DefineTexture(Material theMaterial, string thePropertyName)
    {
        matSim = theMaterial;
        texName = !string.IsNullOrWhiteSpace(thePropertyName) ? thePropertyName : "_MainTex";
        matSimIsValid = (matSim != null);
        if (matSimIsValid)
            matSimIsValid = matSim.HasProperty(texName); 
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

    public bool CreateTexture()
    {
        //if (probs == null || probs.Length != pointsWide)
        //    probs = new float[pointsWide];
        if (texData == null || texData.Length != (pointsWide*2))
            texData = new Color[pointsWide+pointsWide];
        if (!matSimIsValid)
        {
            Debug.LogWarning(gameObject.name + "BallisticScatter->Create Texture->[No Material]");
            return false;
        }
        var tex = new Texture2D(pointsWide*2, 1, TextureFormat.RGBAFloat,0,true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float probSummed = 0;
        float impulse;
        float prob;
        float pi_h = Mathf.PI/planckSim;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxMomentum*i)/pointsWide;

            prob = sampleDistribution(impulse*pi_h);
            //probs[i] = prob;
            texData[pointsWide+i] = new Color(prob, probSummed, impulse,1f);
            texData[pointsWide-i] = new Color(prob, -probSummed, impulse,1f);
            probSummed += prob;
        }
        texData[0] = new Color(0, -probSummed, -1, 1f);

        // Normalize
        //float total = texData[pointsWide-1].g;
        //for (int i = 0;i < pointsWide; i++)
        //    texData[i].g /= total;
        matSim.SetFloat("_MapMaxP",maxMomentum); // "Map max momentum", float ) = 1
        matSim.SetFloat("_MapSum", probSummed); // "Map Summed probability", float ) = 1
        tex.SetPixels(0, 0, pointsWide*2, 1, texData, 0);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        matSim.SetTexture(texName, tex);
        Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    private void Update()
    {
        if (gratingUpdateRequired)
        {
            CreateTexture();
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
        texData = new Color[pointsWide];
        iHaveParticleCRT = simCRT != null;
        if (iHaveParticleCRT)
            matSim = simCRT.material;
        DefineTexture(matSim, texName);
    }
}
