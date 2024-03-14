
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
    BallisticScatter particleSim;
    private bool iHaveParticleSim;

    //CustomRenderTexture particleCRT;
    //[SerializeField]
    //Material matParticleCRT;

    [SerializeField]
    CustomRenderTexture waveCRT;
    [SerializeField]
    Material matWaveCRT;
    private bool iHaveWaveCRT;

    [Header("Grating Properties")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount))]
    private int slitCount;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch;
    [SerializeField, Range(1, 5),UdonSynced, FieldChangeCallback(nameof(SimScale))]
    private float simScale;
    [SerializeField, Range(1, 8), UdonSynced, FieldChangeCallback(nameof(Momentum))]
    private float momentum;
    [SerializeField]
    private bool crtUpdateRequired;

    [Header("UI Controls")]
    // UI
    [SerializeField]
    UdonSlider pitchSlider;
    [SerializeField]
    UdonSlider widthSlider;
    [SerializeField]
    UdonSlider momentumSlider;
    [SerializeField]
    TextMeshProUGUI lblMomentum;
    [SerializeField]
    float lambda;
    [SerializeField]
    TextMeshProUGUI lblLambda;
    [SerializeField]
    UdonSlider scaleSlider;
    [SerializeField]
    TextMeshProUGUI lblSlitCount;
    [Header("Particle Properties")]
    [SerializeField]
    private float particleMass = 1; // Speed 1 matches wavelength 10
    [SerializeField]
    private float maxMomentum = 8;
    [Header("Constants"), SerializeField]

    private int MAX_SLITS = 17;
    [SerializeField, Tooltip("Planck Value for simulation; e.g. lambda=h/p"), UdonSynced, FieldChangeCallback(nameof(PlanckSim))]
    private float planckSim = 12;

    [Header("Working Value Feedback")]
    [SerializeField]
    private float particleK;

    private VRCPlayerApi player;
    private bool iamOwner = false;

    private bool iHavePitchSlider;
    private bool iHaveWidthSlider;
    private bool iHaveMomentumSlider;
    private bool iHaveScaleSlider;

    float PlanckSim
    {
        get => planckSim;
        set
        {
            planckSim = value;
            if (iHaveParticleSim)
                particleSim.PlanckSim = value;
            updateLambda();
            RequestSerialization();
        }
    }

    private void updateLambda()
    { //_particleK("pi*p/h", float)
        lambda = planckSim/momentum;
        if (lblLambda != null)
            lblLambda.text = string.Format("λ={0:0.0}", lambda);
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_Lambda",lambda);
            crtUpdateRequired = true;
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

    private void loadCRTs()
    {
        if (iHaveParticleSim)
        {
            particleSim.SetGrating(slitCount, slitWidth, slitPitch, maxMomentum);
            particleSim.SimScale = simScale;
            particleSim.PlanckSim = planckSim;

        }
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_SlitCount", slitCount);
            matWaveCRT.SetFloat("_SlitWidth", slitWidth);
            matWaveCRT.SetFloat("_SlitPitch", slitPitch);
            matWaveCRT.SetFloat("_Scale", simScale);
            waveCRT.Update(1);
        }
        crtUpdateRequired = false;
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
                crtUpdateRequired = true;
                if (iHaveParticleSim)
                    particleSim.SlitCount = slitCount;
                if (iHaveWaveCRT)
                    matWaveCRT.SetFloat("_SlitCount", slitCount);
            }
            if (lblSlitCount != null)
                lblSlitCount.text = value.ToString();
            RequestSerialization();
        }
    }

    public float SlitWidth
    {
        get => slitWidth;
        private set
        {
            if (value != slitWidth)
            {
                crtUpdateRequired = true;
                slitWidth = value;
                if (iHaveParticleSim)
                    particleSim.SlitWidth = slitWidth;
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
                if (iHaveParticleSim)
                    particleSim.SlitPitch = slitPitch;
                if (iHaveWaveCRT)
                    matWaveCRT.SetFloat("_SlitPitch", slitPitch);
                crtUpdateRequired = true;
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
            if (simScale != value)
            {
                simScale = value;
                crtUpdateRequired = true;
            }
            if (iHaveParticleSim)
                particleSim.SimScale = simScale;
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_Scale", simScale);
            if (iHaveScaleSlider && !scaleSlider.PointerDown && scaleSlider.CurrentValue != simScale)
                scaleSlider.SetValue(simScale);
        }
    }

    private float Momentum
    {
        get { return momentum; }
        set
        {
            value = Mathf.Max(value, 1f);
            momentum = value;
            if (iHaveMomentumSlider && !momentumSlider.PointerDown && momentumSlider.CurrentValue != momentum)
                momentumSlider.SetValue(momentum);
            if (lblMomentum != null)
                lblMomentum.text = string.Format("p={0:0.0}", momentum);
            if (iHaveParticleSim)
                particleSim.ParticleMomentum = value;
            updateLambda();
            RequestSerialization();
        }
    }

    private void Update()
    {
        if (!crtUpdateRequired)
            return;
        crtUpdateRequired = false;
        if (iHaveWaveCRT)
            waveCRT.Update(1);
    }

    void Start()
    {
        iHaveParticleSim = particleSim != null;
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
        if (iHaveMomentumSlider)
            maxMomentum = momentumSlider.MaxValue;
        iHaveScaleSlider = scaleSlider != null;
        loadCRTs();
        SlitCount = slitCount;
        SlitWidth = slitWidth;
        SlitPitch = slitPitch;
        Momentum = momentum;
        SimScale = simScale;
    }
}
