using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class WavePanelControl : UdonSharpBehaviour
{
    [SerializeField, Tooltip("Custom Render texture")]
    private CustomRenderTexture simCRT;

    [Tooltip("Wave Display Mesh")] 
    public MeshRenderer thePanel;

    [SerializeField, Tooltip("CRT Update Cadence"), Range(0.01f, 0.5f)] float dt = 0.3f;

    [SerializeField] Toggle togPlay;
    [SerializeField] Toggle togPause;
    [SerializeField,UdonSynced, FieldChangeCallback(nameof(PlaySim))] bool playSim;

    [Header("Display Mode")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(DisplayMode))]
    public int displayMode;

    [SerializeField] Toggle togReal;
    bool iHaveTogReal = false;
    [SerializeField] Toggle togImaginary;
    bool iHaveTogIm = false;
    [SerializeField] Toggle togRealPwr;
    bool iHaveTogRealPwr = false;
    [SerializeField] Toggle togImPwr;
    bool iHaveTogImPwr = false;
    [SerializeField] Toggle togAmplitude;
    bool iHaveTogAmp = false;
    [SerializeField] Toggle togProbability;
    bool iHaveTogProb = false;

    [SerializeField] UdonSlider speedSlider;
    private bool iHaveSpeedControl = false;
    [SerializeField, FieldChangeCallback(nameof(WaveSpeed))] public float waveSpeed;
    private float initialSpeed = 1;
    [SerializeField] UdonSlider momentumSlider;
    private bool iHaveLambdaControl = false;
    [SerializeField, Range(1, 100)] float defaultLambda = 24;
    [SerializeField, Range(1, 100), FieldChangeCallback(nameof(Lambda))] public float lambda = 24;

    [SerializeField]
    private UdonSlider pitchSlider;
    private bool iHavePitchControl = false;

    [SerializeField]
    private UdonSlider widthSlider;
    private bool iHaveWidthControl = false;

    [SerializeField, Range(20, 500), FieldChangeCallback(nameof(SlitPitch))]
    float slitPitch = 250;
    float defaultPitch = 250;

    [SerializeField, Range(20, 500), FieldChangeCallback(nameof(SlitWidth))]
    float slitWidth = 10;
    float defaultWidth = 10;

    int defaultSources = 2;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(NumSources))] private int numSources = 2;
    [SerializeField] TextMeshProUGUI lblSourceCount;

    [SerializeField] UdonSlider scaleSlider;
    private bool iHaveScaleControl = false;
    [SerializeField, Range(1, 10)] float defaultScale = 24;
    [SerializeField, Range(1, 10), FieldChangeCallback(nameof(SimScale))] public float simScale = 24;

    [SerializeField, FieldChangeCallback(nameof(ContrastVal))]
    public float contrastVal = 40f;

    [SerializeField] UdonSlider contrastSlider;

    [Header("Serialized for monitoring in Editor")]
    //[SerializeField]
    private Material matPanel = null;
   // [SerializeField]
    private Material matSimControl = null;
    //[SerializeField]
    private Material matSimDisplay = null;
    //[SerializeField, Tooltip("Check to invoke CRT Update")]
    private bool crtUpdateNeeded = false;
   // [SerializeField]
    private bool iHaveCRT = false;
    //[SerializeField]
    private bool iHavePanelMaterial = false;
    //[SerializeField]
    private bool iHaveSimDisplay = false;
    //[SerializeField]
    private bool iHaveSimControl = false;
    [SerializeField]
    private bool CRTUpdatesMovement = false;
    private VRCPlayerApi player;
    private bool iamOwner = false;

    private float prevVisibility = -1;
    private void reviewContrast()
    {
        if (!iHaveSimDisplay)
            return;
        float targetViz = (contrastVal / 50);
        if (targetViz == prevVisibility)
            return;
        prevVisibility = targetViz;
        matSimDisplay.SetFloat("_Visibility", targetViz);
    }

    private float ContrastVal
    {
        get => contrastVal;
        set
        {
            contrastVal = value;
            reviewContrast();
        }
    }

    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    private void configureSimControl(bool scrollingDisplay)
    {
        //Debug.Log("configureSimControl(" + scrollingDisplay.ToString() + ")");
        CRTUpdatesMovement = false;
        if (!scrollingDisplay)
        { 
            if (iHaveCRT)
            { // Display mode and wave speed controls get handled by the panel material
                matSimDisplay = simCRT.material;
                matSimControl = simCRT.material;
                CRTUpdatesMovement = true;
                simCRT.material.SetFloat("_OutputRaw", 0);
            }
            else
            {
                // No CRT and not a compatible display
                matSimDisplay = null;
                matSimControl = null;
                Debug.LogWarning("Warning:configureSimControl() no Interference control/display material");
            }
        }
        else 
        {
            if (iHaveCRT)
            { // Display mode and wave speed controls get handled by the CRT
                matSimDisplay = matPanel;
                matSimControl = simCRT.material;
                matSimControl.SetFloat("_OutputRaw", 1);
                crtUpdateNeeded= true;
            }
            else
            {
                matSimDisplay = matPanel;
                matSimControl = matPanel;
            }

        }
        iHaveSimControl = matSimControl != null;
        iHaveSimDisplay = matSimDisplay != null;
    }

    private bool PanelHasScrollingMaterial
    {
        get
        {
            if (!iHavePanelMaterial)
            {
                Debug.LogWarning(gameObject.name + ": no Panel material");
                return false;
            }
            if (matPanel.HasProperty("_ShowReal"))
                return true; 
            return false;
        }
    }

    private void UpdateWaveSpeed()
    {
        if (iHaveSimDisplay)
            matSimDisplay.SetFloat("_Frequency", playSim ?  waveSpeed * defaultLambda / lambda : 0f);
        crtUpdateNeeded |= iHaveCRT;
    }

    float WaveSpeed
    {
        get => waveSpeed;
        set
        {
            waveSpeed = Mathf.Clamp(value,-5,5);
            UpdateWaveSpeed();
        }
    }

    private bool PlaySim
    {
        get => playSim;
        set
        {
            playSim = value;
            if (!iamOwner)
            {
                if (togPlay != null && value && !togPlay.isOn)
                    togPlay.SetIsOnWithoutNotify(true);
                if (togPause != null && !value && !togPause.isOn)
                    togPause.SetIsOnWithoutNotify(true);
            }
            UpdateWaveSpeed();
            RequestSerialization();
        }
    }

    private void updateGrating()
    {
        if (!iHaveSimControl)
            return;
        matSimControl.SetFloat("_SlitPitch", slitPitch);
        crtUpdateNeeded |= iHaveCRT;
        if (numSources > 1 && slitPitch <= slitWidth)
        {
            float gratingWidth = (numSources - 1) * slitPitch + slitWidth;
            matSimControl.SetInteger("_SlitCount", 1);
            matSimControl.SetFloat("_SlitWidth", gratingWidth);
            return;
        }
        matSimControl.SetInteger("_SlitCount", numSources);
        matSimControl.SetFloat("_SlitWidth", slitWidth);
    }
    public int NumSources
    {
        get => numSources;
        set
        {
            if (value < 1)
                value = 1;
            if (value > 17)
                value = 17;
            numSources = value;
            updateGrating();
            if (lblSourceCount != null)
                lblSourceCount.text = numSources.ToString();
            RequestSerialization();
        }
    }
    
    public void incSources()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        NumSources = numSources + 1;
    }

    public void decSources()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        NumSources = numSources - 1;
    }

    private void updateDisplayTxture(int displayMode)
    {
        if (!iHaveSimDisplay)
        {
            Debug.LogWarning(gameObject.name + ": no Display material");
            return;
        }
        //Debug.Log(gameObject.name + "updateDisplayTxture(Mode 2#" + displayMode.ToString() + ")");
        matSimDisplay.SetFloat("_ShowCRT", displayMode >= 0 ? 1f : 0f);
        matSimDisplay.SetFloat("_ShowReal", displayMode == 0 || displayMode == 1 || displayMode >= 4 ? 1f : 0f);
        matSimDisplay.SetFloat("_ShowImaginary", displayMode == 2 || displayMode == 3 || displayMode >= 4 ? 1f : 0f);
        matSimDisplay.SetFloat("_ShowSquare", displayMode == 1 || displayMode == 3 || displayMode == 5 ? 1f : 0f);
    }
    private int DisplayMode
    {
        get => displayMode;
        set
        {
            displayMode = value;
            updateDisplayTxture(displayMode);
            crtUpdateNeeded |= iHaveCRT;
            switch (displayMode)
            {
                case 0:
                    if (iHaveTogReal && !togReal.isOn)
                        togReal.SetIsOnWithoutNotify(true);
                    break;
                case 1:
                    if (iHaveTogRealPwr&& !togRealPwr.isOn)
                        togRealPwr.SetIsOnWithoutNotify(true);
                    break;
                case 2:
                    if (iHaveTogIm && !togImaginary.isOn)
                        togImaginary.SetIsOnWithoutNotify(true);
                    break;
                case 3:
                    if (iHaveTogImPwr && !togImPwr.isOn)
                        togImPwr.SetIsOnWithoutNotify(true);
                    break;
                case 4:
                    if (iHaveTogAmp && !togAmplitude.isOn)
                        togAmplitude.SetIsOnWithoutNotify(true);
                    break;
                default:
                    if (iHaveTogProb && !togProbability.isOn)
                        togProbability.SetIsOnWithoutNotify(true);
                    break;
            }
            RequestSerialization();
        }
    }

    public void onPlaySim()
    {
        if (togPlay == null) 
            return;
        if (togPlay.isOn && !playSim)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlaySim = true;
        }
    }

    public void onPauseSim()
    {
        if (togPause == null)
            return;
        if (togPause.isOn && playSim)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlaySim = false;
        }
    }

    public float Lambda
    {
        get => lambda;
        set
        {
            lambda = value;
            if (iHaveSimControl)
                matSimControl.SetFloat("_Lambda", lambda);
            crtUpdateNeeded |= iHaveCRT;
            UpdateWaveSpeed();
        }
    }


    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            slitPitch = value;
            updateGrating();
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            slitWidth = value;
            updateGrating() ;
        }
    }


    public float SimScale
    {
        get => simScale;
        set
        {
            simScale = value;
            if (iHaveSimControl)
                matSimControl.SetFloat("_Scale", simScale);
            crtUpdateNeeded |= iHaveCRT;
        }
    }

    public void onMode()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        if (iHaveTogReal && togReal.isOn && displayMode != 0)
        {
            DisplayMode = 0;
            return;
        }
        if (iHaveTogIm && togImaginary.isOn && displayMode != 2)
        {
            DisplayMode = 2;
            return;
        }
        if (iHaveTogRealPwr && togRealPwr.isOn && displayMode != 1)
        {
            DisplayMode = 1;
            return;
        }
        if (iHaveTogImPwr && togImPwr.isOn && displayMode != 3)
        {
            DisplayMode = 3;
            return;
        }
        if (iHaveTogAmp && togAmplitude.isOn && displayMode != 4)
        {
            DisplayMode = 4;
            return;
        }
        if (iHaveTogProb && togProbability.isOn && displayMode != 5)
        {
            DisplayMode = 5;
            return;
        }
    }

    void UpdateSimulation()
    {
        if (displayMode >= 0)
            simCRT.Update(1);
        crtUpdateNeeded = false;
        //Debug.Log(gameObject.name + "UpdateSimulation()");
    }

    float waveTime = 0;
    float delta;

    private void Update()
    {
        if (!iHaveCRT)
            return;
        if (CRTUpdatesMovement)
        {
            if (playSim && displayMode >= 0 && displayMode < 4)
            {
                delta = Time.deltaTime;
                waveTime -= delta;
                if (waveTime < 0)
                {
                    waveTime += dt;
                    crtUpdateNeeded |= true;
                }
            }
        }
        if (crtUpdateNeeded)
        {
           UpdateSimulation();
        }
    }
    void Start()
    {
        player = Networking.LocalPlayer;
        ReviewOwnerShip();

        iHaveTogReal = togReal != null;
        iHaveTogIm = togImaginary != null;
        iHaveTogRealPwr = togRealPwr != null;
        iHaveTogImPwr = togImPwr != null;
        iHaveTogProb = togProbability != null;
        iHaveTogAmp = togAmplitude != null;

        if (thePanel != null)
            matPanel = thePanel.material;
        iHavePanelMaterial = matPanel != null;

        if (simCRT != null)
            iHaveCRT = true;
        //Debug.Log(gameObject.name + "Start() iHaveCRT=" + iHaveCRT.ToString());
        configureSimControl(PanelHasScrollingMaterial);
        if (iHaveSimControl)
        {
            defaultWidth = matSimControl.GetFloat("_SlitWidth");
            defaultLambda = matSimControl.GetFloat("_Lambda");
            defaultScale = matSimControl.GetFloat("_Scale");
            defaultPitch = matSimControl.GetFloat("_SlitPitch"); 
            defaultSources = matSimControl.GetInteger("_SlitCount");
        }
        initialSpeed = waveSpeed;
        if (iHaveSimDisplay)
            initialSpeed = matSimDisplay.GetFloat("_Frequency");

        slitPitch = defaultPitch;
        slitWidth = defaultWidth;
        numSources = defaultSources;


        iHaveWidthControl = widthSlider != null;
        iHaveSpeedControl = speedSlider != null;
        iHaveLambdaControl = momentumSlider != null;
        iHaveScaleControl = scaleSlider != null;
        ContrastVal = contrastVal;
        if (contrastSlider != null)
            contrastSlider.SetValue(contrastVal);

        if (iHaveScaleControl)
        {
            scaleSlider.SetLimits(1, 10);
            scaleSlider.SetValue(simScale);
        }
        iHavePitchControl = pitchSlider != null;
        if (iHaveSimDisplay && displayMode < 0)
        {
            int dMode = Mathf.RoundToInt(matSimDisplay.GetFloat("_ShowReal")) > 0 ? 1 : 0;
            dMode += Mathf.RoundToInt(matSimDisplay.GetFloat("_ShowImaginary")) > 0 ? 2 : 0;

            int nSq = Mathf.RoundToInt(matSimDisplay.GetFloat("_ShowSquare")) > 0 ? 1 : 0;
            switch (dMode)
            {
                case 1:
                    displayMode = nSq;
                    break;
                case 2: 
                    displayMode = 2 + nSq;
                    break;
                case 3:
                    displayMode = 4 + nSq;
                    break;
                default:
                    displayMode = 0;
                    break;
            }
        }
        Lambda = defaultLambda;
        if (iHaveLambdaControl)
            momentumSlider.SetValue(defaultLambda);
        WaveSpeed = initialSpeed;
        if (iHaveSpeedControl)
            speedSlider.SetValue(initialSpeed);
        SimScale = defaultScale;
        if (iHaveScaleControl)
            scaleSlider.SetValue(defaultScale);
        SlitPitch = defaultPitch;
        if (iHavePitchControl)
        {
            pitchSlider.SetLimits(20,500);
            pitchSlider.SetValue(defaultPitch);
        }
        NumSources = defaultSources;
        SlitWidth = defaultWidth;
        if (iHaveWidthControl) 
            widthSlider.SetValue(defaultWidth);
        crtUpdateNeeded |= iHaveCRT;
        DisplayMode = displayMode;
    }
}
