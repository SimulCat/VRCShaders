
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
    //[SerializeField]
    //GratingModel slitModel;
    [SerializeField]
    Transform screenModelXfrm;
   // [SerializeField]
   // GiantLaserModel laserModel;
    
    [Header("Simulation Components")]
    [SerializeField] private MeshRenderer particleMeshRend = null;
    [SerializeField] private string texName = "_MomentumMap";
    [SerializeField] private float particleVisibility = 1;
    [Header("Scattering Configuration")]
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    [Header("Simulation Dimensions")]
    [SerializeField]
    private float simulationLength = 8f;
    [SerializeField, Tooltip("Max distance from start to target")] float maxDisplacement = 7f;
    [SerializeField]
    private Vector3 wallLimits = new Vector3(5f, 2f, 1f);
    [Header("Grating Configuration & Scale")]
    [SerializeField]
    private float gratingDistance = 0;
    [SerializeField,FieldChangeCallback(nameof(ScreenDistance)), Tooltip("Distance from grating to screen")]
    private float screenDistance = 7f;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(RowCount)),Range(1, 17)]
    private int rowCount = 1;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount)), Range(1, 17)]
    private int slitCount = 2;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch = 45f;        // "Slit Pitch" millimetre
    [SerializeField, FieldChangeCallback(nameof(RowPitch))]
    private float rowPitch = 45f;        // "Slit Pitch" millimetre
    [SerializeField, FieldChangeCallback(nameof(SlitWidthFrac))]
    private float slitWidthFrac = 0.85f;       
    [SerializeField, FieldChangeCallback(nameof(SlitHeightFrac))]
    private float slitHeightFrac = 0.85f;      
    
    // Pulsed particles and speed range
    [SerializeField, FieldChangeCallback(nameof(PulseParticles))]
    private bool pulseParticles = false;
    [SerializeField, FieldChangeCallback(nameof(PulseWidth)), Range(0.01f, 1.5f)]
    private float pulseWidth = 1f;        // particle Pulse width
    [SerializeField, FieldChangeCallback(nameof(SpeedRange)), Range(0, 50)]
    private float speedRange = 10f;        // Speed Range Percent

    [SerializeField, Range(0, 17)] private int MAX_SLITS = 7;

    [SerializeField,FieldChangeCallback(nameof(DisplayColour))]
    private Color displayColor = Color.cyan;
    [SerializeField]
    private float maxParticleP = 10;
    [SerializeField]
    private float minParticleP = 1;

    [SerializeField,FieldChangeCallback(nameof(ParticleP))]
    private float particleP = 1;

    [Header("UI Elements")]
    [SerializeField] Toggle togPlay = null;
    [SerializeField] Toggle togPause = null;
    [SerializeField] Toggle togStop = null;
    [SerializeField, UdonSynced, Tooltip("Particle demo state"),FieldChangeCallback(nameof(ParticlePlayState))] int particlePlayState = 1;
    [SerializeField] SyncedToggle togPulseParticles;

    [SerializeField] UdonSlider pulseWidthSlider;
    [SerializeField] UdonSlider speedRangeSlider;
    [SerializeField] UdonSlider momentumSlider;

    [SerializeField] UdonSlider slitWidthSlider;
    [SerializeField] UdonSlider slitHeightSlider;
    [SerializeField] UdonSlider slitPitchSlider;
    [SerializeField] UdonSlider rowPitchSlider;
    [SerializeField] UdonSlider screenDistanceSlider;


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

    public float MaxParticleP
    {
        get => maxParticleP;
        set
        {
            gratingUpdateRequired |= value != maxParticleP;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_MaxParticleP", maxParticleP);
            if (matProbCRT != null)
                matProbCRT.SetFloat("_MaxParticleP", maxParticleP);
            maxParticleP = value;
        }
    }
    public float MinParticleP
    {
        get => minParticleP;
        set
        {
            gratingUpdateRequired |= value != minParticleP;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_MinParticleP", minParticleP);
            if (matProbCRT != null)
                matProbCRT.SetFloat("_MinParticleP", minParticleP);
            minParticleP = value;
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
    bool gratingUpdateRequired = false;
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
        particleMeshRend.enabled = playState > 0;
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
            value = Mathf.Clamp(value, gratingDistance + 1, maxDisplacement);
            screenDistance = value;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ScreenDistance", screenDistance);
            float xOffset = screenDistance - (simulationLength * 0.5f);
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
            /*
            if (huygensDisplay != null)
            {
                huygensDisplay.SlitsToScreen = slitsToScreen;
                huygensDisplay.ScreenOffset = xOffset;
            }*/
        }
    }
    //Grating Displacement
    public float GratingDistance
    {
        get => gratingDistance;
        set
        {
            float halfLength = simulationLength * 0.5f;
            gratingDistance = Mathf.Clamp(value, 0, halfLength);
            Vector3 gratingLocal = new Vector3(-halfLength + gratingDistance, 0f, 0f);
            //if (slitModel != null)
            //    slitModel.transform.localPosition = gratingLocal;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_GratingDistance", gratingDistance);
            float slitsToScreen = screenDistance - gratingDistance;
            if (probabilityScreen != null)
                probabilityScreen.SlitsToScreen = slitsToScreen;
            //if (huygensDisplay != null)
            //    huygensDisplay.SlitsToScreen = slitsToScreen;
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
        float slitWidth = slitWidthFrac * slitPitch;
        float slitHeight = slitHeightFrac * rowPitch;
        beamWidthHeight = new Vector2(Mathf.Max(slitCount - 1, 0) * slitPitch + slitWidth * 1.3f, Mathf.Max(rowCount - 1, 0) * rowPitch + slitHeight * 1.3f);
        if (matParticleFlow != null)
        {
            matParticleFlow.SetFloat("_BeamWidth", beamWidthHeight.x / 1000f);
            matParticleFlow.SetFloat("_BeamHeight", beamWidthHeight.y / 1000f);
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
                gratingUpdateRequired = true;
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
                gratingUpdateRequired = true;
            }
            slitCount = value;
            if (txtSlitCountDisplay != null)
                txtSlitCountDisplay.text = slitCount.ToString();
            UpdateBeamDimensions();
        }
    }

    public float SlitWidth
    {
        get => slitWidthFrac * slitPitch;
    }
    public float SlitWidthFrac
    {
        get => slitWidthFrac;
        set
        {
            if (value != slitWidthFrac)
            {
                gratingUpdateRequired = true;
            }
            slitWidthFrac = value;
        }
    }

    public float SlitHeight
    {
        get => slitHeightFrac * rowPitch;
    }
    public float SlitHeightFrac
    {
        get => slitHeightFrac;
        set
        {
            if (value != slitHeightFrac)
                gratingUpdateRequired = true;
            slitHeightFrac = value;
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
                gratingUpdateRequired = true;
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
                gratingUpdateRequired = true;
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


    public float ParticleP
    {
        get => particleP;
        set
        {
            particleP = value;
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ParticleP", particleP);
            if (probabilityScreen != null)
                probabilityScreen.ParticleP = particleP;
            //if (huygensDisplay != null)
            //    huygensDisplay.ParticleP = particleP;
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
            //if (huygensDisplay != null)
            //    huygensDisplay.LaserColour = displayColor;
            //if (laserModel != null)
            //    laserModel.LaserColor = displayColor;
        }
    }


    private const float halfPi = 1.57079632679489661f;
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
    private void GenerateSamples(int apertureCount, float apertureWidthFrac, float aperturePitch_mm)
    {
        float aperturePitch = aperturePitch_mm / 1000f; // Convert to metres
        float apertureWidth = aperturePitch * apertureWidthFrac; // Convert to metres
        if (apertureCount < 1)
            apertureWidth = aperturePitch;
        if (gratingFourierSq == null || gratingFourierSq.Length < pointsWide)
        {
            gratingFourierSq = new float[pointsWide];
            probIntegral = new float[pointsWide + 1];
        }
        float impulse;
        float prob;
        float pi_h = Mathf.PI; // Assume h = 1 for simplicity, so pi_h = π
        float probIntegralSum = 0;
        for (int i = 0; i < pointsWide; i++)
        {
            impulse = (maxParticleP * i) / pointsWide;
            prob = sampleDistribution(impulse * pi_h, apertureCount, apertureWidth, aperturePitch);
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
        int lim = pointsWide - 1;
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

    public void CopyTexToShaders(string TexKeyword, string MaxPKeyword, string MaxIKeyWord)
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
                impulse = (maxParticleP * i) / pointsWide;

                float sample = gratingFourierSq[i];
                float integral = probIntegral[i];
                texData[pointsWide + i] = new Color(sample, integral, impulse, 1f);
                texData[pointsWide - i] = new Color(sample, -integral, -impulse, 1f);
            }
            matProbCRT.SetFloat(MaxPKeyword, maxParticleP); // "Map max momentum", float ) = 1
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
            matParticleFlow.SetFloat(MaxPKeyword, maxParticleP); // "Map max momentum", float ) = 1
        }
    }

    public bool CreateTextures()
    {
        if (matParticleFlow != null)
        {
            // Load X and Y scattering function lookups into shader 
            GenerateSamples(slitCount, slitWidthFrac, slitPitch);
            GenerateReverseLookup();
            CopyTexToShaders(texName, "_MapMaxP", "_MapMaxI");
            GenerateSamples(rowCount, slitHeightFrac, rowPitch);
            GenerateReverseLookup();
            CopyTexToShaders(texName + "Y", "_MapMaxPy", "_MapMaxIy");
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
        if (gratingUpdateRequired)
        {
            if (probabilityScreen != null)
            {
                probabilityScreen.UpdateGratingSettings(slitCount, rowCount, SlitWidth, SlitHeight, slitPitch, rowPitch);
                probabilityScreen.ParticleP = particleP;
                probabilityScreen.LaserColour = displayColor;
            }
            /*
            if (huygensDisplay != null)
            {
                huygensDisplay.UpdateGratingSettings(slitCount, rowCount, SlitWidth, SlitHeight, slitPitch, rowPitch);
                huygensDisplay.ParticleP = particleP;
                huygensDisplay.LaserColour = displayColor;
            }*/
            float slitSpacing = slitPitch / 1000f;
            float rowSpacing = rowPitch / 1000f;
           // if (slitModel != null)
           //     slitModel.UpdateGratingSettings(slitCount, rowCount, SlitWidthFrac * slitSpacing, slitHeightFrac * rowSpacing, slitSpacing, rowSpacing);
            if (matParticleFlow != null)
            {
                matParticleFlow.SetInteger("_SlitCount", slitCount);
                matParticleFlow.SetFloat("_SlitPitch", slitSpacing);
                matParticleFlow.SetFloat("_SlitWidth", slitWidthFrac * slitSpacing);
                matParticleFlow.SetInteger("_RowCount", rowCount);
                matParticleFlow.SetFloat("_RowPitch", rowSpacing);
                matParticleFlow.SetFloat("_SlitHeight", slitHeightFrac * rowSpacing);
            }
            if (matProbCRT != null)
            {
                matProbCRT.SetInteger("_SlitCount", slitCount);
                matProbCRT.SetFloat("_SlitPitch", slitSpacing);
                matProbCRT.SetFloat("_SlitWidth", slitWidthFrac * slitSpacing);
                matProbCRT.SetInteger("_RowCount", rowCount);
                matProbCRT.SetFloat("_RowPitch", rowSpacing);
                matProbCRT.SetFloat("_SlitHeight", slitHeightFrac * rowSpacing);
            }
            UpdateBeamDimensions();
            CreateTextures();
            if (shaderPlaying != (ParticlePlayState == 1))
                setParticlePlay(particlePlayState);
            gratingUpdateRequired = false;
            updateTimer += 0.05f;
        }
        else
            updateTimer += 0.01f;
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
            slitWidthSlider.SliderUnit = "%";
            slitWidthSlider.ClientVariableName = nameof(slitWidthFrac);
            slitWidthSlider.DisplayScale = 100f; // Display in millimetres
            slitWidthSlider.SetLimits(0.01f, 0.95f);
            slitWidthSlider.SetValue(slitWidthFrac);
            slitWidthSlider.Interactable = true;
        }
        if (slitHeightSlider != null)
        {
            slitHeightSlider.SliderUnit = "%";
            slitHeightSlider.DisplayScale = 100f; // Display in millimetres
            slitHeightSlider.ClientVariableName = nameof(slitHeightFrac);
            slitHeightSlider.SetLimits(0.01f, 0.95f);
            slitHeightSlider.SetValue(slitHeightFrac);
            slitHeightSlider.Interactable = true;
        }
        if (slitPitchSlider != null)
        {
            slitPitchSlider.SliderUnit = "mm";
            slitPitchSlider.DisplayScale = 1f; // Display in millimetres
            slitPitchSlider.ClientVariableName = nameof(slitPitch);
            slitPitchSlider.SetLimits(0.1f, 20f);
            slitPitchSlider.SetValue(slitPitch);
            slitPitchSlider.Interactable = true;
        }
        if (rowPitchSlider != null)
        {
            rowPitchSlider.SliderUnit = "<br>mm";
            rowPitchSlider.DisplayScale = 1f; // Display in millimetres
            rowPitchSlider.ClientVariableName = nameof(rowPitch);
            rowPitchSlider.SetLimits(0.1f, 40f);
            rowPitchSlider.SetValue(rowPitch);
            rowPitchSlider.Interactable = true;
        }

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
            momentumSlider.SliderUnit = "";
            momentumSlider.DisplayScale = 0.001f;
            momentumSlider.ClientVariableName = nameof(particleP);
            momentumSlider.SetLimits(minParticleP, maxParticleP);
            momentumSlider.SetValue(particleP);
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
            screenDistanceSlider.SetLimits(gratingDistance, WallLimits.x - 1);
            screenDistanceSlider.SetValue(screenDistance);
            screenDistanceSlider.Interactable = true;
        }
        if (particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
            particleMeshRend.enabled = false;
        }
    }
    void Start()
    {
        ReviewOwnerShip();

        Init();
        //Debug.Log("BScatter Start");
        SlitCount = slitCount;
        SlitPitch = slitPitch;
        RowPitch = rowPitch;
        SlitWidthFrac = slitWidthFrac;
        SlitHeightFrac = slitHeightFrac;
        RowCount = rowCount;
        SpeedRange = speedRange;
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        ParticleVisibility = particleVisibility;
        reviewPulse();
        GratingDistance = gratingDistance;
        ScreenDistance = screenDistance;
        WallLimits = wallLimits;
        MaxParticleP = maxParticleP;
        MinParticleP = minParticleP;
        ParticleP = particleP;
        SetColour();
        gratingUpdateRequired = true;
    }
}
