using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BallisticScatter : UdonSharpBehaviour
{
    [Header("Simulation Components")]
    [SerializeField]
    CustomRenderTexture simCRT;
    [SerializeField,Tooltip("Use same units as slit width and pitch")]
    Vector2 simSize = new Vector2(2.56f, 1.6f);
    [SerializeField] float metresPerUnit = 1f;  
    
    [SerializeField]
    Vector2Int simPixels = new Vector2Int(1024, 640);
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

    [Header("Scattering Configuration")]
    
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    [SerializeField, Tooltip("Planck Value for simulation; lambda=h/p"),FieldChangeCallback(nameof(PlanckSim))]
    public float planckSim = 12;

    [Header("Grating Configuration & Scale")]
    [SerializeField,Range(1,17),FieldChangeCallback(nameof(SlitCount))]
    public int slitCount = 2;          // _SlitCount("Num Sources", float)
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    public float slitPitch = 45f;        // "Slit Pitch" millimetre
    [SerializeField,FieldChangeCallback(nameof(SlitWidth))]
    public float slitWidth = 12f;        // "Slit Width" millimetres
    [SerializeField, Range(1, 5), FieldChangeCallback(nameof(SimScale))]
    public float simScale;
    [SerializeField,FieldChangeCallback(nameof(DisplayColor))]
    public Color displayColor = Color.cyan;
    [SerializeField,FieldChangeCallback(nameof(MaxMomentum))]
    public float maxMomentum = 10;
    public float MaxMomentum { get=>maxMomentum; set => maxMomentum = value; }
    [SerializeField,FieldChangeCallback(nameof(ParticleMomentum))]
    private float particleMomentum = 1;       // _ParticleK("pi*p/h", float) = 0.26179939
    private VRCPlayerApi player;
    private bool iamOwner = false;

    [Header("UI Elements")]
    [SerializeField] Toggle togPlay;
    [SerializeField] Toggle togPause;
    [SerializeField] Button btnReset;
    [SerializeField] Toggle togShowHide;

    /* 
     * Udon Sync Stuff
     */
    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    /*
     * Synced Properties
     */
    [SerializeField, UdonSynced,FieldChangeCallback(nameof(PlayParticles))] bool playParticles = true;
    public bool PlayParticles
    {
        get => playParticles;
        set
        {
            playParticles = value;
            if (togPlay != null && playParticles && !togPlay.isOn)
                    togPlay.isOn = true;
            setParticlePlay(playParticles);
            RequestSerialization();
        }
    }

    [SerializeField, UdonSynced,FieldChangeCallback(nameof(HideParticles))] bool hideParticles = false;
    public bool HideParticles
    {
        get => hideParticles;
        set
        {
            hideParticles = value;
            if (togShowHide != null && hideParticles && !togShowHide.isOn) 
                togShowHide.isOn = hideParticles;
            if (ihaveParticleFlow)
                particleMeshRend.enabled = !value;
            RequestSerialization();
        }
    }


    //[SerializeField]
    //private float particleK = 0.26179939f;       // _ParticleK("pi*p/h", float) = 0.26179939
    [SerializeField]
    bool crtUpdateRequired = false;
    [SerializeField]
    bool gratingUpdateRequired = false;
    [SerializeField]
    float simPixelScale = 1;

    private void setGratingParams(Material mat)
    {
        mat.SetFloat("_SlitCount", 1f*slitCount);
        mat.SetFloat("_SlitWidth", slitWidth * simPixelScale);
        mat.SetFloat("_SlitPitch", slitPitch * simPixelScale);
        mat.SetFloat("_Scale", simScale);
    }
    private void setParticleParams(Material mat)
    {
        mat.SetFloat("_SlitCount", 1f*slitCount);
        mat.SetFloat("_SlitWidth", slitWidth * metresPerUnit);
        mat.SetFloat("_SlitPitch", slitPitch * metresPerUnit);
        mat.SetFloat("_Scale", simScale);
    }

    private float shaderPauseTime = 0;
    private float shaderBaseTime = 0;
    private void setParticlePlay(bool play)
    {
        if (!ihaveParticleFlow)
            return;
        if (play)
        {
            shaderBaseTime = Time.time - shaderPauseTime;
            matParticleFlow.SetFloat("_BaseTime",shaderBaseTime);
            matParticleFlow.SetFloat("_Play", 1f);
            return;
        }
        shaderPauseTime = Time.time;
        matParticleFlow.SetFloat("_PauseTime", shaderPauseTime);
        matParticleFlow.SetFloat("_Play", 0f);
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
            if (ihaveParticleFlow) setParticleParams(matParticleFlow);
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
                matSim.SetFloat("_SlitCount", 1f*slitCount);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitCount", 1f* slitCount);
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
                matSim.SetFloat("_SlitWidth", slitWidth * simPixelScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitWidth", slitWidth * metresPerUnit);
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
                matSim.SetFloat("_SlitPitch", slitPitch * simPixelScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitPitch", slitPitch * metresPerUnit);

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
            //float particleP = particleMomentum / planckSim;
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
   [SerializeField]
    private float[] gratingFourierSq;
   [SerializeField]
    private float[] probIntegral;
   //[SerializeField]
    private float[] weightedLookup;
    [SerializeField]
    float pi_h;
    private void GenerateSamples()
    {
        if (gratingFourierSq == null || gratingFourierSq.Length < pointsWide)
        {
            gratingFourierSq = new float[pointsWide];
            probIntegral = new float[pointsWide+1];
        }
        float impulse;
        float prob;
        pi_h = Mathf.PI / planckSim;
        float probIntegralSum = 0;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxMomentum * i) / pointsWide;
            prob = sampleDistribution(impulse * pi_h);
            gratingFourierSq[i] = prob;
            probIntegral[i] = probIntegralSum;
            probIntegralSum += prob;
        }
        probIntegral[pointsWide] = probIntegralSum;
        // Scale (Normalize?) Integral to Width of Distribution for building inverse lookup;
        float normScale = (pointsWide-1) / probIntegral[pointsWide-1];
        for (int nPoint = 0; nPoint <= pointsWide; nPoint++)
            probIntegral[nPoint] *= normScale;
        //probIntegral[pointsWide] = pointsWide;
    }

    private void GenerateReverseLookup()
    {
        if (weightedLookup == null || weightedLookup.Length < pointsWide)
            weightedLookup = new float[pointsWide];
        // Scale prob distribution to be 0 to pointsWide at max;
        int indexAbove = 0;
        int indexBelow;
        float vmin;
        float vmax = 0;
        float frac;
        float val;
        int lim = pointsWide-1;
        float norm = maxMomentum / lim; 
        for (int i = 0; i <= lim; i++)
        {
            while ((vmax <= i) && (indexAbove <= lim))
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
            //Debug.Log(string.Format("i:{0}, ixAbove{1}, vmax:{2}, ixBelow:{3}, vmin{4}",i, indexAbove, vmax, indexBelow, vmin));
            if (indexBelow >= indexAbove)
                val = vmax;
            else
            {
                frac = Mathf.InverseLerp(vmin, vmax, i);
                val = Mathf.Lerp(indexBelow, indexAbove, frac);
            }
            weightedLookup[i] = val * norm;///lim;
        }
    }
    public bool CreateTextures()
    {
        simPixelScale = metresPerUnit * simPixels.y / simSize.y;

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
            float norm = 1f/(pointsWide - 1);
            for (int i = 0; i < pointsWide; i++)
            {
                float integral = probIntegral[i] * norm;
                float sample = gratingFourierSq[i];
                float reverse = weightedLookup[i];
                texData[i] = new Color(sample, integral, reverse, 1f);
            }
            var tex = new Texture2D(pointsWide, 1, TextureFormat.RGBAFloat, 0, true);
            tex.SetPixels(0, 0, pointsWide, 1, texData, 0);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            matParticleFlow.SetTexture(texName, tex);

            matParticleFlow.SetFloat("_MapMaxP", maxMomentum); // "Map max momentum", float ) = 1
        }
        Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    /* UI Stuff
     * 
     */
    // play/pause reset particle events
    public void simHide()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        if (togShowHide != null && togShowHide.isOn != hideParticles)
            HideParticles = !hideParticles;
    }
    public void simPlay()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        if (togPlay != null && !playParticles && togPlay.isOn)
            PlayParticles = true;
    }

    public void simPause()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        if (togPause != null && playParticles && togPause.isOn)
            PlayParticles = false;
    }


    /*
     * Update and Start
     */
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
        HideParticles = hideParticles;
    }

    void Start()
    {
        iHaveParticleCRT = simCRT != null;
        simPixelScale = metresPerUnit * simPixels.y / simSize.y;
        if (iHaveParticleCRT)
            matSim = simCRT.material;
        iHaveMatSim = ValidMaterial(matSim, texName);
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
        }
        ihaveParticleFlow = ValidMaterial(matParticleFlow, texName);
        ReviewOwnerShip();
        shaderBaseTime = Time.time;
        shaderPauseTime = Time.time;
        setParticlePlay(playParticles);
    }
}
