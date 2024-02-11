﻿
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
    
    [SerializeField, Tooltip("Display Panel")]
    private MeshRenderer thePanel;
    
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
    private bool iHaveDisplayControl = false;
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
            if (iHaveDisplayControl)
                matSimDisplay.SetFloat("_DisplayMode", value);
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

    private bool PanelHasVanillaMaterial
    {
        get
        {
            if (!iHavePanelMaterial)
                return true;
            return !matPanel.HasProperty("_DisplayMode");
        }
    }

    private void configureSimControl(bool vanillaDisplay)
    {
        CRTUpdatesMovement = false;
        if (vanillaDisplay)
        { // Display mode and wave speed controls get handled by the panel material
            if (iHaveCRT)
            {
                matSimDisplay = simCRT.material;
                matSimControl = simCRT.material;
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
        else
        { // Display mode and wave speed controls get handled by the CRT
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
        iHaveDisplayControl = matSimDisplay != null;
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


        if (simCRT != null)
        {
            iHaveCRT = true;
            matSimControl = simCRT.material;
            simCRT.Initialize();
        }
        iHaveSimControl = matSimControl != null;
        if (thePanel != null)
            matPanel = thePanel.material;
        iHavePanelMaterial = matPanel != null;
        configureSimControl(PanelHasVanillaMaterial);
        crtUpdateNeeded = true;
    }
}
