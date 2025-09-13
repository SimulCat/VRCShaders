﻿using UdonSharp;
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
// Pulsed particles and speed range
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(PulseParticles))]
    public bool pulseParticles = false;
    [SerializeField, Range(0.01f, 1.5f), FieldChangeCallback(nameof(PulseWidth))]
    public float pulseWidth = 1f;        // particle Pulse width
    [SerializeField, Range(0,50), FieldChangeCallback(nameof(SpeedRange))]
    public float speedRange = 10f;        // Speed Range Percent

    [SerializeField, Range(1, 10), FieldChangeCallback(nameof(SimScale))]
    public float simScale;
    [SerializeField,FieldChangeCallback(nameof(DisplayColor))]
    public Color displayColor = Color.cyan;
    [SerializeField,FieldChangeCallback(nameof(MaxParticleK))]
    public float maxParticleK = 10;
    [SerializeField, FieldChangeCallback(nameof(MinParticleK))]
    public float minParticleK = 1;
    [SerializeField] bool updateColour = false;

    private float Visibility
    {
        get => visibility;
        set
        {
            visibility = Mathf.Clamp01(value);
            if (matParticleFlow != null)
            {
                matParticleFlow.SetFloat("_Visibility", visibility);
            }
            reviewProbVisibility();
        }
    }

    public float MaxParticleK 
    {   get=>maxParticleK; 
        set 
        {
            if (MaxParticleK == value)
                return;
            maxParticleK = value;
            SetColour(); 
        } 
    }
    public float MinParticleK 
    { 
        get => minParticleK;
        set
        {
            if (minParticleK == value)
                return;
            minParticleK = value;
            SetColour();
        }
    }

    [SerializeField,FieldChangeCallback(nameof(ParticleK))]
    private float particleK = 1;

    [Header("UI Elements")]
    [SerializeField] Toggle togPlay;
    [SerializeField] Toggle togPause;
    [SerializeField] Toggle togShowHide;
    [SerializeField] Toggle togProbability;
    [SerializeField] Toggle togPulseParticles;
    [SerializeField] UdonSlider probVizSlider;
    [SerializeField] UdonSlider pulseWidthSlider;
    [SerializeField] UdonSlider speedRangeSlider;

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
        matProbabilitySim.SetFloat("_Brightness", targetViz);
        crtUpdateRequired = true;
    }

    private void reviewPulse()
    {
        if (matParticleFlow == null)
            return;
        float width = pulseParticles ? pulseWidth : -1f;
        matParticleFlow.SetFloat("_PulseWidth", width);
        if (togPulseParticles != null && togPulseParticles.isOn != pulseParticles)
            togPulseParticles.SetIsOnWithoutNotify(pulseParticles);

    }
    private bool ShowProbability
    {
        get=> showProbability;
        set
        {
            bool chg = showProbability != value;
            showProbability = value;
            if (togProbability != null && togProbability.isOn != showProbability)
                togProbability.SetIsOnWithoutNotify(showProbability);
            if (probVizSlider != null)
                probVizSlider.Interactable = showProbability;
            if (chg)
                reviewProbVisibility();
            RequestSerialization();
        }
    }

    private bool PulseParticles
    {
        get => pulseParticles;
        set
        {
            bool chg = pulseParticles != value;
            pulseParticles = value;
            if (togPulseParticles != null && togPulseParticles.isOn != value)
                togPulseParticles.SetIsOnWithoutNotify(pulseParticles);
            if (chg) 
                reviewPulse();
            RequestSerialization();
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
                    togPause.SetIsOnWithoutNotify(true);
                break;
            case 1:
                if (togPlay != null && !togPlay.isOn)
                    togPlay.SetIsOnWithoutNotify(true);
                break;
            default:
                if (togShowHide != null && !togShowHide.isOn)
                    togShowHide.SetIsOnWithoutNotify(true);
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
            setParticlePlay(particlePlayState);
            updatePlayPauseStop();
            RequestSerialization();
        }
    }

    //[SerializeField]
    bool crtUpdateRequired = false;
   // [SerializeField]
    bool gratingUpdateRequired = false;
   // [SerializeField]
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
        mat.SetFloat("_SpeedRange", speedRange / 100f);
        mat.SetFloat("_ParticleP", particleK);
        mat.SetFloat("_MaxParticleP", maxParticleK);
        mat.SetFloat("_MinParticleP", minParticleK);
        mat.SetColor("_Color", displayColor);

    }

    private void initParticlePlay(Material mat)
    {
        shaderBaseTime = 0;
        shaderPauseTime = 0;
        matParticleFlow.SetFloat("_PauseTime", 0f);
        matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
        matParticleFlow.SetFloat("_Play", 1f);
        shaderPlaying = true;
        //Debug.Log("Init");
    }
    private void setParticlePlay(int playState)
    {
        if (particleMeshRend == null)
            return;
        particleMeshRend.enabled = playState >= 0;
        switch (playState)
        {
            case 1:
                if (!shaderPlaying)
                {
                    shaderBaseTime += Time.timeSinceLevelLoad - shaderPauseTime;
                    matParticleFlow.SetFloat("_BaseTime", shaderBaseTime);
                    matParticleFlow.SetFloat("_Play", 1f);
                    shaderPlaying = true;
                    //Debug.Log("Play");
                }
                break;
            case 0:
                if (shaderPlaying)
                {
                    shaderPauseTime = Time.timeSinceLevelLoad;
                    matParticleFlow.SetFloat("_PauseTime", shaderPauseTime);
                    matParticleFlow.SetFloat("_Play", 0f);
                    shaderPlaying = false;
                    //Debug.Log("Pause");
                }
                break;
            default: 
                return;
        }
    }
    /*
    public void SetGrating(int numSlits, float widthSlit, float pitchSlits, float momentumMax, float momentumMin)
    {
        //Debug.Log(string.Format("{0} SetGrating: #slit1={1} width={2} pitch={3}", gameObject.name,  numSlits, widthSlit, pitchSlits));

        bool isChanged = numSlits != slitCount || widthSlit != slitWidth || pitchSlits != slitPitch;
        isChanged |= maxParticleK != momentumMax;
        slitCount = numSlits;
        maxParticleK = momentumMax;
        minParticleK = momentumMin;
        slitWidth = widthSlit;
        slitPitch = pitchSlits;
        gratingUpdateRequired = isChanged;
        if (isChanged)
        {
            if (iHaveProbSimMat) setGratingParams(matProbabilitySim);
            if (particleMeshRend != null) 
                setParticleParams(matParticleFlow);
        }
    }
    */
    
    private float GratingOffset
    {
        get=>gratingOffset;
        set
        {
            gratingOffset = value;
            //Debug.Log("GratingOffset=" + value.ToString());
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_GratingOffset", gratingOffset*simPixelScale);
            if (matParticleFlow != null)
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
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitCount = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_SlitCount", 1f*slitCount);
            if (matParticleFlow)
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
                gratingUpdateRequired = true;
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
    [SerializeField]
    Texture2D colourMap = null;

    //private const float tHz750nm = 400;
    private const float tHz725nm = 413.5f;
    //private const float tHz700nm = 430;
    //private const float tHz400nm = 750;
    private const float tHz380nm = 790;
    /*
    private bool loadColourMap(int nSamples, string texName, Material mat)
    {
        if (mat == null)
            return false;
        Color[] texData = new Color[nSamples];
        for (int i = 0; i < nSamples; i++)
        {
            float frac = Mathf.InverseLerp(0f, nSamples, i);
            Color dColour = momentumColour(frac);
            texData[i] = dColour;
        }
        colourMap = new Texture2D(nSamples, 1, TextureFormat.RGBAFloat, 0, true);
        colourMap.SetPixels(0, 0, nSamples, 1, texData, 0);
        colourMap.filterMode = FilterMode.Point;
        colourMap.wrapMode = TextureWrapMode.Clamp;
        colourMap.Apply();
        mat.SetTexture(texName, colourMap);
        return true;
    }
    */
    private Color momentumColour(float e)
    {
        float tHz = Mathf.Lerp(tHz725nm, tHz380nm, e);
        float nm = 299792f / tHz;
        return spectrumColour(nm);
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
        float frac = Mathf.InverseLerp(minParticleK, maxParticleK, particleK);
        Color dColour = momentumColour(frac);
        DisplayColor = dColour;
    }


    public float ParticleK
    {
        get => particleK;
        set
        {
            if (value == particleK)
                return;
            crtUpdateRequired = true;
            particleK = value;
            if (iHaveProbSimMat)
                matProbabilitySim.SetFloat("_ParticleP", particleK);
            if (matParticleFlow != null)
                matParticleFlow.SetFloat("_ParticleP", particleK);
            SetColour();
        }
    }

    private Color DisplayColor
    {
        get => displayColor;
        set
        {
           // Debug.Log(gameObject.name + ": displayColour->" + value.ToString());
            displayColor = value;
            if (iHaveProbability)
                matProbabilitySim.SetColor("_Color", displayColor);
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

        if (iHaveProbSimMat)
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
            matProbabilitySim.SetFloat("_MapMaxP", maxParticleK); // "Map max momentum", float ) = 1
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

    public void showProb()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        if ((togProbability != null) && togProbability.isOn != showProbability)
            ShowProbability = !showProbability;
        //Debug.Log("togProb");
    }

    public void togPulse()
    {
        if (!iamOwner)
            Networking.SetOwner(player,gameObject);
        if (togPulseParticles != null && togPulseParticles.isOn != pulseParticles)
            PulseParticles = !pulseParticles;
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
        if (!init && particleMeshRend != null)
        {
            matParticleFlow = particleMeshRend.material;
            if (matParticleFlow != null)
            {
                init = true;
                initParticlePlay(matParticleFlow);
                setGratingParams(matParticleFlow);
                setParticleParams(matParticleFlow);
                CreateTextures();
            }
        }
        if (!init)
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

        if (particleMeshRend != null)
            matParticleFlow = particleMeshRend.material;
//        if ((matParticleFlow != null) && matParticleFlow.HasProperty("_ColourMap"))
//           loadColourMap(256, "_ColourMap", matParticleFlow);

        //Debug.Log("BScatter Start");
        ReviewOwnerShip();
        iHaveProbability = probabilityCRT != null;
        simPixelScale = simPixels.y / simSize.y;
        if (iHaveProbability)
            matProbabilitySim = probabilityCRT.material;
        iHaveProbSimMat = hasMaterialWithProperty(matProbabilitySim, texName);
        ShowProbability = showProbability;
        SlitCount = slitCount;
        SlitWidth = slitWidth;
        SlitPitch = slitPitch;
        SimScale = simScale;
        SpeedRange = speedRange;
        if (speedRangeSlider != null)
        {
            speedRangeSlider.SetLimits(0, 50);
            speedRangeSlider.SetValue(speedRange);
        }
        PulseParticles = pulseParticles;
        PulseWidth = pulseWidth;
        if (pulseWidthSlider != null)
        {
            pulseWidthSlider.SetLimits(0.1f, 1.5f);
            pulseWidthSlider.SetValue(pulseWidth);
        }
        Visibility = visibility;
        ProbVisPercent = probVisPercent;
        if (probVizSlider != null)
            probVizSlider.SetValue(probVisPercent);
        reviewPulse();
        GratingOffset = gratingOffset;
        ParticleK = particleK;
        //Debug.Log("BScatter Started");
    }
}
