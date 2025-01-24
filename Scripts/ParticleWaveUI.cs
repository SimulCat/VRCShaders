
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
    [SerializeField]
    private UdonBehaviour vectorDrawing;

    [SerializeField]
    CustomRenderTexture waveCRT;
    private bool iHaveWaveCRT;
    [SerializeField]
    Vector2Int waveCrtSizePx = new Vector2Int(1280, 640);
    [SerializeField]
    Vector2 waveCrtDims = new Vector2(3.2f, 1.6f);
    [SerializeField]
    private CRTWaveDemo waveDemo;
    private bool iHaveWaveDemo;

    [Header("Grating Properties")]
    [SerializeField,Tooltip("Scales control settings in mm to lengths in metres")]
    private float controlScale = 0.001f;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount))]
    private int slitCount;
    [SerializeField, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch;
    [SerializeField, Range(1, 5), FieldChangeCallback(nameof(SimScale))]
    private float simScale;
    [SerializeField]
    private float momentum;
    [SerializeField, FieldChangeCallback(nameof(Lambda))]
    private float lambda;

    //[SerializeField]
    private bool crtUpdateRequired;

    [Header("UI Controls")]
    // UI
    [SerializeField]
    UdonSlider pitchSlider;
    [SerializeField]
    UdonSlider widthSlider;
    [SerializeField]
    UdonSlider lambdaSlider;
    [SerializeField]
    UdonSlider scaleSlider;
    [SerializeField]
    TextMeshProUGUI lblMomentum;
    [SerializeField]
    float waveSpeed = 10;
    [SerializeField]
    TextMeshProUGUI lblLambda;
    [SerializeField]
    TextMeshProUGUI lblSlitCount;
    [Header("Particle Properties")]

    [SerializeField,FieldChangeCallback(nameof(MinLambda))]
    private float minLambda = 20;
    [SerializeField]
    private float maxLambda = 100;
    [Header("Constants"), SerializeField]

    private int MAX_SLITS = 17;

    [Header("Working Value Feedback")]
    //[SerializeField]
    Material matWaveCRT;
    [SerializeField]
    private float waveMeshScale = 1; // Scales UI control values for wavelength and grating dimensions to CRT gridpoints units
    private VRCPlayerApi player;
    private bool iamOwner = false;

    private bool iHavePitchSlider;
    private bool iHaveWidthSlider;
    private bool iHaveLambdaSlider;
    private bool iHaveScaleSlider;

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
    private void SetColour()
    {
        float frac = Mathf.InverseLerp(minLambda,maxLambda,lambda);
        Color dColour = spectrumColour(Mathf.Lerp(400,700,frac));
        dColour.r = Mathf.Clamp(dColour.r, 0.2f, 2f);
        dColour.g = Mathf.Clamp(dColour.g, 0.2f, 2f);
        dColour.b = Mathf.Clamp(dColour.b, 0.2f, 2f);
        DisplayColour = dColour;
    }

    private void updateLambda()
    {
        if (lblLambda != null)
            lblLambda.text = string.Format("λ={0:0.0}", lambda);
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_Lambda",lambda * waveMeshScale);
            crtUpdateRequired = true;
        }
        if (vectorDrawing != null)
            vectorDrawing.SetProgramVariable<float>("lambda", lambda);
        if (iHaveWaveDemo)
        {
            waveDemo.Frequency = waveSpeed/lambda;
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

    private void initSimulations()
    {
        if (iHaveParticleSim)
        {
            particleSim.SetProgramVariable<float>("maxParticleK", 1/(minLambda*controlScale));
            particleSim.SetProgramVariable<float>("simScale", simScale);
        }
        if (iHaveWaveCRT)
        {
            matWaveCRT.SetFloat("_SlitCount", slitCount);
            matWaveCRT.SetFloat("_SlitWidth", slitWidth*waveMeshScale);
            matWaveCRT.SetFloat("_SlitPitch", slitPitch*waveMeshScale);
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
            if (vectorDrawing != null)
                vectorDrawing.SetProgramVariable<int>("slitCount", slitCount);
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
            if (vectorDrawing != null)
                vectorDrawing.SetProgramVariable<float>("slitWidth", slitWidth);
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<float>("slitWidth", slitWidth * controlScale);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_SlitWidth", slitWidth * waveMeshScale);
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
            if (vectorDrawing != null)
                vectorDrawing.SetProgramVariable<float>("slitPitch", slitPitch);
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<float>("slitPitch", slitPitch * controlScale);
            if (iHaveWaveCRT)
                matWaveCRT.SetFloat("_SlitPitch", slitPitch * waveMeshScale);
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
            RequestSerialization();
        }
    }

    private float MinLambda
    {
        get => minLambda;
        set
        {
            minLambda = Mathf.Max(value, 10); // !! Hard coded to 10mm
            if (iHaveParticleSim)
                particleSim.SetProgramVariable<float>("maxParticleK", 1 / (minLambda*controlScale));
        }
    }

    private float Lambda
    {
        get => lambda;
        set
        {
            lambda = Mathf.Clamp(value, minLambda,maxLambda);
            SetColour();
            updateLambda();
            updateMomentum();
            RequestSerialization();
        }
    }

    private void updateMomentum()
    {
        momentum = 1/(lambda*controlScale);
        if (lblMomentum != null)
            lblMomentum.text = string.Format("p={0:0.0}", momentum);
        if (iHaveParticleSim)
        {
            particleSim.SetProgramVariable<float>("particleK", momentum);
            particleSim.SetProgramVariable<Color>("displayColor", displayColour);
        }
    }

    bool started = false;
    float timeCount = 1;
    private void Update()
    {
        timeCount -= Time.deltaTime;
        if (timeCount > 0)
            return;
        timeCount += 0.0333f;
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
        }
        waveMeshScale = controlScale * waveCrtSizePx.y /(waveCrtDims.y > 0 ? waveCrtDims.y : 1);
        player = Networking.LocalPlayer;
        ReviewOwnerShip();

        iHavePitchSlider = pitchSlider != null;
        iHaveWidthSlider = widthSlider != null;
        iHaveLambdaSlider = lambdaSlider != null; 
        
        iHaveScaleSlider = scaleSlider != null;
        MinLambda = iHaveLambdaSlider ? lambdaSlider.MinValue : minLambda;
        initSimulations();
        SlitCount = slitCount;
        if (iHaveWidthSlider)
        {
            widthSlider.SetLimits(5, 75);
            widthSlider.SetValue(slitWidth);
        }
        SlitWidth = slitWidth;
        if (iHavePitchSlider)
        {
            pitchSlider.SetLimits(40, 200);
            pitchSlider.SetValue(slitPitch);
        }
        SlitPitch = slitPitch;
        if (iHaveLambdaSlider)
        {
            lambdaSlider.SetLimits(minLambda, maxLambda);
            lambdaSlider.SetValue(lambda);
        }
        Lambda = lambda;
        if (iHaveScaleSlider)
        {
            scaleSlider.SetLimits(1, 5);
            scaleSlider.SetValue(simScale);
        }
        SimScale = simScale;
        SetColour();
    }
}
