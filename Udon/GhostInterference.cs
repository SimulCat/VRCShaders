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
    private float[] planckSteps = { 1f, 5f, 10f, 50f, 100f, 500f, 1000f, 5000f };
    [SerializeField, FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;
    [Header("Fundamental Constants")]
    // [SerializeField]
    bool experimentUpdateRequired = false;
    bool scatterUpdateRequired = false;

    // Planck's constant h in Joule seconds
    [SerializeField]
    private float h = 6.62607015e-10f; // 
    [SerializeField]
    private const float AMU_ToKg = 1.66054e-27f;
    private const float halfPi = 1.57079632679489661f;

    [Header("Simulation Dimensions")]
    [SerializeField, Tooltip("Max distance from start to target")] float maxDisplacement = 7f;
    [SerializeField]
    private Vector3 wallLimits = new Vector3(0.150f, 0.400f, 0.2f);
    [Header("Grating Configuration & Scale")]
    [SerializeField]
    float slitWidthUnitScale = 10000f; // Scale factor from Unity units to experiment units (1 mm is 0.01 Unity units)
    [SerializeField]
    string slitWidthDisplayUnits = "μm";
    [SerializeField, Tooltip("Distance from source to grating")]
    private float gratingDistance = 0;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount)), Range(1, 17)]
    private int slitCount = 2;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch = 0.0027f;        // "Slit Pitch" millimetre
    [SerializeField, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth = 0.006f;        // "Slit Width" millimetre

    // Pulsed particles and speed range
    [SerializeField, FieldChangeCallback(nameof(PulseParticles))]
    private bool pulseParticles = false;
    [SerializeField, FieldChangeCallback(nameof(PulseWidth)), Range(0.01f, 1.5f)]
    private float pulseWidth = 1f;        // particle Pulse width

    [SerializeField, Range(0, 17)] private int MAX_SLITS = 7;

    [SerializeField, Tooltip("Simulation Momentum default yocto newton-seconds 1e-24")]
    float SItoSimMomentum = 1.0e24f;
    [SerializeField]
    float gameLengthToSI = 0.001f;
    [SerializeField]
    private float maxParticleP = 13.2f;
    [SerializeField]
    private float minParticleP = 7.64f;
    private float particleP = 10.0f;
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI planckLabel;
    [SerializeField] private TextMeshProUGUI planckScaleLabel;

    [SerializeField] private Toggle togPlay = null;
    [SerializeField] private Toggle togPause = null;
    [SerializeField] private Toggle togStop = null;
    [SerializeField, UdonSynced, Tooltip("Particle demo state"), FieldChangeCallback(nameof(ParticlePlayState))]
    private int particlePlayState = 1;
    [SerializeField] private SyncedToggle togPulseParticles;

    [SerializeField] private UdonSlider pulseWidthSlider;

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
        new Vector3(0.10f,0.3f,0.15f), new Vector3(0.1f,0.3f,0.15f) 
    };
    [SerializeField]
    private Vector3[] slitPitchMinMaxNominal = new[] {
        new Vector3(0.35f, 0.55f, 0.45f), new Vector3(0.35f, 0.55f, 0.45f)
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
            planckLabel.text = string.Format("h={0:#.##e+0}", h);
    }

    private void configureExperiment(int mode)
    {
        // Update momentum and Planck units
        switch (experimentMode)
        {
            default:
            case 0:
                gameLengthToSI = 0.1f;
                slitWidthUnitScale = 1e3f;
                slitWidthDisplayUnits = "μm";
                break;
                // Handle mode change
        }
        if (slitWidthSlider != null)
        {
            slitWidthSlider.SliderUnit = slitWidthDisplayUnits;
            slitWidthSlider.DisplayScale = slitWidthUnitScale; // Display in millimetres
            slitWidthSlider.SetLimits(slitWidthMinMaxNominal[mode].x, slitWidthMinMaxNominal[mode].y);
            slitWidthSlider.SetValue(slitWidthMinMaxNominal[mode].z);
        }
        SlitWidth = slitWidthMinMaxNominal[mode].z;
        if (slitPitchSlider != null)
        {
            slitPitchSlider.SliderUnit = slitWidthDisplayUnits;
            slitPitchSlider.DisplayScale = slitWidthUnitScale; // Display in nanometres
            slitPitchSlider.SetLimits(slitPitchMinMaxNominal[mode].x, slitPitchMinMaxNominal[mode].y);
            slitPitchSlider.SetValue(slitPitchMinMaxNominal[mode].z);
        }
        SlitPitch = slitPitchMinMaxNominal[mode].z;
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
            switch (particlePlayState)
            {
                case 0: // PlayState.Paused:
                    if (togPause != null && !togPause.isOn)
                        togPause.SetIsOnWithoutNotify(true);
                    break;
                case 1:// PlayState.Playing:
                    if (togPlay != null && !togPlay.isOn)
                        togPlay.SetIsOnWithoutNotify(true);
                    break;
                default:
                    if (togStop != null && !togStop.isOn)
                        togStop.SetIsOnWithoutNotify(true);
                    break;
            }
            setParticlePlay(particlePlayState);
            RequestSerialization();
        }
    }

    public void simHide()
    {
        if (togStop != null && togStop.isOn && particlePlayState >= 0)
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
        int play = shaderPlaying ? 1 : 0;
        if (matGhostParticles != null)
        {
            matGhostParticles = particleMeshRend.material;
            particleMeshRend.enabled = (particlePlayState > 0);

            matGhostParticles.SetFloat("_PauseTime", shaderPauseTime);
            matGhostParticles.SetFloat("_BaseTime", shaderBaseTime);
            matGhostParticles.SetInteger("_Play", play);
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
                    matGhostParticles.SetInteger("_Play", 0);
                    shaderPlaying = false;
                    //Debug.Log("Pause");
                }
                break;
            default:
                return;
        }
    }
    public Vector3 WallLimits
    {
        get => wallLimits;
        set
        {
            wallLimits = value;
            if (matGhostParticles != null)
                matGhostParticles.SetVector("_WallLimits", wallLimits);
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
    // [SerializeField]
    private float[] gratingFourierSq;
    //[SerializeField]
    private float[] probIntegral;
    //[SerializeField]
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
        //float pMaxSingle = (float)((7.0d * Math.PI) / (apertureWidth * pointsWide));
        //float maxP = Mathf.Min(pMaxSingle,maxParticleP*0.667f);
        float impulse;
        float prob;
        float planckScaled = h * SItoSimMomentum;
        float pi_div_h = Mathf.PI / planckScaled; // Assume h = 1 for simplicity, so pi_div_h = π
        //Debug.Log(string.Format("{0} generateSamples: maxP (yNs)={5} pi_div_h {1} s={2} widthSI={3} pitchSI={4}", gameObject.name, pi_div_h, apertureCount, apertureWidthSI, aperturePitchSI, maxP));
        float probIntegralSum = 0;
        float maxDistributionP = (10 * planckScaled) / apertureWidthSI;
        //Debug.Log(string.Format("{0} generateSamples: planckScaled={1} maxDistributionP={2}", gameObject.name, planckScaled, maxDistributionP));
        maxDistributionP = maxDistributionP < maxP ? maxDistributionP : maxP;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxDistributionP * i) / pointsWide;
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
        return maxDistributionP;
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
            weightedLookup[i] = val * norm;///lim;
        }
    }

    public void CopyTexToShaders(string TexKeyword, string MaxPKeyword, string MaxIKeyWord, float mapMaxP, float maxP)
    {
        Color[] texData = new Color[pointsWide + pointsWide];
        //Debug.Log($"{gameObject.name} CopyTexToShaders maxP={maxP}, mapMaxP={mapMaxP}");
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
            matGhostParticles.SetFloat(MaxPKeyword, maxP); // "Map max momentum", float ) = 1
        }
    }

    public bool CreateTextures()
    {
        if (matGhostParticles != null)
        {
            float maxP = maxParticleP * 0.414f;
            // Load X and Y scattering function lookups into shader 
            if (scatterUpdateRequired)
            {
                float hMaxP = GenerateSamples(slitCount, slitWidth * gameLengthToSI, slitPitch * gameLengthToSI, maxP);
                //Debug.Log(string.Format("{0} CreateTextures horiz: hMaxP={1}", gameObject.name, hMaxP));
                GenerateReverseLookup(hMaxP);
                CopyTexToShaders(texName, "_MapMaxP", "_MapMaxI", hMaxP, maxP);
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
            if (slitModel != null)
                slitModel.UpdateGratingSettings(slitCount, 0, slitWidth, 0, slitPitch, 0);
            if (matGhostParticles != null)
            {
                matGhostParticles.SetInteger("_SlitCount", slitCount);
                matGhostParticles.SetFloat("_SlitPitch", slitPitch);
                matGhostParticles.SetFloat("_SlitWidth", slitWidth);
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
        }
        UpdateLabels();
    }
    void Start()
    {
        ReviewOwnerShip();
        WallLimits = wallLimits;

        Init();
        //Debug.Log("BScatter Start");
        SlitCount = slitCount;
        SlitPitch = slitPitch;
        SlitWidth = slitWidth;
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        reviewPulse();
        experimentUpdateRequired = true;
        scatterUpdateRequired = true;
    }
}
