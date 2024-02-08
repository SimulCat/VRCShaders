
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
    [SerializeField, Tooltip("Use Render texture mode")] bool useCRT = false;
    [Tooltip("CRT Material")] public Material matCRT = null;
    [Tooltip("Display Panel")] public MeshRenderer thePanel;
    private Material matDisplay = null;
    [Tooltip("Display CRT Passive Material")] public Material matPassive = null;
    [Tooltip("Display CRT Active Material")] public Material matActive = null;
    [SerializeField] public bool playPhase = false;
    [SerializeField] private bool updateNeeded = false;
    
    [SerializeField,FieldChangeCallback(nameof(DisplayOnStatic)) ] public bool displayOnStatic = false;
    [SerializeField,Tooltip("Texture update interval (Passive Phase)"),Range(0.01f, 0.2f)] float dt = 0.1f;

    private bool DisplayOnStatic
    { 
        get => displayOnStatic; 
        set 
        {  
            displayOnStatic = value;
            if (value)
            {
                if (thePanel != null && matPassive != null)
                {
                    thePanel.material = matPassive;
                }
                matDisplay = matCRT;
                if (iHaveSimMaterial)
                    matCRT.SetFloat("_OutputRaw", 0);
            }
            else
            {
                if (thePanel != null && matActive != null)
                    thePanel.material = matActive;
                matDisplay = matActive;
                if (iHaveSimMaterial)
                    matCRT.SetFloat("_OutputRaw", 1);
            }
        } 
    }

    [SerializeField]
    bool iHaveSimMaterial = false;

    void UpdateSimulation()
    {
        if (useCRT)
            simCRT.Update(1);
        updateNeeded = false;
    }

    float phaseTime = 0;
    float phaseRate = 0.6f;
    float waveTime = 0;
    float delta;

    private void Update()
    {
        if (!useCRT) return;
        if (updateNeeded)
        {
            UpdateSimulation();
        }
        if (playPhase && displayOnStatic)
        {
            delta = Time.deltaTime;
            waveTime -= delta;
            phaseTime -= delta * phaseRate;
            if (phaseTime < 0)
                phaseTime += 1;
            if (waveTime < 0)
            {
                if (iHaveSimMaterial)
                    matCRT.SetFloat("_Phase", phaseTime);
                waveTime += dt;
                if (useCRT)
                    UpdateSimulation();
            }
        }

    }

    void Start()
    {
        if (useCRT)
        {
            if (simCRT != null)
            {
                matCRT = simCRT.material;
                simCRT.Initialize();
            }
            else
                useCRT = false;
        }
        else
        {
            matCRT = thePanel.material;
        }
        if (thePanel != null)
        {
            if (matActive == null)
                matActive = thePanel.material;
            if (matPassive == null)
                matPassive = thePanel.material;
        }
        if (useCRT)
            DisplayOnStatic = displayOnStatic;
        iHaveSimMaterial = matCRT != null;
        updateNeeded = true;
    }
}
