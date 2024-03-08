
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ParticleWaveUI : UdonSharpBehaviour
{
    [Header("Demo Handlers")]
    [SerializeField]
    CustomRenderTexture particleCRT;
    [SerializeField]
    Material matParticleCRT;
    private bool iHaveWaveCRT;

    [SerializeField]
    CustomRenderTexture waveCRT;
    [SerializeField]
    Material matWaveCRT;
    private bool iHaveParticleCRT;

    [Header("Grating Properties")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount))]
    private int slitCount;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SimScale))]
    private float simScale;
    [SerializeField, Range(1, 10), UdonSynced, FieldChangeCallback(nameof(IncidentP))]
    private float incidentP;

    [Header("UI Controls")]
    // UI
    [SerializeField]
    UdonSlider pitchSlider;
    [SerializeField]
    UdonSlider widthSlider;
    [SerializeField]
    UdonSlider momentumSlider;
    [SerializeField]
    UdonSlider scaleSlider;
    [SerializeField]
    TextMeshProUGUI slitCountLabel;

    [Header("Constants"), SerializeField]
    private int MAX_SLITS = 17;

    private VRCPlayerApi player;
    private bool iamOwner = false;

    private bool iHavePitchSlider;
    private bool iHaveWidthSlider;
    private bool iHaveMomentumSlider;
    private bool iHaveScaleSlider;

    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    public void incSlits()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        SlitCount = slitCount + 1;
    }
    public void decSlits()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        SlitCount = slitCount - 1;
    }

    public void slidePtr()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        Debug.Log("DualUI SlidePtr Event");
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            if (value < 1)
                value = 1;
            else if (value > MAX_SLITS)
                value = MAX_SLITS;
            if (value != slitCount)
            {
                slitCount = value;
                if (iHaveParticleCRT)
                    matParticleCRT.SetFloat("_SlitCount",slitCount);
                if (iHaveWaveCRT)
                    matWaveCRT.SetFloat("_SlitCount", slitCount);
                if (slitCountLabel != null)
                    slitCountLabel.text = value.ToString();
            }
            RequestSerialization();
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        private set
        {
            Debug.Log(gameObject.name + "slitWidth=" + value.ToString());
            if (value != slitWidth)
            {
                slitWidth = value;
                if (iHaveParticleCRT)
                    matParticleCRT.SetFloat("_SlitWidth", slitWidth);
                if (iHaveWaveCRT)
                    matWaveCRT.SetFloat("_SlitWidth",slitWidth);
            }
            if (iHaveWidthSlider && !widthSlider.PointerDown && widthSlider.CurrentValue != widthSlider.CurrentValue)
                widthSlider.SetValue(value);
            RequestSerialization();
        }
    }

    public float SlitPitch
    {
        get => slitPitch;
        private set
        {
            if (value != slitPitch)
            {
                slitPitch = value;
                if (iHaveParticleCRT)
                    matParticleCRT.SetFloat("_SlitPitch", slitPitch);
                if (iHaveWaveCRT)
                    matWaveCRT.SetFloat("_SlitPitch", slitPitch);
            }
            if (iHavePitchSlider && !pitchSlider.PointerDown && slitPitch != pitchSlider.CurrentValue)
            {
                pitchSlider.SetValue(value);
            }
            RequestSerialization();
        }
    }

    public float SimScale
    {
        get => simScale;
        set
        {
            simScale = value;
            if (iHaveParticleCRT)
                matParticleCRT.SetFloat("_Scale", simScale);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_Scale", value);
            if (iHaveScaleSlider && !scaleSlider.PointerDown && scaleSlider.CurrentValue != simScale)
                scaleSlider.SetValue(simScale);
        }
    }

    private float IncidentP
    {
        get { return incidentP; }
        set
        {
            value = Mathf.Max(value, 1f);
            incidentP = value;
            if (iHaveMomentumSlider && !momentumSlider.PointerDown && momentumSlider.CurrentValue != incidentP)
                momentumSlider.SetValue(incidentP);
        }
    }

    void Start()
    {
        iHaveParticleCRT = particleCRT != null;
        if (iHaveParticleCRT)
        {
            matParticleCRT = particleCRT.material;
            iHaveParticleCRT = matParticleCRT != null;
        }    
        iHaveWaveCRT = waveCRT != null;
        if (iHaveWaveCRT)
        {
            matWaveCRT = waveCRT.material;
            iHaveWaveCRT = matWaveCRT != null;
        }
        player = Networking.LocalPlayer;
        ReviewOwnerShip();

        iHavePitchSlider = pitchSlider != null;
        iHaveWidthSlider = widthSlider != null;
        iHaveMomentumSlider = momentumSlider != null; 
        iHaveScaleSlider = scaleSlider != null;
    }
}
