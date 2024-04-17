using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BallisticScatter : UdonSharpBehaviour
{
    [Header("Simulation Components")]
    [SerializeField,Tooltip("CRT to generate probability density")]
    CustomRenderTexture probabilityCRT;
    [SerializeField,Tooltip("Simulation Panel Dimensions")]
    Vector2 simSize = new Vector2(2.56f, 1.6f);
    [SerializeField,UdonSynced,FieldChangeCallback(nameof(ShowProbability))] 
    public bool showProbability = true;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(ProbVisPercent))]
    public float probVisPercent = 45f;
    [SerializeField]
    Vector2Int simPixels = new Vector2Int(1024, 640);
    bool iHaveProbability = false;
    [SerializeField,Tooltip("Shown for editor reference, loaded at Start")]
    Material matProbabilitySim = null;
    [SerializeField]
    string texName = "_MomentumMap";
    [SerializeField]
    bool iHavematProbabilitySim = false;
    [SerializeField]
    MeshRenderer particleMeshRend = null;
    [SerializeField]
    Material matParticleFlow = null;
    [SerializeField]
    bool ihaveParticleFlow = false;

    [Header("Scattering Configuration")]
    
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    //[SerializeField, Tooltip("Planck Value for simulation; lambda=h/p"),FieldChangeCallback(nameof(PlanckSim))]
    //public float planckSim = 12;

    [Header("Grating Configuration & Scale")]
    [SerializeField, UdonSynced,FieldChangeCallback(nameof(GratingOffset))] 
    public float gratingOffset = 0;
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
    [SerializeField,FieldChangeCallback(nameof(MaxParticleK))]
    public float maxParticleK = 10;
    public float MaxParticleK { get=>maxParticleK; set => maxParticleK = value; }
    [SerializeField,FieldChangeCallback(nameof(ParticleK))]
    private float particleK = 1;    
    private VRCPlayerApi player;
    private bool iamOwner = false;

    [Header("UI Elements")]
    [SerializeField] Toggle togPlay;
    [SerializeField] Toggle togPause;
    [SerializeField] Toggle togShowHide;
    [SerializeField] Toggle togProbability;
    [SerializeField] UdonSlider probVizSlider;

    private float prevVisibility = -1;
    private void reviewProbVisibility()
    {
        if (!iHavematProbabilitySim) 
            return;
        float targetViz = showProbability ? ProbVisPercent/10 : 0;
        if (targetViz == prevVisibility)
            return;
        prevVisibility = targetViz;
        matProbabilitySim.SetFloat("_Brightness", targetViz);
        crtUpdateRequired = true;
    }

    private bool ShowProbability
    {
        get=> showProbability;
        set
        {
            bool trig = value != showProbability;
            showProbability = value;
            if (togProbability != null && togProbability.isOn != value)
                togProbability.isOn = value;
            if (trig)
            {
                if (probVizSlider != null)
                    probVizSlider.IsInteractible = showProbability;
                reviewProbVisibility();
            }
            RequestSerialization();
        }
    }

    private float ProbVisPercent
    {
        get=> probVisPercent;
        set
        {
            bool trig = probVisPercent != value;
            probVisPercent = value;
            if (probVizSlider != null && !probVizSlider.PointerDown && probVizSlider.CurrentValue !=probVisPercent)
                probVizSlider.SetValue(probVisPercent);
            if (trig) 
                reviewProbVisibility();
            RequestSerialization();
        }
    }


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
    private void updatePlayPauseStop()
    {
        if (iamOwner)
            return;
        switch (particlePlayState)
        { 
            case 0:
                if (togPause != null && !togPause.isOn)
                    togPause.isOn = true;
                break;
            case 1:
                if (togPlay != null && !togPlay.isOn)
                    togPlay.isOn = true;
                break;
            default:
                if (togShowHide != null && !togShowHide.isOn)
                    togShowHide.isOn = true;
                break;
        }
    }

    [SerializeField, UdonSynced,FieldChangeCallback(nameof(ParticlePlayState))] int particlePlayState = 1;
    public int ParticlePlayState
    {
        get => particlePlayState;
        set
        {
            particlePlayState = value;
            setParticlePlay(value);
            updatePlayPauseStop();
            RequestSerialization();
        }
    }

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
        mat.SetFloat("_GratingOffset", gratingOffset);
    }
    private void setParticleParams(Material mat)
    {
        mat.SetFloat("_SlitCount", 1f*slitCount);
        mat.SetFloat("_SlitWidth", slitWidth);
        mat.SetFloat("_SlitPitch", slitPitch);
        mat.SetFloat("_Scale", simScale);
        mat.SetFloat("_GratingOffset", gratingOffset);
    }

    private float shaderPauseTime = 0;
    private float shaderBaseTime = 0;
    private bool shaderPlaying = false;
    private void setParticlePlay(int playState)
    {
        if (!ihaveParticleFlow)
            return;
        particleMeshRend.enabled = playState >= 0;
        switch (playState)
        {
            case 1:
                if (!shaderPlaying)
                {
                    shaderBaseTime = Time.time - shaderPauseTime;
                    matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
                    matParticleFlow.SetFloat("_Play", 1f);
                    shaderPlaying = true;
                }
                break;
            case 0:
                if (shaderPlaying)
                {
                    shaderPauseTime = Time.time;
                    matParticleFlow.SetFloat("_PauseTime", shaderPauseTime);
                    matParticleFlow.SetFloat("_Play", 0f);
                    shaderPlaying = false;
                }
                break;
            default: 
                return;
        }
    }

    public void SetGrating(int numSlits, float widthSlit, float pitchSlits, float momentumMax)
    {
        //Debug.Log(string.Format("{0} SetGrating: #slit1={1} width={2} pitch={3}", gameObject.name,  numSlits, widthSlit, pitchSlits));

        bool isChanged = numSlits != slitCount || widthSlit != slitWidth || pitchSlits != slitPitch;
        isChanged |= maxParticleK != momentumMax;
        slitCount = numSlits;
        maxParticleK = momentumMax;
        slitWidth = widthSlit;
        slitPitch = pitchSlits;
        gratingUpdateRequired = isChanged;
        if (isChanged)
        {
            if (iHavematProbabilitySim) setGratingParams(matProbabilitySim);
            if (ihaveParticleFlow) setParticleParams(matParticleFlow);
        }
    }

    
    private float GratingOffset
    {
        get=>gratingOffset;
        set
        {
            gratingOffset = value;
            Debug.Log("GratingOffset=" + value.ToString());
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_GratingOffset", gratingOffset*simPixelScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_GratingOffset", gratingOffset);
        }
    }
    
    private float SimScale
    {
        get => simScale;
        set
        {
            if (value != simScale)
                crtUpdateRequired = true;
            simScale = value;
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_Scale", simScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_Scale", simScale);
        }
    }

    float beamWidth = 1;
    private void UpdatebeamWidth()
    {
        beamWidth = Mathf.Max(slitCount-1,0)* slitPitch + slitWidth*1.3f;
        if (iHavematProbabilitySim)
            matProbabilitySim.SetFloat("_BeamWidth", beamWidth * simPixelScale);
        if (ihaveParticleFlow)
            matParticleFlow.SetFloat("_BeamWidth", beamWidth);
    }
    private int SlitCount
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
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_SlitCount", 1f*slitCount);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitCount", 1f* slitCount);
            UpdatebeamWidth();
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
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_SlitWidth", slitWidth * simPixelScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitWidth", slitWidth);
            UpdatebeamWidth();
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
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_SlitPitch", slitPitch * simPixelScale);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_SlitPitch", slitPitch);
            UpdatebeamWidth();
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


    public float ParticleK
    {
        get => particleK;
        set
        {
            crtUpdateRequired |= value != particleK;
            particleK = value;
            //float particleP = particleK / planckSim;
            if (iHavematProbabilitySim)
                matProbabilitySim.SetFloat("_ParticleP", particleK);
            if (ihaveParticleFlow)
                matParticleFlow.SetFloat("_ParticleP", particleK);
        }
    }

    public Color DisplayColor
    {
        get => displayColor;
        set
        {
            displayColor = value;
            if (iHaveProbability)
                matProbabilitySim.SetColor("_Color", displayColor);
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
   //[SerializeField]
    private float[] probIntegral;
   //[SerializeField]
    private float[] weightedLookup;
    private void GenerateSamples()
    {
        if (gratingFourierSq == null || gratingFourierSq.Length < pointsWide)
        {
            gratingFourierSq = new float[pointsWide];
            probIntegral = new float[pointsWide+1];
        }
        float impulse;
        float prob;
        float pi_h = Mathf.PI;// / planckSim;
        float probIntegralSum = 0;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxParticleK * i) / pointsWide;
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
        float norm = maxParticleK / lim; 
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
        simPixelScale = simPixels.y / simSize.y;

        GenerateSamples();

        Color[] texData = new Color[pointsWide + pointsWide];

        if (iHavematProbabilitySim)
        {
            var tex = new Texture2D(pointsWide * 2, 1, TextureFormat.RGBAFloat, 0, true);

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float impulse;
            for (int i = 0; i < pointsWide; i++)
            {
                impulse = (maxParticleK * i) / pointsWide;

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
            matProbabilitySim.SetFloat("_MapMaxP", maxParticleK); // "Map max momentum", float ) = 1
            matProbabilitySim.SetFloat("_MapMaxI", probIntegral[pointsWide-1]); // "Map Summed probability", float ) = 1
            tex.SetPixels(0, 0, pointsWide * 2, 1, texData, 0);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            matProbabilitySim.SetTexture(texName, tex);
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

            matParticleFlow.SetFloat("_MapMaxP", maxParticleK); // "Map max momentum", float ) = 1
        }
        //Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    /* UI Stuff
     * 
     */
    // play/pause reset particle events
    public void simHide()
    {
        if (togShowHide != null && togShowHide.isOn && particlePlayState >= 0)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            ParticlePlayState = -1;
        }
    }
    public void simPlay()
    {
        if (togPlay != null && togPlay.isOn && particlePlayState <= 0)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            ParticlePlayState = 1;
        }
    }

    public void simPause()
    {
        if (togPause != null && togPause.isOn && particlePlayState != 0)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            ParticlePlayState = 0;
        }
    }

    public void probVisPtr()
    {
        if (!iamOwner)
            Networking.SetOwner(player,gameObject);
        //Debug.Log("probVisPtr");
    }
    public void showProb()
    {
       // Debug.Log("showProb");
        if ((togProbability != null) && togProbability.isOn != showProbability)
        {
            if (!iamOwner)
                Networking.SetOwner(player,gameObject);
            ShowProbability = !showProbability;
        }
    }

    /*
     * Update and Start
     */
    private float updateTimer = 1;
    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0)
            return;
        if (gratingUpdateRequired)
        {
            CreateTextures();
            crtUpdateRequired = true;
            gratingUpdateRequired = false;
            updateTimer += 0.05f;
        }
        else
            updateTimer += 0.01f;
        if (crtUpdateRequired)
        {
            crtUpdateRequired = false;
            if (iHaveProbability)
                probabilityCRT.Update(1);
        }
    }

    void Start()
    {
        iHaveProbability = probabilityCRT != null;
        simPixelScale = simPixels.y / simSize.y;
        if (iHaveProbability)
            matProbabilitySim = probabilityCRT.material;
        iHavematProbabilitySim = ValidMaterial(matProbabilitySim, texName);
        ProbVisPercent = probVisPercent;
        ShowProbability = showProbability;
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
        }
        ihaveParticleFlow = ValidMaterial(matParticleFlow, texName);
        SlitCount = slitCount;
        SlitWidth = slitWidth;
        SlitPitch = slitPitch;
        ProbVisPercent = probVisPercent;
        GratingOffset = gratingOffset;
        ParticleK = particleK;
        ReviewOwnerShip();
        shaderBaseTime = Time.time;
        shaderPauseTime = Time.time;
        setParticlePlay(particlePlayState);
    }
}
