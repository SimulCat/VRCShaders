
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CRTWaveDemo : UdonSharpBehaviour
{
    [SerializeField, Tooltip("Custom Render texture")]
    private CustomRenderTexture simCRT;
    
    [SerializeField, Tooltip("Display Mesh")]
    private MeshRenderer waveMesh;
    
    [SerializeField] private bool playPhase = false;
    
    [SerializeField,Tooltip("Texture update interval (Passive Phase)"),Range(0.01f, 0.2f)] float dt = 0.1f;

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
    bool iHaveToImPwr = false;
    [SerializeField] Toggle togAmplitude;
    bool iHaveTogAmp = false;
    [SerializeField] Toggle togProbability;
    bool iHaveTogProb = false;

    [SerializeField] UdonSlider scaleSlider;
    bool iHaveScale = false;

    [Header("Serialized for monitoring in Editor")]
    [SerializeField]
    private Material matPanel = null;
    [SerializeField]
    private Material matSimControl = null;
    [SerializeField]
    private Material matSimDisplay = null;
    [SerializeField, Tooltip("Check to invoke CRT Update")] 
    private bool crtUpdateNeeded = false;
    [SerializeField]
    private bool iHaveCRT = false;
    [SerializeField]
    private bool iHaveSimControl = false;
    [SerializeField]
    private bool useDisplayMode = false;
    [SerializeField]
    private bool useDisplayStates = false;
    [SerializeField]
    private bool iHavePanelMaterial = false;
    [SerializeField]
    private bool CRTUpdatesMovement;

    private int DisplayMode
    {
        get => displayMode;
        set
        {
            displayMode = value;
            if (useDisplayMode)
                matSimDisplay.SetFloat("_DisplayMode", value);
            if (useDisplayStates)
            {
                switch (displayMode)
                {
                    case 0:
                        matSimDisplay.SetFloat("_ShowReal", 1f);
                        matSimDisplay.SetFloat("_ShowImaginary", 0f);
                        matSimDisplay.SetFloat("_ShowSquare", 0f);
                        break;
                    case 1:
                        matSimDisplay.SetFloat("_ShowReal", 1f);
                        matSimDisplay.SetFloat("_ShowImaginary", 0f);
                        matSimDisplay.SetFloat("_ShowSquare", 1f);
                        break;
                    case 2:
                        matSimDisplay.SetFloat("_ShowReal", 0f);
                        matSimDisplay.SetFloat("_ShowImaginary", 1f);
                        matSimDisplay.SetFloat("_ShowSquare", 0f);
                        break;
                    case 3:
                        matSimDisplay.SetFloat("_ShowReal", 0f);
                        matSimDisplay.SetFloat("_ShowImaginary", 1f);
                        matSimDisplay.SetFloat("_ShowSquare", 1f);
                        break;
                    case 4:
                        matSimDisplay.SetFloat("_ShowReal", 1f);
                        matSimDisplay.SetFloat("_ShowImaginary", 1f);
                        matSimDisplay.SetFloat("_ShowSquare", 0f);
                        break;
                    case 5:
                        matSimDisplay.SetFloat("_ShowReal", 1f);
                        matSimDisplay.SetFloat("_ShowImaginary", 1f);
                        matSimDisplay.SetFloat("_ShowSquare", 1f);
                        break;
                    default:
                        matSimDisplay.SetFloat("_ShowReal", 0f);
                        matSimDisplay.SetFloat("_ShowImaginary", 0f);
                        matSimDisplay.SetFloat("_ShowSquare", 0f);
                        break;
                }
            }
            switch (displayMode)
            {
                case 0:
                    if (iHaveTogReal && !togReal.isOn)
                        togReal.isOn = true;
                    break;
                case 1:
                    if (iHaveTogRealPwr && !togRealPwr.isOn)
                        togRealPwr.isOn = true;
                    break;
                case 2:
                    if (iHaveTogIm && !togImaginary.isOn)
                        togImaginary.isOn = true;
                    break;
                case 3:
                    if (iHaveToImPwr && !togImPwr.isOn)
                        togImPwr.isOn = true;
                    break;
                case 4:
                    if (iHaveTogAmp && !togAmplitude.isOn)
                        togAmplitude.isOn = true;
                    break;
                default:
                    if (iHaveTogProb && !togProbability.isOn)
                        togProbability.isOn = true;
                    break;
            }
            RequestSerialization();
        }
    }

    // Display mode Toggles are all set to send custom event to this function
    public void togMode()
    {
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
        if (iHaveToImPwr && togImPwr.isOn && displayMode != 3)
        {
            DisplayMode = 3;
            return;
        }
        if (iHaveTogAmp &&  togAmplitude.isOn && displayMode != 4)
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

    private void checkPanelType()
    {
        if (!iHavePanelMaterial)
        {
            useDisplayMode = false;
            useDisplayStates = false;
            Debug.LogWarning("CRTWaveDemo: No Wave Display Material");
            return;
        }
        useDisplayStates = matPanel.HasProperty("_ShowReal");
        //Debug.Log("CRTWaveDemo: useDisplayStates" + useDisplayStates.ToString());
        useDisplayMode = matPanel.HasProperty("_DisplayMode");
        //Debug.Log("CRTWaveDemo: useDisplayMode" + useDisplayMode.ToString());
    }

    private void configureSimControl()
    {
        CRTUpdatesMovement = false;
        if (useDisplayMode || useDisplayStates)
        { // Display mode and wave speed controls get handled by the Panel/Mesh Shader
            if (iHaveCRT)
            {
                matSimDisplay = matPanel;
                matSimControl = simCRT.material;
                if (iHaveCRT && iHaveSimControl)
                    matSimControl.SetFloat("_OutputRaw", 1);
                crtUpdateNeeded = true;
            }
            else
            {
                matSimDisplay = matPanel;
                matSimControl = matPanel;
            }
        }
        else
        { // Display mode and wave speed controls get handled by the panel material
            if (iHaveCRT)
            {
                matSimDisplay = simCRT.material;
                matSimControl = simCRT.material;
                useDisplayStates = matSimDisplay.HasProperty("_ShowReal");
                useDisplayMode = matSimDisplay.HasProperty("_DisplayMode");

                CRTUpdatesMovement = false;
                matSimControl.SetFloat("_OutputRaw", 0);
                crtUpdateNeeded = true;
            }
            else
            {
                // No CRT and not a compatible display
                matSimDisplay = null;
                matSimControl = null;
                Debug.Log("Warning:configureSimControl() no Interference control/display material");
            }
        }
        iHaveSimControl = matSimControl != null;
    }


    void UpdateSimulation()
    {
        crtUpdateNeeded = false;
        if (!iHaveCRT)
            return;
        simCRT.Update(1);
    }

    float waveTime = 0;
    float delta;

    private void Update()
    {
        if (!iHaveCRT) 
            return;
        if (CRTUpdatesMovement)
        {
            if (playPhase && displayMode >= 0 && displayMode < 4)
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
        iHaveTogReal = togReal != null;
        iHaveTogIm = togImaginary != null;
        iHaveTogRealPwr = togRealPwr != null;
        iHaveToImPwr = togImPwr != null;
        iHaveTogAmp = togAmplitude != null;
        iHaveTogProb = togProbability != null;
        iHaveScale = scaleSlider != null;

        if (simCRT != null)
        {
            iHaveCRT = true;
            matSimControl = simCRT.material;
            simCRT.Initialize();
        }
        iHaveSimControl = matSimControl != null;
        if (waveMesh != null)
            matPanel = waveMesh.material;
        iHavePanelMaterial = matPanel != null;
        checkPanelType();
        configureSimControl();
        DisplayMode = displayMode;
        crtUpdateNeeded = true;
    }
}
