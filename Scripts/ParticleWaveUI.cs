
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
    UdonBehaviour particleSim;
    private bool iHaveParticleSim;

    //CustomRenderTexture particleCRT;
    //[SerializeField]
    //Material matParticleCRT;

    [SerializeField]
    CustomRenderTexture waveCRT;
    [SerializeField]
    Material matWaveCRT;
    private bool iHaveWaveCRT;
    [SerializeField]
    Vector2Int waveCrtSizePx = new Vector2Int(1024, 640);
    [SerializeField]
    Vector2 waveCrtDims = new Vector2(2.56f, 1.6f);
    [SerializeField]
    private CRTWaveDemo waveDemo;
    private bool iHaveWaveDemo;

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
    //[SerializeField]
    //private float particleMass = 1; // Speed 1 matches wavelength 10
    [SerializeField]
    private float maxMomentum = 8;
    [SerializeField]
    private float minMomentum = 1;
    [Header("Constants"), SerializeField]

    private int MAX_SLITS = 17;
    [SerializeField, Tooltip("Planck Value for simulation; e.g. lambda=h/p"), UdonSynced, FieldChangeCallback(nameof(PlanckSim))]
    private float planckSim = 12;

    [Header("Working Value Feedback")]
    [SerializeField]
    private float waveDimScale = 1;
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
                particleSim.SetProgramVariable<float>("planckSim",planckSim);
            updateLambda();
            RequestSerialization();
        }
    }

    public Color lerpColour(float frac)
    {
        return spectrumColour(Mathf.Lerp(700, 400, frac));
    }

    public Color spectrumColour(float wavelength, float gamma = 0.8f)
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

    [SerializeField, UdonSynced, FieldChangeCallback(nameof(DisplayColour))]
    Color displayColour;

    public Color DisplayColour
    {
        get => displayColour;
        set
        {
            displayColour = value;
            {
                if (iHaveParticleSim)
                    particleSim.SetProgramVariable<Color>("displayColour",displayColour);
                if (iHaveWaveDemo)
                    waveDemo.FlowColour = displayColour;
            }
            RequestSerialization();
        }
    }
    private void SetMomentumColour()
    {
        Color dColour = lerpColour((momentum - minMomentum) / (maxMomentum - minMomentum));
        dColour.r = Mathf.Clamp(dColour.r, 0.2f, 2f);
        dColour.g = Mathf.Clamp(dColour.g, 0.2f, 2f);
        dColour.b = Mathf.Clamp(dColour.b, 0.2f, 2f);
        DisplayColour = dColour;
    }

    private void updateLambda()
    { //_particleK("pi*p/h", float)
        lambda = planckSim/momentum;
        if (lblLambda != null)
            lblLambda.text = string.Format("λ={0:0.0}", lambda);
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_Lambda",lambda * waveDimScale);
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
            particleSim.SetProgramVariable<float>("maxMomentum", maxMomentum);
            particleSim.SetProgramVariable<float>("simScale", simScale);
            particleSim.SetProgramVariable<float>("planckSim", planckSim);

            particleSim.SetProgramVariable<int>("slitCount", slitCount);
            particleSim.SetProgramVariable<float>("slitWidth", slitWidth); // Particle Sim in in metres
            particleSim.SetProgramVariable<float>("slitPitch", slitPitch); // Particle Sim in in metres

        }
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_SlitCount", slitCount);
            matWaveCRT.SetFloat("_SlitWidth", slitWidth*waveDimScale);
            matWaveCRT.SetFloat("_SlitPitch", slitPitch*waveDimScale);
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
            }
            if (lblSlitCount != null)
                lblSlitCount.text = value.ToString();
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<int>("slitCount", slitCount);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_SlitCount", slitCount);
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
            }
            if (iHaveWidthSlider && !widthSlider.PointerDown && widthSlider.CurrentValue != widthSlider.CurrentValue)
                widthSlider.SetValue(value);
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<float>("slitWidth", slitWidth);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_SlitWidth", slitWidth * waveDimScale);
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
                crtUpdateRequired = true;
            }
            if (iHavePitchSlider && !pitchSlider.PointerDown && slitPitch != pitchSlider.CurrentValue)
            {
                pitchSlider.SetValue(value);
            }
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<float>("slitPitch", slitPitch);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_SlitPitch", slitPitch * waveDimScale);
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
                particleSim.SetProgramVariable<float>("simScale",simScale);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_Scale", simScale);
            if (iHaveScaleSlider && !scaleSlider.PointerDown && scaleSlider.CurrentValue != simScale)
                scaleSlider.SetValue(simScale);
            RequestSerialization();
        }
    }

    private float Momentum
    {
        get { return momentum; }
        set
        {
            value = Mathf.Max(value, minMomentum);
            bool changed = momentum != value;
            momentum = value;
            if (iHaveMomentumSlider && !momentumSlider.PointerDown && momentumSlider.CurrentValue != momentum)
                momentumSlider.SetValue(momentum);
            if (lblMomentum != null)
                lblMomentum.text = string.Format("p={0:0.0}", momentum);
            SetMomentumColour();
            if (iHaveParticleSim)
            {
                particleSim.SetProgramVariable<float>("particleMomentum",momentum);
                particleSim.SetProgramVariable<Color>("displayColor", displayColour);
            }
            updateLambda();
            RequestSerialization();
        }
    }

    bool started = false;
    private void Update()
    {
        if (!crtUpdateRequired)
            return;
        crtUpdateRequired = false;
        if (iHaveWaveCRT)
            waveCRT.Update(1);
        if (started)
            return;
        if (iHaveWaveDemo)
            waveDemo.FlowColour = displayColour;
    }

    void Start()
    {
        iHaveParticleSim = particleSim != null;
        iHaveWaveDemo = waveDemo != null;
        iHaveWaveCRT = waveCRT != null;
        if (iHaveWaveCRT)
        {
            matWaveCRT = waveCRT.material;
            iHaveWaveCRT = matWaveCRT != null;
            waveCrtSizePx = new Vector2Int(waveCRT.width, waveCRT.height);
            waveDimScale = (waveCrtSizePx.y/waveCrtDims.y)*.001f;
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
        SetMomentumColour();
    }
}
