
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CRTWaveDemo : UdonSharpBehaviour
{
    [SerializeField, Tooltip("Display Mesh")]
    private MeshRenderer waveMesh;
    
    [Header("Display Mode")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(DisplayMode))]
    public int displayMode;

    [SerializeField] private float frequency;

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
    [SerializeField] Toggle togPlay;
    [SerializeField] Toggle togPause;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(PlaySim))] bool playSim;


    [SerializeField] UdonSlider contrastSlider;


    [SerializeField, FieldChangeCallback(nameof(ContrastVal))]
    public float contrastVal = 40f;

    private float prevVisibility = -1;
    private void reviewContrast()
    {
        if (matPanel == null)
            return;
        float targetViz = contrastVal / 50;
        if (targetViz == prevVisibility)
            return;
        prevVisibility = targetViz;
        matPanel.SetFloat("_Brightness", targetViz);
    }


    [Header("Serialized for monitoring in Editor")]
    [SerializeField]
    private Material matPanel = null;
    [SerializeField]
    private bool useDisplayMode = false;
    [SerializeField]
    private bool useFrequency = false;
    [SerializeField]
    private bool useDisplayStates = false;

    [SerializeField]
    private Color flowColour = Color.magenta;

    public Color FlowColour
    {
        get => flowColour; 
        set
        {
           flowColour = value;
            if (matPanel != null)
            {
                matPanel.SetColor("_ColorFlow", flowColour);
            }
        } 
    }

    public float Frequency
    {
        get => frequency;
        set
        {
            frequency = value;
            UpdateWaveFrequency();
        }
    }
    private void UpdateWaveFrequency()
    {
        if (matPanel != null && useFrequency)
            matPanel.SetFloat("_Frequency", playSim ? frequency : 0f);
    }

    private bool PlaySim
    {
        get => playSim;
        set
        {
            bool changed = playSim != value;
            playSim = value;
            UpdateWaveFrequency();
            if (changed)
            {
                if (togPlay != null && !togPlay.isOn && playSim)
                    togPlay.SetIsOnWithoutNotify(true);
                if (togPause != null && !togPause.isOn && !playSim)
                    togPause.SetIsOnWithoutNotify(true);
            }
            RequestSerialization();
        }
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

    private VRCPlayerApi player;
    private bool iamOwner = false;

    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    private int DisplayMode
    {
        get => displayMode;
        set
        {
            displayMode = value;
            if (useDisplayMode)
                matPanel.SetFloat("_DisplayMode", value);
            if (useDisplayStates)
            {
                switch (displayMode)
                {
                    case 0:
                        matPanel.SetFloat("_ShowReal", 1f);
                        matPanel.SetFloat("_ShowImaginary", 0f);
                        matPanel.SetFloat("_ShowSquare", 0f);
                        break;
                    case 1:
                        matPanel.SetFloat("_ShowReal", 1f);
                        matPanel.SetFloat("_ShowImaginary", 0f);
                        matPanel.SetFloat("_ShowSquare", 1f);
                        break;
                    case 2:
                        matPanel.SetFloat("_ShowReal", 0f);
                        matPanel.SetFloat("_ShowImaginary", 1f);
                        matPanel.SetFloat("_ShowSquare", 0f);
                        break;
                    case 3:
                        matPanel.SetFloat("_ShowReal", 0f);
                        matPanel.SetFloat("_ShowImaginary", 1f);
                        matPanel.SetFloat("_ShowSquare", 1f);
                        break;
                    case 4:
                        matPanel.SetFloat("_ShowReal", 1f);
                        matPanel.SetFloat("_ShowImaginary", 1f);
                        matPanel.SetFloat("_ShowSquare", 0f);
                        break;
                    case 5:
                        matPanel.SetFloat("_ShowReal", 1f);
                        matPanel.SetFloat("_ShowImaginary", 1f);
                        matPanel.SetFloat("_ShowSquare", 1f);
                        break;
                    default:
                        matPanel.SetFloat("_ShowReal", 0f);
                        matPanel.SetFloat("_ShowImaginary", 0f);
                        matPanel.SetFloat("_ShowSquare", 0f);
                        break;
                }
            }
            switch (displayMode)
            {
                case 0:
                    if (iHaveTogReal && !togReal.isOn)
                        togReal.SetIsOnWithoutNotify(true);
                    break;
                case 1:
                    if (iHaveTogRealPwr && !togRealPwr.isOn)
                        togRealPwr.SetIsOnWithoutNotify(true);
                    break;
                case 2:
                    if (iHaveTogIm && !togImaginary.isOn)
                        togImaginary.SetIsOnWithoutNotify(true);
                    break;
                case 3:
                    if (iHaveToImPwr && !togImPwr.isOn)
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

    public void contrastPtr()
    {
        if (!iamOwner)
            Networking.SetOwner(player,gameObject);
    }

    // Display mode Toggles are all set to send custom event to this function
    public void onPlayPhi()
    {
        if (togPlay == null || !togPlay.isOn)
            return;
        if (!playSim)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlaySim = true;
        }
    }

    public void onPausePhi()
    {
        if (togPause == null || !togPause.isOn)
            return;
        if (playSim)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlaySim = false;
        }
    }
    public void togMode()
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
        if (matPanel == null)
        {
            useDisplayMode = false;
            useDisplayStates = false;
            useFrequency = false;
            Debug.LogWarning("CRTWaveDemo: No Wave Display Material");
            return;
        }
        useDisplayStates = matPanel.HasProperty("_ShowReal");
        //Debug.Log("CRTWaveDemo: useDisplayStates" + useDisplayStates.ToString());
        useDisplayMode = matPanel.HasProperty("_DisplayMode");
        useFrequency = matPanel.HasProperty("_Frequency");
        Debug.Log("CRTWaveDemo: useFrequency" + useFrequency.ToString());
    }

    void Start()
    {
        player = Networking.LocalPlayer;
        ReviewOwnerShip();

        iHaveTogReal = togReal != null;
        iHaveTogIm = togImaginary != null;
        iHaveTogRealPwr = togRealPwr != null;
        iHaveToImPwr = togImPwr != null;
        iHaveTogAmp = togAmplitude != null;
        iHaveTogProb = togProbability != null;
        if (waveMesh != null)
            matPanel = waveMesh.material;
        ContrastVal = contrastVal;
        if (contrastSlider != null)
            contrastSlider.SetValue(contrastVal);
        checkPanelType();
        if (togPlay != null)
            PlaySim = togPlay.isOn;
        DisplayMode = displayMode;
        if (useFrequency)
            frequency = matPanel.GetFloat("_Frequency");
    }
}
