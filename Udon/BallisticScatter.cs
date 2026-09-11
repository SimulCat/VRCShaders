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
    Vector3 simSize = new Vector3(2.56f, 0.1f, 1.6f);
    [SerializeField,FieldChangeCallback(nameof(ShowProbability))] 
    public bool showProbability = true;
    [SerializeField, FieldChangeCallback(nameof(ProbVisPercent))]
    private float probVisPercent = 45f;
    [SerializeField]
    Vector2Int simPixels = new Vector2Int(1280, 640);
    
    [SerializeField]
    string texName = "_MomentumMap";
    [SerializeField]
    MeshRenderer particleMeshRend = null;
    [SerializeField, FieldChangeCallback(nameof(Visibility))]
    private float visibility = 1;

    [Header("Scattering Configuration")]
    
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;

    [Header("Grating Configuration & Scale")]
    [SerializeField, UdonSynced,FieldChangeCallback(nameof(GratingOffset))] 
    public float gratingOffset = 0;
    [SerializeField,Range(1,17),FieldChangeCallback(nameof(SlitCount))]
    private int slitCount = 2;          // _SlitCount("Num Sources", float)
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    public float slitPitch = 45f;        // "Slit Pitch" millimetre
    [SerializeField]
    private float slitPitchMin = 1f;
    [SerializeField,FieldChangeCallback(nameof(SlitWidth))]
    public float slitWidth = 12f;        // "Slit Width" millimetres
// Pulsed particles and speed range
    [SerializeField, FieldChangeCallback(nameof(PulseParticles))]
    private bool pulseParticles = false;

    [SerializeField, Range(0.01f, 1.5f), FieldChangeCallback(nameof(PulseWidth))]
    public float pulseWidth = 1f;        // particle Pulse width
    [SerializeField, Range(0,50), FieldChangeCallback(nameof(SpeedRange))]
    public float speedRange = 10f;        // Speed Range Percent

    [SerializeField, Range(0.1f, 10), FieldChangeCallback(nameof(WorldScale))]
    public float worldScale = 1f;        // Environment Scale

    [SerializeField, Range(1, 10), FieldChangeCallback(nameof(SimScale))]
    public float simScale;
    [SerializeField, Tooltip("Exaggerate/Suppress Beam Particle Size"), Range(0.1f, 5f), FieldChangeCallback(nameof(ParticleSize))] float particleSize = 1;
    [SerializeField] private float maxParticleSize = 1;
    [SerializeField,FieldChangeCallback(nameof(DisplayColour))]
    public Color displayColour = Color.cyan;
    [SerializeField,FieldChangeCallback(nameof(MaxParticleP))]
    public float maxParticleP = 10;
    [SerializeField, FieldChangeCallback(nameof(MinParticleP))]
    public float minParticleP = 1;
    [SerializeField] bool updateColour = false;

    private float Visibility
    {
        get => visibility;
        set
        {
            visibility = Mathf.Clamp01(value);
            reviewProbVisibility();
        }
    }

    public float MaxParticleP 
    {   get=>maxParticleP; 
        set 
        {
            if (MaxParticleP == value)
                return;
            maxParticleP = value;
            SetColour(); 
        } 
    }
    public float MinParticleP 
    { 
        get => minParticleP;
        set
        {
            if (minParticleP == value)
                return;
            minParticleP = value;
            SetColour();
        }
    }

    [SerializeField,FieldChangeCallback(nameof(ParticleP))]
    private float particleP = 1;

    [Header("UI Elements")]
    [SerializeField] UdonToggleGroup togPlayPauseStop;
    [SerializeField] SyncedToggle togProbability;
    [SerializeField] SyncedToggle togPulseParticles;
    [SerializeField] UdonSlider particleSizeSlider;
    [SerializeField] UdonSlider probVizSlider;
    [SerializeField] UdonSlider pulseWidthSlider;
    [SerializeField] UdonSlider speedRangeSlider;
    [SerializeField] UdonSlider particlePslider;
    // Slit Configuration
    [SerializeField] SyncedIncDec slitCountIncDec;
    [SerializeField] UdonSlider slitWidthSlider;
    [SerializeField] UdonSlider slitPitchSlider;

    [Header("For tracking in Editor")]
    //[SerializeField, Tooltip("Shown for editor reference, loaded at Start")]
    private Material matProbabilitySim = null;
    //[SerializeField]
    private Material matParticleFlow = null;

    //[SerializeField]
    bool iHaveProbability = false;
    //[SerializeField]
    bool iHaveProbSimMat = false;
    //[SerializeField]
    private float shaderPauseTime = 0;
    //[SerializeField]
    private float shaderBaseTime = 0;
    [SerializeField]
    private bool shaderPlaying = false;
    private VRCPlayerApi player;
    private bool iamOwner = false;


    private float prevVisibility = -1;
    private void reviewProbVisibility()
    {
        if (!iHaveProbSimMat) 
            return;
        float targetViz = visibility * (showProbability ? ProbVisPercent/10 : 0);
        if (targetViz == prevVisibility)
            return;
        prevVisibility = targetViz;
        matProbabilitySim.SetFloat("_Visibility", targetViz);
        crtUpdateRequired = true;
    }

    private void reviewPulse()
    {
        if (matParticleFlow == null)
            return;
        if (pulseWidthSlider != null)
            pulseWidthSlider.Interactable = pulseParticles;
        float width = pulseParticles ? pulseWidth : -1f;
        matParticleFlow.SetFloat("_PulseWidth", width);
    }
    private bool ShowProbability
    {
        get=> showProbability;
        set
        {
            bool chg = showProbability != value;
            showProbability = value;
            if (probVizSlider != null)
                probVizSlider.Interactable = showProbability;
            if (chg)
                reviewProbVisibility();
        }
    }

    private bool PulseParticles
    {
        get => pulseParticles;
        set
        {
            bool chg = pulseParticles != value;
            pulseParticles = value;
            if (chg) 
                reviewPulse();
        }
    }

    private float ProbVisPercent
    {
        get=> probVisPercent;
        set
        {
            //Debug.Log("ProbvizPct :"+ value);
            probVisPercent = value;
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

    [SerializeField, FieldChangeCallback(nameof(ParticlePlayState))] int particlePlayState = 1;
    public int ParticlePlayState
    {
        get => particlePlayState;
        set
        {
            particlePlayState = value;
            setParticlePlay(particlePlayState);
        }
    }

    //[SerializeField]
    bool crtUpdateRequired = false;
   // [SerializeField]
    bool experimentUpdateRequired = false;
   // [SerializeField]
    float simPixelScale = 1;

    private void setGratingParams(Material mat)
    {
        mat.SetInteger("_SlitCount", slitCount);
        mat.SetFloat("_SlitWidth", slitWidth * simPixelScale);
        mat.SetFloat("_SlitPitch", slitPitch * simPixelScale);
        mat.SetFloat("_Scale", simScale);
        mat.SetFloat("_GratingDistance", gratingOffset);

    }
    private void setParticleParams(Material mat)
    {
        mat.SetInteger("_SlitCount", slitCount);
        mat.SetFloat("_SlitWidth", slitWidth);
        mat.SetFloat("_SlitPitch", slitPitch);
        mat.SetFloat("_Scale", simScale);
        mat.SetFloat("_GratingDistance", gratingOffset);
        Vector4 wallLimits = new Vector4(simSize.x, simSize.y / 2f, simSize.z / 2f, 0f);
        mat.SetVector("_WallLimits", wallLimits);
        mat.SetFloat("_SpeedRange", speedRange / 100f);
        mat.SetFloat("_ParticleP", particleP);
        mat.SetFloat("_MaxParticleP", maxParticleP);
        mat.SetFloat("_MinParticleP", minParticleP);
    }

    private void initParticlePlay(Material mat)
    {
        shaderBaseTime = 0;
        shaderPauseTime = 0;
        matParticleFlow.SetFloat("_PauseTime", 0f);
        matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
        matParticleFlow.SetInteger("_Play", 1);
        shaderPlaying = true;
        //Debug.Log("Init");
    }
    private void setParticlePlay(int playState)
    {
        if (particleMeshRend == null)
            return;
        particleMeshRend.enabled = (playState >= 0 && playState < 2);
        switch (playState)
        {
            case 1:
                if (!shaderPlaying)
                {
                    shaderBaseTime += Time.timeSinceLevelLoad - shaderPauseTime;
                    matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
                    matParticleFlow.SetInteger("_Play", 1);
                    shaderPlaying = true;
                    //Debug.Log("Play");
                }
                break;
            case 0:
                if (shaderPlaying)
                {
                    shaderPauseTime = Time.timeSinceLevelLoad;
                    matParticleFlow.SetFloat("_PauseTime", shaderPauseTime);
                    matParticleFlow.SetInteger("_Play", 0);
                    shaderPlaying = false;
                    //Debug.Log("Pause");
                }
                break;
            default: 
                return;
        }
    }

    private float ScreenDistance
    {
        get => simSize.x;
        set
        {
            simSize.x = value;
            simPixelScale = simPixels.x / simSize.x;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_ScreenDistance", simSize.x * simPixelScale);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ScreenDistance", simSize.x);
        }
    }

    
    private float GratingOffset
    {
        get=>gratingOffset;
        set
        {
            gratingOffset = value;
            //Debug.Log("GratingOffset=" + value.ToString());
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_GratingDistance", gratingOffset*simPixelScale);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_GratingDistance", gratingOffset);
        }
    }

    private void checkMarkerSizes()
    {
        if (matParticleFlow == null)
            return;
        matParticleFlow.SetFloat("_MarkerScale", particleSize * worldScale);
    }
    private float ParticleSize
    {
        get => particleSize;
        set
        {
            value = Mathf.Clamp(value, 0.1f, 5.0f);
            particleSize = value;
            checkMarkerSizes();
        }
    }


    private float WorldScale
    {
        get => worldScale;
        set
        {
            if (value != worldScale)
                crtUpdateRequired = true;
            worldScale = value;
            checkMarkerSizes();
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
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_Scale", simScale);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_Scale", simScale);
        }
    }

    float beamWidth = 1;
    private void UpdatebeamWidth()
    {
        beamWidth = Mathf.Max(slitCount-1,0)* slitPitch + slitWidth*1.3f;
        if (iHaveProbSimMat)
            matProbabilitySim.SetFloat("_BeamWidth", beamWidth * simPixelScale);
        if (matParticleFlow != null)
            matParticleFlow.SetFloat("_BeamWidth", beamWidth);
    }
    private int SlitCount
    {
        get => slitCount;
        set
        {
            if (value != slitCount)
            {
                experimentUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitCount = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetInteger("_SlitCount", slitCount);
            if (matParticleFlow)
                matParticleFlow.SetInteger("_SlitCount", slitCount);
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
                experimentUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitWidth = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_SlitWidth", slitWidth * simPixelScale);
            if (matParticleFlow)
                matParticleFlow.SetFloat("_SlitWidth", slitWidth);
            UpdatebeamWidth();
        }
    }

    public float PulseWidth
    {
        get => pulseWidth;
        set
        {
            value = Mathf.Clamp(value, 0.1f, 2f);
            bool chg = value != pulseWidth;
            pulseWidth = value;
            if (chg)
                reviewPulse();
        }
    }

    public float SpeedRange
    {
        get => speedRange;
        set
        {
           speedRange = Mathf.Clamp(value,0,50);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_SpeedRange", value / 100f);
        }
    }

    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            if (value != slitPitch)
            {
                experimentUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitPitch = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_SlitPitch", slitPitch * simPixelScale);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_SlitPitch", slitPitch);
            UpdatebeamWidth();
        }
    }

    private Color spectrumColour(float wavelength, float gamma = 0.8f)
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

    private void SetColour()
    {
        if (!updateColour)
            return;
        float frac = Mathf.InverseLerp(minParticleP, maxParticleP, particleP);
        Color dColour = spectrumColour(Mathf.Lerp(725, 380, frac));
        DisplayColour = dColour;
    }


    public float ParticleP
    {
        get => particleP;
        set
        {
            crtUpdateRequired = true;
            particleP = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_ParticleP", particleP);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ParticleP", particleP);
            SetColour();
        }
    }

    private Color DisplayColour
    {
        get => displayColour;
        set
        {
            displayColour = value;
            if (iHaveProbability)
                matProbabilitySim.SetColor("_Color", displayColour);
        }
    }

    private bool hasMaterialWithProperty(Material theMaterial, string thePropertyName)
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
            impulse = (maxParticleP * i) / pointsWide;
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
        float norm = maxParticleP / lim; 
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
        simPixelScale = simPixels.x / simSize.x;

        GenerateSamples();

        Color[] texData = new Color[pointsWide + pointsWide];

        if (iHaveProbSimMat)
        {
            var tex = new Texture2D(pointsWide * 2, 1, TextureFormat.RGBAFloat, 0, true);

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float impulse;
            for (int i = 0; i < pointsWide; i++)
            {
                impulse = (maxParticleP * i) / pointsWide;

                float sample = gratingFourierSq[i];
                float integral = probIntegral[i];
                texData[pointsWide + i] = new Color(sample, integral, impulse, 1f);
                texData[pointsWide - i] = new Color(sample, -integral, -impulse, 1f);
            }
            matProbabilitySim.SetFloat("_MapMaxP", maxParticleP); // "Map max momentum", float ) = 1
            matProbabilitySim.SetFloat("_MapMaxI", probIntegral[pointsWide - 1]); // "Map Summed probability", float ) = 1
            texData[0] = new Color(0, -probIntegral[pointsWide-1], -1, 1f);

            // Normalize
            //float total = texData[pointsWide-1].g;
            //for (int i = 0;i < pointsWide; i++)
            //    texData[i].g /= total;
            tex.SetPixels(0, 0, pointsWide * 2, 1, texData, 0);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            matProbabilitySim.SetTexture(texName, tex);
        }
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
            if (matParticleFlow == null)
                return false;
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

            matParticleFlow.SetFloat("_MapMaxP", maxParticleP); // "Map max momentum", float ) = 1
        }
        //Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    
    /*
     * Update and Start
     */
    private float updateTimer = 1;
    private bool init = false;
    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0)
            return;
        if (!init)
        {
            if (particleMeshRend != null)
            {
                matParticleFlow = particleMeshRend.material;
                if (matParticleFlow != null)
                {
                    init = true;
                    initParticlePlay(matParticleFlow);
                    setGratingParams(matParticleFlow);
                    setParticleParams(matParticleFlow);
                    CreateTextures();
                    UpdatebeamWidth();
                }
            }
            init = true;
            ParticleP = particleP;
            DisplayColour = displayColour;
            return;
        }
        if (experimentUpdateRequired)
        {
            CreateTextures();
            crtUpdateRequired = true;
            experimentUpdateRequired = false;
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

    void OnEnable()
    {
        iHaveProbability = probabilityCRT != null;
        if (particleMeshRend != null)
            matParticleFlow = particleMeshRend.material;
        if (iHaveProbability)
            matProbabilitySim = probabilityCRT.material;
        if (slitCountIncDec != null)
        {
            slitCountIncDec.SetLimits(1, 17);
            slitCountIncDec.SetValueWithoutNotification(slitCount);
            slitCountIncDec.clientVariableName = "slitCount";
        }
        if (togProbability != null)
        {
            togProbability.IsBoolean = true;
            togProbability.ClientVariableName = "showProbability";
            togProbability.setState(showProbability);
        }
        if (togPulseParticles != null)
        {
            togPulseParticles.IsBoolean = true;
            togPulseParticles.ClientVariableName = "pulseParticles";
            togPulseParticles.setState(pulseParticles);
        }
        if (particlePslider != null)
        {
            particlePslider.SetLimits(minParticleP, maxParticleP);
            particlePslider.SetValue(particleP);
        }
        if (particleSizeSlider != null)
        {
            particleSize = Mathf.Clamp(particleSize, 0.1f, maxParticleSize);
            particleSizeSlider.SetLimits(0.1f, maxParticleSize);
            particleSizeSlider.SetValue(particleSize);
            particleSizeSlider.ClientVariableName = "particleSize";
        }
        if (speedRangeSlider != null)
        {
            speedRangeSlider.SetLimits(0, 50);
            speedRangeSlider.SetValue(speedRange);
        }
        if (pulseWidthSlider != null)
        {
            pulseWidthSlider.SetLimits(0.1f, 1.5f);
            pulseWidthSlider.ClientVariableName = "pulseWidth";
            pulseWidthSlider.SetValue(pulseWidth);
            pulseWidthSlider.Interactable = pulseParticles;
        }
        if (probVizSlider != null)
        {
            probVizSlider.SetLimits(1.5f, 60);
            probVizSlider.ClientVariableName = "probVisPercent";
            probVizSlider.SetValue(probVisPercent);
            probVizSlider.Interactable = showProbability;
        }
        if (slitWidthSlider != null)
        {
            slitWidthSlider.SetLimits(slitPitchMin* 0.2f, slitPitchMin*0.9f);
            slitWidthSlider.ClientVariableName = "slitWidth";
            slitWidthSlider.SetValue(slitWidth);
        }
        if (slitPitchSlider != null)
        {
            slitPitchSlider.SetLimits(slitPitchMin,slitPitchMin*5f);
            slitPitchSlider.ClientVariableName = "slitPitch";
            slitPitchSlider.SetValue(slitPitch);
        }
    }
    void Start()
    {
        player = Networking.LocalPlayer;
        if (particleMeshRend != null)
            matParticleFlow = particleMeshRend.material;
        ReviewOwnerShip();
        simPixelScale = simPixels.x / simSize.x;
        if (iHaveProbability)
            matProbabilitySim = probabilityCRT.material;
        iHaveProbSimMat = hasMaterialWithProperty(matProbabilitySim, texName);
        ShowProbability = showProbability;
        SlitCount = slitCount;
        SlitWidth = slitWidth;
        SlitPitch = slitPitch;
        SimScale = simScale;
        SpeedRange = speedRange;
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        Visibility = visibility;
        ProbVisPercent = probVisPercent;
        if (probVizSlider != null)
            probVizSlider.SetValue(probVisPercent);
        reviewPulse();
        GratingOffset = gratingOffset;
        ScreenDistance = simSize.x;
        ParticleP = particleP;
        crtUpdateRequired = true;
        //Debug.Log("BScatter Started");
    }
}
