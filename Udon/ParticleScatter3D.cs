
using Newtonsoft.Json.Linq;
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class ParticleScatter3D : UdonSharpBehaviour
{
    [SerializeField]
    ProbabilityScreen probabilityScreen;
    //[SerializeField]
    // HuygensDisplay huygensDisplay;
    [SerializeField]
    GratingModel slitModel;
    [SerializeField]
    Transform screenModelXfrm;
    // [SerializeField]
    GiantLaser laserModel;

    [Header("Simulation Components")]
    [SerializeField] private MeshRenderer particleMeshRend = null;
    [SerializeField] private string texName = "_MomentumMap";
    [SerializeField] private float particleVisibility = 1;
    [Header("Scattering Configuration")]
    [SerializeField, Tooltip("Experiment Mode"), FieldChangeCallback(nameof(ExperimentMode))]
    private int experimentMode = 0;
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    private float[] planckSteps = { 1f, 5f, 10f, 50f, 100f, 500f, 1000f, 10000f };
    [SerializeField, FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;
    [Header("Fundamental Constants")]
    // [SerializeField]
    bool experimentUpdateRequired = false;

    // Planck's constant h in Joule seconds
    [SerializeField]
    private float h = 6.62607015e-10f; // 
    [SerializeField]
    private const float AMU_ToKg = 1.66054e-27f;
    private const float halfPi = 1.57079632679489661f;

    [SerializeField, FieldChangeCallback(nameof(MolecularWeight))]
    private float molecularWeight = 1f;

    [SerializeField,UdonSynced,FieldChangeCallback(nameof(PlanckIndex))]
    private int planckIndex = 1;
    [Header("Simulation Dimensions")]
    [SerializeField, Tooltip("Max distance from start to target")] float maxDisplacement = 7f;
    [SerializeField]
    private Vector3 wallLimits = new Vector3(5f, 2f, 1f);
    [Header("Grating Configuration & Scale")]
    [SerializeField]
    float slitWidthUnitFactor = 1000f; // Scale factor from Unity units to experiment units (mm)
    [SerializeField]
    float slitHeightUnitFactor = 1000f; // Scale factor from Unity units to experiment units (mm)
    [SerializeField]
    string slitWidthDisplayUnits = "μm";
    [SerializeField]
    string slitHeightDisplayUnits = "μm";
    [SerializeField, Tooltip("Distance from source to grating")]
    private float gratingDistance = 0;
    [SerializeField, FieldChangeCallback(nameof(ScreenDistance)), Tooltip("Distance from grating to screen")]
    private float screenDistance = 7f;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(RowCount)), Range(1, 17)]
    private int rowCount = 1;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount)), Range(1, 17)]
    private int slitCount = 2;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch = 0.0027f;        // "Slit Pitch" millimetre
    [SerializeField, FieldChangeCallback(nameof(RowPitch))]
    private float rowPitch = 1f;        // "Slit Pitch" millimetre
    [SerializeField, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth = 0.006f;        // "Slit Width" millimetre
    [SerializeField, FieldChangeCallback(nameof(SlitHeight))]
    private float slitHeight = 0.1f;        // "Slit Height" millimetre

    // Pulsed particles and speed range
    [SerializeField, FieldChangeCallback(nameof(PulseParticles))]
    private bool pulseParticles = false;
    [SerializeField, FieldChangeCallback(nameof(PulseWidth)), Range(0.01f, 1.5f)]
    private float pulseWidth = 1f;        // particle Pulse width
    [SerializeField, FieldChangeCallback(nameof(SpeedRange)), Range(0, 50)]
    private float speedRange = 10f;        // Speed Range Percent

    [SerializeField, Range(0, 17)] private int MAX_SLITS = 7;

    [SerializeField, FieldChangeCallback(nameof(DisplayColour))]
    private Color displayColor = Color.cyan;
    [SerializeField, FieldChangeCallback(nameof(NominalParticleP))]
    float nominalParticleP = 13.6f;
    [SerializeField]
    float SItoMomentum = 1.0e24f;
    [SerializeField]
    string momentumUnits = "yNs";
    [SerializeField]
    private float maxParticleP = 13.2f;
    [SerializeField]
    private float minParticleP = 7.64f;
    [SerializeField]
    private float particleAMU = 0.00054858f;
    [SerializeField, FieldChangeCallback(nameof(MomentumAdj))]
    private float momentumAdj = 1;
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
    [SerializeField] private UdonSlider speedRangeSlider;
    [SerializeField] private UdonSlider momentumSlider;

    [SerializeField] private UdonSlider slitWidthSlider;
    [SerializeField] private UdonSlider slitHeightSlider;
    [SerializeField] private UdonSlider slitPitchSlider;
    [SerializeField] private UdonSlider rowPitchSlider;
    [SerializeField] private UdonSlider screenDistanceSlider;


    [SerializeField] private TextMeshProUGUI txtSlitCountDisplay;
    [SerializeField] private TextMeshProUGUI txtRowCountDisplay;

    //[Header("For tracking in Editor")]
    //[SerializeField, Tooltip("Shown for editor reference, loaded at Start")]
    [SerializeField]
    private Material matParticleFlow = null;
    [SerializeField]
    Material matProbCRT;

    //[SerializeField]
    private float shaderPauseTime = 0;
    //[SerializeField]
    private float shaderBaseTime = 0;
    //[SerializeField]
    private bool shaderPlaying = true;
    private VRCPlayerApi player;
    private bool iamOwner = false;
    [SerializeField]
    private Vector3[] slitWidthMinMaxDefault = new[] {
        new Vector3(.001f,0.0125f,.008f), new Vector3(0.0001f,0.00095f,0.00063f), new Vector3(0.0050f,0.050f, 0.023f) 
    };
    [SerializeField]
    private Vector3[] slitHeightMinMaxDefault = new[] {
        new Vector3(.001f,.0125f,.008f), new Vector3(0.01f,.1f,0.04f), new Vector3(0.1f,0.5f, 0.2f)
    };
    [SerializeField]
    private Vector3[] slitPitchMinMaxDefault = new[] {
        new Vector3(0.013f,0.065f,.03f), new Vector3(0.0001f,0.005f,0.00272f), new Vector3(0.07f,0.35f, 0.115f)
    }; 
    [SerializeField]
    private Vector3[] rowPitchMinMaxDefault = new[] {
        new Vector3(.013f,.065f,0.03f), new Vector3(.1f,0.3f,0.2f), new Vector3(0.07f,0.35f, 0.2f)
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

    private float PlanckScale
    {
        get => planckSteps[Mathf.Clamp(planckIndex, 0, planckSteps.Length - 1)];
    }

    public float MolecularWeight
    {
        get => molecularWeight;
        set
        {
            if (value != molecularWeight)
            {
                experimentUpdateRequired = true;
            }
            molecularWeight = value;
        }
    }

    public bool UseQuantumScatter
    {
        get => useQuantumScatter;
        set
        {
            useQuantumScatter = value;
            if (matParticleFlow != null)
                matParticleFlow.SetInteger("_UseQuantumScatter", useQuantumScatter ? 1 : 0);
        }
    }

    private void UpdateLabels()
    {
        if (planckLabel != null) 
            planckLabel.text =  string.Format("h={0:#.##e+0}", h * PlanckScale);
        if (planckScaleLabel != null)
            planckScaleLabel.text = string.Format("h x {0}", (int)PlanckScale);
    }

    private int PlanckIndex
    {
        get => planckIndex;
        set
        {
            int val = Mathf.Clamp(value, 0, planckSteps.Length - 1);
            if (val != planckIndex)
            {
                planckIndex = val;
                experimentUpdateRequired = true;
            }
            UpdateLabels();
            RequestSerialization();
        }
    }

    public void IncPlanck()
    {    
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        PlanckIndex = planckIndex + 1;
    }
    public void DecPlanck()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        PlanckIndex = planckIndex - 1;
    }
    private void configureExperiment(int mode)
    {
        // Update momentum and Planck units


        switch (experimentMode)
        {
            case 1: // Electrons slit width is from 20nm to 100nm, height 1um to 10um
                slitWidthUnitFactor = 100000f; // (1mm = 100nm)
                slitHeightUnitFactor = 100f;   //  
                slitWidthDisplayUnits = "nm";
                slitHeightDisplayUnits = "μm";
                NominalParticleP = 13.6f; // 13.6 yocto Newton-Seconds Electron 600V
                molecularWeight = 0.00054858f; // Electron mass in AMU
                break;
            case 2: // Neutrons
                slitWidthUnitFactor = 1000f; // (1mm = 1μm) 0.02 -> 20
                slitHeightUnitFactor = 1000f; //  
                slitWidthDisplayUnits = "μm";
                slitHeightDisplayUnits = "μm";
                NominalParticleP = 0.442f; // 0.442 yocto Newton-Seconds Cold Neutron 15Angstrom
                molecularWeight = 1.008664f; // Neutron mass in AMU
                break;
            default:
            case 0:
                slitWidthUnitFactor = 1e3f;
                slitHeightUnitFactor = 1e3f;
                slitWidthDisplayUnits = "μm";
                slitHeightDisplayUnits = "μm";
                planckIndex = 4;
                NominalParticleP = 13.6f; // 13.6 yocto Newton-Seconds Electron 600V
                molecularWeight = 0.00054858f; // Electron mass in AMU
                break;
                // Handle mode change
        }

        if (slitWidthSlider != null)
        {
            slitWidthSlider.SliderUnit = slitWidthDisplayUnits;
            slitWidthSlider.DisplayScale = slitWidthUnitFactor; // Display in millimetres
            slitWidthSlider.SetLimits(slitWidthMinMaxDefault[mode].x, slitWidthMinMaxDefault[mode].y);
            slitWidthSlider.SetValue(slitWidthMinMaxDefault[mode].z);
        }
        SlitWidth = slitWidthMinMaxDefault[mode].z;
        if (slitHeightSlider != null)
        {
            slitHeightSlider.SliderUnit = "<br>" + slitHeightDisplayUnits;
            slitHeightSlider.DisplayScale = slitHeightUnitFactor; // Display in micrometres
            slitHeightSlider.SetLimits(slitHeightMinMaxDefault[mode].x, slitHeightMinMaxDefault[mode].y);
            slitHeightSlider.SetValue(slitHeightMinMaxDefault[mode].z);
        }
        SlitHeight = slitHeightMinMaxDefault[mode].z;
        if (slitPitchSlider != null)
        {
            slitPitchSlider.SliderUnit = slitWidthDisplayUnits;
            slitPitchSlider.DisplayScale = slitWidthUnitFactor; // Display in nanometres
            slitPitchSlider.SetLimits(slitPitchMinMaxDefault[mode].x, slitPitchMinMaxDefault[mode].y);
            slitPitchSlider.SetValue(slitPitchMinMaxDefault[mode].z);
        }
        SlitPitch = slitPitchMinMaxDefault[mode].z;
        if (rowPitchSlider != null)
        {
            rowPitchSlider.SliderUnit = "<br>" + slitHeightDisplayUnits;
            rowPitchSlider.DisplayScale = slitHeightUnitFactor; // Display in micrometres
            rowPitchSlider.SetLimits(rowPitchMinMaxDefault[mode].x, rowPitchMinMaxDefault[mode].y);
            rowPitchSlider.SetValue(rowPitchMinMaxDefault[mode].z);
        }
        rowPitch = rowPitchMinMaxDefault[mode].z;
    }
    public int ExperimentMode
    {
        get => experimentMode;
        set
        {
            if (value != experimentMode)
            {
                experimentMode = value;
                configureExperiment(experimentMode);
            }
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

    public float ParticleVisibility
    {
        get => particleVisibility;
        set
        {
            particleVisibility = Mathf.Clamp01(value);
            if (matParticleFlow != null)
               matParticleFlow.SetFloat("_Visibility", particleVisibility);
        }
    }

    // Nominal Particle Momentum   
    public float NominalParticleP
    {
        get => nominalParticleP;
        set
        {
            experimentUpdateRequired |= value != nominalParticleP;
            nominalParticleP = value;
            minParticleP = value * 0.25f;
            maxParticleP = value;
            if (matParticleFlow != null)
            {
                matParticleFlow.SetFloat("_MaxParticleP", maxParticleP);
                matParticleFlow.SetFloat("_MinParticleP", minParticleP);
            }
            if (matProbCRT != null)
            {
                matProbCRT.SetFloat("_MaxParticleP", maxParticleP);
                matProbCRT.SetFloat("_MinParticleP", minParticleP);
            }
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

    public void incRows()
    {
        if (rowCount < MAX_SLITS)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            RowCount = rowCount + 1;
        }
    }

    public void decRows()
    {
        if (rowCount > 0)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            RowCount = rowCount - 1;
        }
    }
    private void reviewPulse()
    {
        if (matParticleFlow == null)
            return;
        float width = pulseParticles ? pulseWidth : -1f;
        matParticleFlow.SetFloat("_PulseWidth", width);

    }
    public bool PulseParticles
    {
        get => pulseParticles;
        set
        {
            Debug.Log($"pulse Event {value}");
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
        if (matParticleFlow != null)
        {
            matParticleFlow = particleMeshRend.material;
            particleMeshRend.enabled = (particlePlayState > 0);

            matParticleFlow.SetFloat("_PauseTime", shaderPauseTime);
            matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
            matParticleFlow.SetInteger("_Play", play);
        }

        shaderPlaying = true;
        //Debug.Log("Init");
    }


    private void setParticlePlay(int playState)
    {
        if (matParticleFlow == null)
            return;
        particleMeshRend.enabled = playState >= 0;
        switch (playState)
        {
            case 1: // PlayState.Playing:
                if (!shaderPlaying)
                {
                    shaderBaseTime += Time.timeSinceLevelLoad - shaderPauseTime;
                    matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
                    matParticleFlow.SetInteger("_Play", 1);
                    shaderPlaying = true;
                    //Debug.Log("Play");
                }
                break;
            case 0: // PlayState.Paused:
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

    public float ScreenDistance
    {
        get => screenDistance;
        set
        {
            value = Mathf.Clamp(value, gratingDistance + 1.5f, maxDisplacement);
            screenDistance = value;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ScreenDistance", screenDistance);
            float xOffset = screenDistance - (wallLimits.x * 0.5f);
            float slitsToScreen = screenDistance - gratingDistance;
            if (screenModelXfrm != null)
            {
                Vector3 pos = screenModelXfrm.localPosition;
                pos.x = xOffset;
                screenModelXfrm.localPosition = pos;
            }
            if (probabilityScreen != null)
            {
                probabilityScreen.ScreenOffset = xOffset;
                probabilityScreen.SlitsToScreen = slitsToScreen;
            }
        }
    }
    //Grating Displacement
    public float GratingDistance
    {
        get => gratingDistance;
        set
        {
            float halfLength = wallLimits.x * 0.5f;
            gratingDistance = Mathf.Clamp(value, 0, halfLength);
            Vector3 gratingLocal = new Vector3(-halfLength + gratingDistance, 0f, 0f);
            if (slitModel != null)
                slitModel.transform.localPosition = gratingLocal;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_GratingDistance", gratingDistance);
            float slitsToScreen = screenDistance - gratingDistance;
            if (probabilityScreen != null)
                probabilityScreen.SlitsToScreen = slitsToScreen;
        }
    }

    public Vector3 WallLimits
    {
        get => wallLimits;
        set
        {
            wallLimits = value;
            if (matParticleFlow != null)
                matParticleFlow.SetVector("_WallLimits", wallLimits);
        }
    }

    Vector2 beamWidthHeight = new Vector2(1, 1);
    private void UpdateBeamDimensions()
    {
        beamWidthHeight = new Vector2(Mathf.Max(slitCount - 1, 0) * slitPitch + slitWidth * 1.3f, Mathf.Max(rowCount - 1, 0) * rowPitch + slitHeight * 1.3f);
        if (matParticleFlow != null)
        {
            matParticleFlow.SetFloat("_BeamWidth", beamWidthHeight.x);
            matParticleFlow.SetFloat("_BeamHeight", beamWidthHeight.y);
        }
    }

    public int RowCount
    {
        get => rowCount;
        set
        {
            value = Mathf.Clamp(value, 0, MAX_SLITS);
            if (value != rowCount)
            {
                experimentUpdateRequired = true;
            }
            rowCount = value;
            if (txtRowCountDisplay != null)
                txtRowCountDisplay.text = rowCount.ToString();
            UpdateBeamDimensions();
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
                experimentUpdateRequired = true;
            }
            slitCount = value;
            if (txtSlitCountDisplay != null)
                txtSlitCountDisplay.text = slitCount.ToString();
            UpdateBeamDimensions();
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
            }
            slitWidth = value;
        }
    }

    public float SlitHeight
    {
        get => slitHeight;
        set
        {
            if (value != slitHeight)
            {
                experimentUpdateRequired = true;
            }
            slitHeight = value;
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
            speedRange = Mathf.Clamp(value, 0, 50);
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
            }
            slitPitch = value;
        }
    }

    public float RowPitch
    {
        get => rowPitch;
        set
        {
            if (value != rowPitch)
            {
                experimentUpdateRequired = true;
            }
            rowPitch = value;
        }
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


    private void SetColour()
    {
        float frac = Mathf.InverseLerp(minParticleP, maxParticleP, particleP);
        Color dColour = spectrumColour(Mathf.Lerp(725f, 380f, frac), 1f);
        DisplayColour = dColour;
    }


    private float MomentumAdj
    {
        get => momentumAdj;
        set
        {
            momentumAdj = value;
            ParticleP = Mathf.Lerp(minParticleP, maxParticleP, momentumAdj);
        }
    }

    private float ParticleP
    {
        get => particleP;
        set
        {
            particleP = value;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ParticleP", particleP);
            if (probabilityScreen != null)
                probabilityScreen.ParticleP = particleP;
            SetColour();
        }
    }
    public Color DisplayColour
    {
        get => displayColor;
        set
        {
            displayColor = value;
            if (probabilityScreen != null)
               probabilityScreen.LaserColour = displayColor;
            if (laserModel != null)
                laserModel.LaserColor = displayColor;
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
    private void GenerateSamples(int apertureCount, float apertureWidthSI, float aperturePitchSI, float maxP)
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
        float planckScaled = h * PlanckScale;
        float pi_div_h = Mathf.PI / planckScaled; // Assume h = 1 for simplicity, so pi_div_h = π
        float probIntegralSum = 0;
        float maxDistributionP = ((6 * planckScaled) / apertureWidthSI)*SItoMomentum;
        Debug.Log(string.Format("{0} generateSamples: planckScaled={1} maxDistributionP={2} apertureWidthSI={3} apertureCount={4}", gameObject.name, planckScaled, maxDistributionP, apertureWidthSI, apertureCount));
        Debug.Log(string.Format("{0} generateSamples: maxP={5} pi_div_h {1} s={2} w={3} p={4}", gameObject.name, pi_div_h, slitCount, slitWidth, slitPitch, maxP));

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
        //probIntegral[pointsWide] = pointsWide;
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

    public void CopyTexToShaders(string TexKeyword, string MaxPKeyword, string MaxIKeyWord, float maxP)
    {
        Color[] texData = new Color[pointsWide + pointsWide];

        if (matProbCRT != null)
        {
            var tex = new Texture2D(pointsWide * 2, 1, TextureFormat.RGBAFloat, 0, true);

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float impulse;
            for (int i = 0; i < pointsWide; i++)
            {
                impulse = (maxP * i) / pointsWide;

                float sample = gratingFourierSq[i];
                float integral = probIntegral[i];
                texData[pointsWide + i] = new Color(sample, integral, impulse, 1f);
                texData[pointsWide - i] = new Color(sample, -integral, -impulse, 1f);
            }
            matProbCRT.SetFloat(MaxPKeyword, maxP); // "Map max momentum", float ) = 1
            matProbCRT.SetFloat(MaxIKeyWord, probIntegral[pointsWide - 1]); // "Map max integral", float ) = 1
            texData[0] = new Color(0, -probIntegral[pointsWide - 1], -1, 1f);
            tex.SetPixels(0, 0, pointsWide * 2, 1, texData, 0);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            matProbCRT.SetTexture(TexKeyword, tex);
        }
        if (matParticleFlow != null)
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
            matParticleFlow.SetTexture(TexKeyword, tex);
            matParticleFlow.SetFloat(MaxPKeyword, maxP); // "Map max momentum", float ) = 1
        }
    }

    public bool CreateTextures()
    {
        if (matParticleFlow != null)
        {
            float maxP = maxParticleP * 0.5f;
            // Load X and Y scattering function lookups into shader 
            GenerateSamples(slitCount, slitWidth/slitWidthUnitFactor, slitPitch/slitWidthUnitFactor,maxP);
            GenerateReverseLookup(maxP);
            CopyTexToShaders(texName, "_MapMaxP", "_MapMaxI",maxP);
            GenerateSamples(rowCount, slitHeight/slitHeightUnitFactor, rowPitch/slitHeightUnitFactor,maxP);
            GenerateReverseLookup(maxP);
            CopyTexToShaders(texName + "Y", "_MapMaxPy", "_MapMaxIy",maxP);
        }
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
        }
        if (experimentUpdateRequired)
        {
            if (probabilityScreen != null)
            {
                probabilityScreen.UpdateGratingSettings(slitCount, rowCount, slitWidth, slitHeight, slitPitch, rowPitch);
                probabilityScreen.ParticleP = particleP;
                probabilityScreen.LaserColour = displayColor;
            }
            if (slitModel != null)
                slitModel.UpdateGratingSettings(slitCount, rowCount, slitWidth, slitHeight, slitPitch, rowPitch);
            if (matParticleFlow != null)
            {
                matParticleFlow.SetInteger("_SlitCount", slitCount);
                matParticleFlow.SetFloat("_SlitPitch", slitPitch);
                matParticleFlow.SetFloat("_SlitWidth", slitWidth);
                matParticleFlow.SetInteger("_RowCount", rowCount);
                matParticleFlow.SetFloat("_RowPitch", rowPitch);
                matParticleFlow.SetFloat("_SlitHeight", slitHeight);
            }
            if (matProbCRT != null)
            {
                matProbCRT.SetInteger("_SlitCount", slitCount);
                matProbCRT.SetFloat("_SlitPitch", slitPitch);
                matProbCRT.SetFloat("_SlitWidth", slitWidth);
                matProbCRT.SetInteger("_RowCount", rowCount);
                matProbCRT.SetFloat("_RowPitch", rowPitch);
                matProbCRT.SetFloat("_SlitHeight", slitHeight);
            }
            UpdateBeamDimensions();
            CreateTextures();
            if (shaderPlaying != (ParticlePlayState == 1))
                setParticlePlay(particlePlayState);
            experimentUpdateRequired = false;
            updateTimer += 0.1f;
        }
        else
            updateTimer += 0.033f;
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

    void Init()
    {
        if (togPulseParticles != null)
        {
            togPulseParticles.IsBoolean = true;
            togPulseParticles.setState(pulseParticles);
            togPulseParticles.ClientVariableName = nameof(pulseParticles);

        }

        if (slitWidthSlider != null)
        {
            slitWidthSlider.ClientVariableName = nameof(slitWidth);
            slitWidthSlider.DisplayInteger = true;
            slitWidthSlider.Interactable = true;
        }
        if (slitHeightSlider != null)
        {
            slitHeightSlider.ClientVariableName = nameof(slitHeight);
            slitHeightSlider.DisplayInteger = true;
            slitHeightSlider.Interactable = true;
        }
        if (slitPitchSlider != null)
        {
            slitPitchSlider.DisplayInteger = true;
            slitPitchSlider.ClientVariableName = nameof(slitPitch);
            slitPitchSlider.Interactable = true;
        }
        if (rowPitchSlider != null)
        {
            rowPitchSlider.DisplayInteger = true;
            rowPitchSlider.ClientVariableName = nameof(rowPitch);
            rowPitchSlider.Interactable = true;
        }

        configureExperiment(experimentMode);

        if (speedRangeSlider != null)
        {
            speedRangeSlider.SliderUnit = "%";
            speedRangeSlider.DisplayScale = 1f;
            speedRangeSlider.ClientVariableName = nameof(speedRange);
            speedRangeSlider.SetLimits(0, 50);
            speedRangeSlider.SetValue(speedRange);
            speedRangeSlider.Interactable = true;
        }
        if (momentumSlider != null)
        {
            momentumSlider.SliderUnit = "%";
            momentumSlider.DisplayScale = 100f;
            momentumSlider.ClientVariableName = nameof(momentumAdj);
            momentumSlider.SetLimits(0.25f, 1f);
            momentumSlider.SetValue(momentumAdj);
            momentumSlider.Interactable = true;
        }
        if (pulseWidthSlider != null)
        {
            pulseWidthSlider.SliderUnit = "s";
            pulseWidthSlider.DisplayScale = 1f; // Display in seconds
            pulseWidthSlider.ClientVariableName = nameof(pulseWidth);
            pulseWidthSlider.SetLimits(0.1f, 2f);
            pulseWidthSlider.SetValue(pulseWidth);
            pulseWidthSlider.Interactable = pulseParticles;
        }
        if (screenDistanceSlider != null)
        {
            screenDistanceSlider.SliderUnit = "m";
            screenDistanceSlider.DisplayScale = 1f; // Display in metres
            screenDistanceSlider.ClientVariableName = nameof(screenDistance);
            screenDistanceSlider.SetLimits(gratingDistance+1.5f, WallLimits.x - 1);
            screenDistanceSlider.SetValue(screenDistance);
            screenDistanceSlider.Interactable = true;
        }
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
            particleMeshRend.enabled = false;
        }
        UpdateLabels();
    }
    void Start()
    {
        ReviewOwnerShip();

        Init();
        //Debug.Log("BScatter Start");
        SlitCount = slitCount;
        SlitPitch = slitPitch;
        RowPitch = rowPitch;
        SlitWidth = slitWidth;
        SlitHeight= slitHeight;
        RowCount = rowCount;
        SpeedRange = speedRange;
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        ParticleVisibility = particleVisibility;
        reviewPulse();
        GratingDistance = gratingDistance;
        ScreenDistance = screenDistance;
        WallLimits = wallLimits;
        NominalParticleP = nominalParticleP;
        ParticleP = particleP;
        SetColour();
        experimentUpdateRequired = true;
    }
}
