
using System;
using UdonSharp;
using UnityEngine;
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
    
    [Header("Serialized for monitoring in Editor")]
    [SerializeField]
    private Material matPanel = null;
    [SerializeField]
    private Material matCRT = null;
    [SerializeField]
    private Material matDisplayControl = null;
    [SerializeField, Tooltip("Check to invoke CRT Update")] 
    private bool crtUpdateNeeded = false;
    [SerializeField]
    private bool iHaveCRT = false;
    [SerializeField]
    private bool iHaveCrtMaterial = false;
    [SerializeField]
    private bool iHavePanelMaterial = false;
    [SerializeField]
    private bool iHaveDisplayControl = false;

    private bool PanelHasVanillaMaterial
    {
        get
        {
            if (!iHavePanelMaterial)
                return true;
            return !matPanel.HasProperty("_DisplayMode");
        }
    }

    private void configureCRTMode(bool vanillaDisplay)
    {
        if (!iHaveCrtMaterial)
            return;
        if (vanillaDisplay)
        { // Display mode and wave speed controls get handled by the panel material
            matDisplayControl = matCRT;
            if (iHaveCrtMaterial)
                matCRT.SetFloat("_OutputRaw", 0);
        }
        else
        { // Display mode and wave speed controls get handled by the CRT
            matDisplayControl = matPanel;
            if (iHaveCrtMaterial)
                matCRT.SetFloat("_OutputRaw", 1);
        }
        iHaveDisplayControl = matDisplayControl != null;
    }


    void UpdateSimulation()
    {
        crtUpdateNeeded = false;
        if (!iHaveCRT)
            return;
        simCRT.Update(1);
    }

    float phaseTime = 0;
    float phaseRate = 0.6f;
    float waveTime = 0;
    float delta;

    private void Update()
    {
        if (!iHaveCRT) 
            return;
        if (playPhase && iHaveDisplayControl)
        {
            delta = Time.deltaTime;
            waveTime -= delta;
            phaseTime -= delta * phaseRate;
            if (phaseTime < 0)
                phaseTime += 1;
            if (waveTime < 0)
            {
                if (iHaveCrtMaterial)
                    matCRT.SetFloat("_Phase", phaseTime);
                waveTime += dt;
                UpdateSimulation();
            }
        }
        if (crtUpdateNeeded)
        {
            UpdateSimulation();
        }
    }

    void Start()
    {
        if (simCRT != null)
        {
            iHaveCRT = true;
            matCRT = simCRT.material;
            simCRT.Initialize();
        }
        iHaveCrtMaterial = matCRT != null;
        if (thePanel != null)
            matPanel = thePanel.material;
        iHavePanelMaterial = matPanel != null;
        configureCRTMode(PanelHasVanillaMaterial);
        crtUpdateNeeded = true;
    }
}
