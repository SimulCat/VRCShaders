using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class GhostInterference : UdonSharpBehaviour
{
    [Header("Simulation Components")]
    [SerializeField]
    GratingModel slitModel;

    [Header("GPU Billboard Particles")]
    [SerializeField] private MeshRenderer particleMeshRend = null;
    [SerializeField] private string texName = "_MomentumMap";

    [Header("Scattering Configuration")]
    [SerializeField, Tooltip("Experiment Mode"), FieldChangeCallback(nameof(ExperimentMode))]
    private int experimentMode = 0;
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    [SerializeField, Tooltip("Planck's constant multiplier for momentum scaling"), Range(1, 10000)]
    private int planckMultiplier = 1;
    [SerializeField, FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;
    [Header("Fundamental Constants")]
    // [SerializeField]
    bool experimentUpdateRequired = false;
    bool scatterUpdateRequired = false;

    // Planck's constant h in Joule seconds
    [SerializeField]
    private float _h = 6.62607015e-10f; // 
    [SerializeField]
    private const float AMU_ToKg = 1.66054e-27f;
    private const float halfPi = 1.57079632679489661f;

    [Header("Grating Configuration & Scale")]
    [SerializeField, Tooltip("Scale from UI scale 0-1 to range in mm (1 mm is 0.001 Unity units)")]
    float slitUnitScale = 1e-3f; // Default to millimetres for UI display, but actual scale applied to model is in metres, so 1e6 for microns
    [SerializeField]
    string gratingUIUnits = "μm";
    //[SerializeField, Tooltip("Distance from source to grating")]
    //private float gratingDistance = 0;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount)), Range(1, 17)]
    private int slitCount = 2;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch = 0.0027f;        // Slit Pitch
    private float slitPitchSI => slitPitch * slitUnitScale;
    [SerializeField, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth = 0.006f;        // "Slit Width
    private float slitWidthSI => slitWidth * slitUnitScale;

    // Pulsed particles and speed range
    [SerializeField, FieldChangeCallback(nameof(PulseParticles))]
    private bool pulseParticles = false;
    [SerializeField, FieldChangeCallback(nameof(PulseWidth)), Range(0.01f, 1.5f)]
    private float pulseWidth = 1f;        // particle Pulse width

    [SerializeField, Range(0, 17)] private int MAX_SLITS = 7;

    [SerializeField, Tooltip("Simulation Momentum default ronto newton-seconds 1e-27")]
    float planckToSimMomentum = 1.0e27f;
    [SerializeField]
    float modelScale = 10f;
    [SerializeField, FieldChangeCallback(nameof(LaserParticleP))]
    float laserParticleP = 1.89f;

    [SerializeField]
    private float maxParticleP = 2f;
    [SerializeField]
    private float minParticleP = 7.64f;
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI planckLabel;
    [SerializeField] private TextMeshProUGUI planckScaleLabel;

    [SerializeField] private UdonToggleGroup togGroupPlayPause = null;
    [SerializeField, Tooltip("Particle demo state"), FieldChangeCallback(nameof(ParticlePlayState))]
    private int particlePlayState = 1;
    [SerializeField] private SyncedToggle togPulseParticles;

    [SerializeField] private UdonSlider pulseWidthSlider;
    [SerializeField] private UdonSlider momentumSlider;

    [SerializeField] private UdonSlider slitWidthSlider;
    [SerializeField] private UdonSlider slitPitchSlider;

    [Tooltip("Exaggerate/Suppress Beam Particle Size"), SerializeField, Range(0.01f, .5f), FieldChangeCallback(nameof(ParticleSize))] float particleSize = 0.15f;
    public UdonSlider particleSizeSlider;

    private float ParticleSize
    {
        get => particleSize;
        set
        {
            if (matGhostParticles != null)
                matGhostParticles.SetFloat("_MarkerScale", value);
            particleSize = value;
        }
    }

    [SerializeField] private TextMeshProUGUI txtSlitCountDisplay;
    
    //[Header("For tracking in Editor")]
    //[SerializeField, Tooltip("Shown for editor reference, loaded at Start")]
    [SerializeField]
    private Material matGhostParticles = null;

    //[SerializeField]
    private float shaderPauseTime = 0;
    //[SerializeField]
    private float shaderBaseTime = 0;
    //[SerializeField]
    private bool shaderPlaying = true;
    private VRCPlayerApi player;
    private bool iamOwner = false;
    [SerializeField]
    private Vector3[] slitWidthMinMaxNominal = new[] {
        new Vector3(100f,300f,150f), new Vector3(100f,300f,150f) 
    };
    [SerializeField]
    private Vector3[] slitPitchMinMaxNominal = new[] {
        new Vector3(350f, 550f, 450f), new Vector3(350f, 550f, 450f)
    };

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

    public bool UseQuantumScatter
    {
        get => useQuantumScatter;
        set
        {
            useQuantumScatter = value;
            if (matGhostParticles != null)
                matGhostParticles.SetInteger("_UseQuantumScatter", useQuantumScatter ? 1 : 0);
        }
    }

    private void UpdateLabels()
    {
        if (planckLabel != null)
            planckLabel.text = string.Format("h={0:#.##e+0}", _h);
    }

    private void configureExperiment(int mode)
    {
        // Update momentum and Planck units
        //Debug.Log($"ConfigureExperiment mode {mode}");
        switch (experimentMode)
        {
            default:
            case 0:
                mode=0;
                modelScale = 10f;
                slitUnitScale = 1e-6f;
                gratingUIUnits = "μm";
                break;
                // Handle mode change
        }
        if (slitWidthSlider != null)
        {
            slitWidthSlider.SliderUnit = gratingUIUnits;
            slitWidthSlider.DisplayInteger = true;
            slitWidthSlider.DisplayScale = 1; // Slit engineering units, e.g. microns
            slitWidthSlider.SetLimits(slitWidthMinMaxNominal[mode].x, slitWidthMinMaxNominal[mode].y);
            slitWidthSlider.SetValue(slitWidthMinMaxNominal[mode].z);
        }
        SlitWidth = slitWidthMinMaxNominal[mode].z;
        if (slitPitchSlider != null)
        {
            slitPitchSlider.SliderUnit = gratingUIUnits;
            slitPitchSlider.DisplayInteger = true;
            slitPitchSlider.DisplayScale = 1; // Display in engineering units microns
            slitPitchSlider.SetLimits(slitPitchMinMaxNominal[mode].x, slitPitchMinMaxNominal[mode].y);
            slitPitchSlider.SetValue(slitPitchMinMaxNominal[mode].z);
        }
        SlitPitch = slitPitchMinMaxNominal[mode].z;
        //Debug.Log($"ConfigureExperiment applied mode {mode} slitPitch={SlitPitch} slitWidth={SlitWidth}");
    }
    public int ExperimentMode
    {
        get => experimentMode;
        set
        {
            if (value != experimentMode)
            {
                experimentMode = value;
            }
            configureExperiment(experimentMode);
        }
    }

    private int ParticlePlayState
    {
        get => particlePlayState;
        set
        {
            particlePlayState = value;
            setParticlePlay(particlePlayState);
        }
    }

    public void incSlits()
    {
        if (slitCount < MAX_SLITS)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            SlitCount = slitCount + 1;
        }
    }
    public void decSlits()
    {
        if (slitCount > 1)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            SlitCount = slitCount - 1;
        }
    }

    private void reviewPulse()
    {
        if (matGhostParticles == null)
            return;
        float width = pulseParticles ? pulseWidth : -1f;
        matGhostParticles.SetFloat("_PulseWidth", width);

    }
    public bool PulseParticles
    {
        get => pulseParticles;
        set
        {
            //Debug.Log($"pulse Event {value}");
            bool isChanged = pulseParticles != value;
            pulseParticles = value;
            if (pulseWidthSlider != null)
                pulseWidthSlider.Interactable = pulseParticles;
            if (isChanged)
                reviewPulse();
        }
    }

    // [SerializeField]

    private void initParticlePlay()
    {
        shaderBaseTime = Time.time;
        shaderPauseTime = shaderBaseTime;
        shaderPlaying = particlePlayState == 1;
        if (matGhostParticles != null)
        {
            matGhostParticles = particleMeshRend.material;
            particleMeshRend.enabled = (particlePlayState > 0);

            matGhostParticles.SetFloat("_PauseTime", shaderPauseTime);
            matGhostParticles.SetFloat("_BaseTime", shaderBaseTime);
            matGhostParticles.SetInteger("_Play", ParticlePlayState);
            matGhostParticles.SetFloat("_MarkerScale", particleSize);
        }

        shaderPlaying = true;
        //Debug.Log("Init");
    }


    private void setParticlePlay(int playState)
    {
        if (matGhostParticles == null)
            return;
        particleMeshRend.enabled = playState >= 0;
        switch (playState)
        {
            case 1: // PlayState.Playing:
                if (!shaderPlaying)
                {
                    shaderBaseTime += Time.timeSinceLevelLoad - shaderPauseTime;
                    matGhostParticles.SetFloat("_BaseTime", shaderBaseTime);
                    matGhostParticles.SetInteger("_Play", 1);
                    shaderPlaying = true;
                    //Debug.Log("Play");
                }
                break;
            case 0: // PlayState.Paused:
                if (shaderPlaying)
                {
                    shaderPauseTime = Time.timeSinceLevelLoad;
                    matGhostParticles.SetFloat("_PauseTime", shaderPauseTime);
                    shaderPlaying = false;
                    //Debug.Log("Pause");
                }
                matGhostParticles.SetInteger("_Play", 0);
                break;
            case 2: // PlayState Stopped at Limit:
                if (shaderPlaying)
                {
                    shaderPauseTime = Time.timeSinceLevelLoad;
                    matGhostParticles.SetFloat("_PauseTime", shaderPauseTime);
                    shaderPlaying = false;
                    //Debug.Log("Stop");
                }
                matGhostParticles.SetInteger("_Play", -1);
                break;
            default:
                return;
        }
    }

    // Nominal Particle Momentum   
    public float LaserParticleP
    {
        get => laserParticleP;
        set
        {
            if (value != laserParticleP)
            {
                experimentUpdateRequired = true;
            }
            laserParticleP = value;
            if (matGhostParticles != null)
            {
                matGhostParticles.SetFloat("_LaserP", laserParticleP);
            }
            if (momentumSlider != null)
            {
                momentumSlider.DisplayInteger = (value >= 10);
            }
        }
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            value = Mathf.Clamp(value, 1, MAX_SLITS);
            if (value != slitCount)
            {
                scatterUpdateRequired = true;
            }
            slitCount = value;
            if (txtSlitCountDisplay != null)
                txtSlitCountDisplay.text = slitCount.ToString();
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            if (value != slitWidth)
            {
                scatterUpdateRequired = true;
            }
            slitWidth = value;
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

    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            if (value != slitPitch)
            {
                scatterUpdateRequired = true;
            }
            slitPitch = value;
        }
    }

    private float sampleDistribution(float spatialK, int apertureCount, float apertureWidth, float aperturePitch)
    {

        float slitPhase = spatialK * apertureWidth;
        float apertureProb = 0;
        if (apertureCount <= 0)
        {
            if (Mathf.Abs(slitPhase) < halfPi)
            {
                apertureProb = Mathf.Cos(slitPhase);
                apertureProb *= apertureProb;
            }
        }
        else
        {
            apertureProb = Mathf.Abs(slitPhase) > 0.000001f ? Mathf.Sin(slitPhase) / slitPhase : 1.0f;
            apertureProb *= apertureProb;
        }
        float multiSlitProb = 1f;
        if (apertureCount > 1)
        {
            float gratingPhase = spatialK * aperturePitch;
            if (apertureCount == 2)
                multiSlitProb = Mathf.Cos(gratingPhase) * 2;
            else
            {
                float sinGrPhase = Mathf.Sin(gratingPhase);
                multiSlitProb = (Mathf.Abs(sinGrPhase) < 0.000001f) ? apertureCount : Mathf.Sin(apertureCount * gratingPhase) / sinGrPhase;
            }
            multiSlitProb *= multiSlitProb;
        }
        return multiSlitProb * apertureProb;
    }
    [SerializeField]
    private float[] gratingFourierSq;
    [SerializeField]
    private float[] probIntegral;
    [SerializeField]
    private float[] weightedLookup;
    private float GenerateSamples(int apertureCount, float apertureWidthSI, float aperturePitchSI, float maxP)
    {
        if (apertureCount < 1)
            apertureWidthSI = aperturePitchSI;
        if (gratingFourierSq == null || gratingFourierSq.Length < pointsWide)
        {
            gratingFourierSq = new float[pointsWide];
            probIntegral = new float[pointsWide + 1];
        }
        // Check the width of the distribution
        float impulse;
        float prob;
        float planckScaled = _h * planckMultiplier* planckToSimMomentum;
        float pi_div_h = Mathf.PI / planckScaled; //  
        //Debug.Log($"{gameObject.name} generateSamples: planckScaled={planckScaled} maxP={maxP} pi_div_h {pi_div_h} s={apertureCount} widthSI={apertureWidthSI} pitchSI={aperturePitchSI}");
        float probIntegralSum = 0;
        //float maxDistributionP = (10 * planckScaled) / apertureWidthSI;
        //maxDistributionP = maxDistributionP < maxP ? maxDistributionP : maxP;
        //Debug.Log($"Set maxDistributionP={maxDistributionP}");
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxP * i) / pointsWide;
            prob = sampleDistribution(impulse * pi_div_h, apertureCount, apertureWidthSI, aperturePitchSI);
            gratingFourierSq[i] = prob;
            probIntegral[i] = probIntegralSum;
            probIntegralSum += prob;
        }
        probIntegral[pointsWide] = probIntegralSum;
        // Scale (Normalize?) Integral to Width of Distribution for building inverse lookup;
        float normScale = (pointsWide - 1) / probIntegral[pointsWide - 1];
        for (int nPoint = 0; nPoint <= pointsWide; nPoint++)
            probIntegral[nPoint] *= normScale;
        return maxP;
    }

    private void GenerateReverseLookup(float maxP)
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
        int lim = pointsWide - 1;
        float norm = maxP / lim;
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
            if (indexBelow >= indexAbove)
                val = vmax;
            else
            {
                frac = Mathf.InverseLerp(vmin, vmax, i);
                val = Mathf.Lerp(indexBelow, indexAbove, frac);
            }
            weightedLookup[i] = val * norm;
        }
    }

    public void CopyTexToShaders(string TexKeyword, string MaxPKeyword, float mapMaxP)
    {
        Color[] texData = new Color[pointsWide + pointsWide];
        //Debug.Log($"{gameObject.name} CopyTexToShaders {MaxPKeyword}={mapMaxP}");
        if (matGhostParticles != null)
        {
            float norm = 1f / (pointsWide - 1);

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
            matGhostParticles.SetTexture(TexKeyword, tex);
            matGhostParticles.SetFloat(MaxPKeyword, mapMaxP); // "Map max momentum", float ) = 1
        }
    }

    public bool CreateTextures()
    {
        if (matGhostParticles != null)
        {
            float maxP = maxParticleP * 0.1f;
            // Load X and Y scattering function lookups into shader 
            if (scatterUpdateRequired)
            {
                float hMaxP = GenerateSamples(slitCount, slitWidthSI, slitPitchSI, maxP);
                //Debug.Log($"{gameObject.name} CreateTextures horiz: hMaxP={hMaxP}");
                GenerateReverseLookup(hMaxP);
                CopyTexToShaders(texName, "_MapMaxP", hMaxP);
            }
        }
        scatterUpdateRequired = false;
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
            init = true;
            initParticlePlay();
            configureExperiment(experimentMode);
        }
        if (experimentUpdateRequired || scatterUpdateRequired)
        {
            float modelSlitWidth = slitWidthSI * modelScale;
            float modelSlitPitch = slitPitchSI * modelScale;
            if (slitModel != null)
                slitModel.UpdateGratingSettings(slitCount, 0, modelSlitWidth, 0, modelSlitPitch, 0);
            if (matGhostParticles != null)
            {
                matGhostParticles.SetInteger("_SlitCount", slitCount);
                matGhostParticles.SetFloat("_SlitWidth", modelSlitWidth);
                matGhostParticles.SetFloat("_SlitPitch", modelSlitPitch);
            }
            CreateTextures();
            if (shaderPlaying != (ParticlePlayState == 1))
                setParticlePlay(particlePlayState);
            experimentUpdateRequired = false;
            scatterUpdateRequired = false;
            updateTimer += 0.1f;
        }
        else
            updateTimer += 0.033f;
    }

    void Init()
    {
        if (togPulseParticles != null)
        {
            togPulseParticles.IsBoolean = true;
            togPulseParticles.setState(pulseParticles);
            togPulseParticles.ClientVariableName = nameof(pulseParticles);
        }
        if (particleSizeSlider != null)
        {
            particleSizeSlider.DisplayInteger = false;
            particleSizeSlider.SliderUnit = "x";
            particleSizeSlider.DisplayScale = 10f; // Display in millimetres
            particleSizeSlider.ClientVariableName = nameof(particleSize);
            particleSize = Mathf.Clamp(particleSize, 0.01f, 0.5f);
            particleSizeSlider.SetLimits(0.01f, 0.5f);
            particleSizeSlider.SetValue(particleSize);
        }

        if (slitWidthSlider != null)
        {
            slitWidthSlider.ClientVariableName = nameof(slitWidth);
            slitWidthSlider.DisplayInteger = true;
            slitWidthSlider.Interactable = true;
        }
        if (slitPitchSlider != null)
        {
            slitPitchSlider.DisplayInteger = true;
            slitPitchSlider.ClientVariableName = nameof(slitPitch);
            slitPitchSlider.Interactable = true;
        }

        configureExperiment(experimentMode);

        if (pulseWidthSlider != null)
        {
            pulseWidthSlider.SliderUnit = "s";
            pulseWidthSlider.DisplayScale = 1f; // Display in seconds
            pulseWidthSlider.ClientVariableName = nameof(pulseWidth);
            pulseWidthSlider.SetLimits(0.1f, 2f);
            pulseWidthSlider.SetValue(pulseWidth);
            pulseWidthSlider.Interactable = pulseParticles;
        }
        if (particleMeshRend != null)
        {
            matGhostParticles = particleMeshRend.material;
            particleMeshRend.enabled = false;
            if (matGhostParticles != null)
            {
                matGhostParticles.SetFloat("_MaxParticleP", maxParticleP);
                matGhostParticles.SetFloat("_MinParticleP", minParticleP);
                matGhostParticles.SetFloat("_LaserP", laserParticleP);
            }
        }
        UpdateLabels();
    }

    void OnEnable()
    {
        if (togGroupPlayPause != null)
            togGroupPlayPause.SetActiveValue(particlePlayState);
    }
    void Start()
    {
        player = Networking.LocalPlayer;
        ReviewOwnerShip();

        Init();
        //Debug.Log("BScatter Start");
        SlitCount = slitCount;
        SlitPitch = slitPitch;
        SlitWidth = slitWidth;
        LaserParticleP = laserParticleP;
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        reviewPulse();
        experimentUpdateRequired = true;
        scatterUpdateRequired = true;
    }
}
