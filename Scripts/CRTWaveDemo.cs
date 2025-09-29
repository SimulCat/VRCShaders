
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
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(PlayPhi))] bool playPhi;


    [SerializeField] UdonSlider contrastSlider;

    // Overall visibility of the diagram independent of the contrast
    [SerializeField, Tooltip("Overall visibility of pattern fades from 0 to 1"), Range(0,1),FieldChangeCallback(nameof(Visibility))]
    public float visibility = 1f;

    // Contrast value sets the brightness of the diagram
    [SerializeField, Tooltip("Contrast of pattern as percentage"),FieldChangeCallback(nameof(BrightPercent))]
    public float brightPercent = 40f;

    private void reviewContrast()
    {
        if (matPanel == null)
            return;
        matPanel.SetFloat("_Brightness", (brightPercent / 50) * visibility);
    }

    [Header("Mode Colours")]

    [SerializeField]
    private Color flowColour = Color.magenta;

    [Header("Serialized for monitoring in Editor")]
    [SerializeField]
    private Material matPanel = null;
    private bool iHaveDisplayMat = false;

    //[SerializeField]
    private bool hasDisplayModes = false;
    [SerializeField]
    private bool useFrequency = false;


    public Color FlowColour
    {
        get => flowColour; 
        set
        {
           flowColour = value;
            //Debug.Log(gameObject.name + "->CRTWaveDemo: FlowColour: " + flowColour);
            if (iHaveDisplayMat)
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
        //Debug.Log(gameObject.name + "->CRTWaveDemo: Frequency: " + frequency);
        if (iHaveDisplayMat && useFrequency)
            matPanel.SetFloat("_Frequency", playPhi ? frequency : 0f);
    }

    private bool PlayPhi
    {
        get => playPhi;
        set
        {
            bool changed = playPhi != value;
            playPhi = value;
            UpdateWaveFrequency();
            if (changed)
            {
                if (togPlay != null && !togPlay.isOn && playPhi)
                    togPlay.SetIsOnWithoutNotify(true);
                if (togPause != null && !togPause.isOn && !playPhi)
                    togPause.SetIsOnWithoutNotify(true);
            }
            RequestSerialization();
        }
    }

    private float BrightPercent
    {
        get => brightPercent;
        set
        {
            brightPercent = value;
            reviewContrast();
        }
    }

    private float Visibility
    {
        get => visibility;
        set
        {
            value = Mathf.Clamp01(value);
            if (visibility != value)
            {
                visibility = value;
                reviewContrast();
            }
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
            if (hasDisplayModes)
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
    public void onPlay()
    {
        if (togPlay == null || !togPlay.isOn)
            return;
        if (!playPhi)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlayPhi = true;
        }
    }

    public void onPause()
    {
        if (togPause == null || !togPause.isOn)
            return;
        if (playPhi)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            PlayPhi = false;
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
        iHaveDisplayMat = matPanel != null;
        if (!iHaveDisplayMat)
        {
            iHaveDisplayMat = false;
            hasDisplayModes = false;
            useFrequency = false;
            Debug.LogWarning("CRTWaveDemo: No Wave Display Material");
            return;
        }
        hasDisplayModes = matPanel.HasProperty("_ShowReal");
        useFrequency = matPanel.HasProperty("_Frequency");
        //Debug.Log("CRTWaveDemo: Wave Display Material Is: " + matPanel.name);
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
        checkPanelType();
        BrightPercent = brightPercent;
        if (contrastSlider != null)
            contrastSlider.SetValue(brightPercent);
        if (togPlay != null)
            PlayPhi = togPlay.isOn;
        DisplayMode = displayMode;
    }
}
