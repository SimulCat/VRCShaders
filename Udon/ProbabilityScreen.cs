
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum ScatterType { Shadow = 0, Duane, Huygens }
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class ProbabilityScreen : UdonSharpBehaviour
{
    [SerializeField, Tooltip("CRT to generate probability/diffusion maps")]
    private CustomRenderTexture probabilityCRT;
    [SerializeField, Tooltip("CRT materials")] private Material matProbCRT;
    public Material MatProbCrt { get => matProbCRT; }

    [SerializeField] private int slitCount = 2;
    [SerializeField] private int rowCount = 1;
    [SerializeField] private float slitWidth = 0.0001f;
    [SerializeField] private float slitHeight = 0.0001f;
    [SerializeField] private float slitPitch = 0.0005f;
    [SerializeField] private float rowPitch = 0.0005f;
   // [SerializeField] GratingModel slitModel;
    [SerializeField, Tooltip("Distance from slits to screen")]
    private float slitsToScreen = 6.7f;
    [SerializeField, Tooltip("Screen dimensions in model")]
    private Vector2 screenDimensions = Vector2.one;
    [SerializeField]
    private Vector2Int _displayPixels = new Vector2Int(16, 10);
    [SerializeField] private int screenPixelsAcross = 4096;
    [SerializeField, Tooltip("Initial Particle Momentum")]
    private float particleP = 1000; //Units
    [Header("Wavelength/Momentum Units")]
    [SerializeField,FieldChangeCallback(nameof(Intensity))]
    float intensity = 12;
    [SerializeField]
    float intensityMax = 20;
    [SerializeField] private ScatterType scatteringType;

    [SerializeField,FieldChangeCallback(nameof(ShowProbability)), Tooltip("Show/Hide Probability Denisty")]
    private bool showProbability = true;
    [SerializeField, FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;

    [SerializeField]
    private SyncedToggle togProbability;
    [SerializeField, Tooltip("Intensity Control Slider")]
    private UdonSlider intensityControl;

    [SerializeField] private GiantLaser laser;

    [Tooltip("Set Scattering Shadow, Huygens, or Van Vliet/Duane")]

    [SerializeField] private Transform _targetScreen;
    [SerializeField] private GameObject _screenQuad;

    [SerializeField] private Color laserColour;
    [SerializeField] private Material _screenMaterial;
    private bool crtUpdateRequired = false;

    [SerializeField] private Vector2 _gratingSize;


    private bool _gratingChanged = false;

    private void UpdateVisibility()
    {
        if (matProbCRT != null)
        {
            matProbCRT.SetFloat("_Visibility", showProbability ? intensity : 0);
        }
        if (laser != null)
        {
            laser.LaserOn = showProbability;
            laser.LaserColor = laserColour * (intensity*2f / intensityMax);
        }
    }
    public bool ShowProbability
    {
        get => showProbability;
        set
        {
            bool chg = showProbability != value;
            showProbability = value;
            if (intensityControl != null)
                intensityControl.Interactable = showProbability;
            if (chg)
                UpdateVisibility();
            crtUpdateRequired = true;
        }
    }
    public float ScreenOffset
    {
        set
        {
            if (_targetScreen != null)
            {
                Vector3 pos = _targetScreen.localPosition;
                pos.x = value;
                _targetScreen.localPosition = pos;
            }
        }
    }

    public bool UseQuantumScatter
    {
        get => useQuantumScatter;
        set
        {
            useQuantumScatter = value;
            if (useQuantumScatter)
                setCRTPass(ScatterType.Duane);
            else
                setCRTPass(ScatterType.Shadow);
        }
    }

    public float SlitsToScreen
    {
        get => slitsToScreen;
        set
        {
            if (slitsToScreen != value)
            {
                if (matProbCRT != null)
                    matProbCRT.SetFloat("_SlitsToScreen", value);
                crtUpdateRequired = true;
            }
            slitsToScreen = value;
        }
    }
    // cumulative position expectation functions for magnified image;
    public float Intensity
    {
        get => intensity;
        set
        {
            value = Mathf.Clamp(value, 0f, intensityMax);
            bool chg = intensity != value;
            intensity = value;
            if (chg)
            {
                UpdateVisibility();
            }
            crtUpdateRequired = true;
        }
    }

    public float ParticleP
    {
        get => particleP;
        set
        {
            if (value != particleP)
            {
                particleP = value;
                if (matProbCRT != null)
                {
                    matProbCRT.SetFloat("_ParticleP", particleP);
                }
            }
            crtUpdateRequired = true;
        }
    }

    public Color LaserColour
    {
        get => laserColour;
        set
        {

            laserColour = value;
            if (matProbCRT != null)
                matProbCRT.SetColor("_Color", value);
            
            if (laser != null)
            {
                laser.LaserColor = laserColour * (intensity * 2 / intensityMax);
            }
            if (_screenMaterial != null)
            {
                if (_screenMaterial.HasProperty("_EmissionColor"))
                    _screenMaterial.SetColor("_EmissionColor", value);
                _screenMaterial.color = value * 1.25f;

            }
        }
    }

    public int RowCount
    {
        set
        {
            if (value != rowCount)
            {
                _gratingChanged = true;
                crtUpdateRequired = true;
            }

            rowCount = value;
        }
        get { return rowCount; }
    }

    public int SlitCount
    {
        set
        {
            if (value != slitCount)
            {
                _gratingChanged = true;
                crtUpdateRequired = true;
            }
            slitCount = value;
        }
        get { return slitCount; }
    }


    public float SlitWidth
    {
        set
        {
            if (slitWidth != value)
            {
                _gratingChanged = true;
                crtUpdateRequired = true;
            }
            slitWidth = value;
        }
        get { return slitWidth; }
    }
    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            if (slitPitch != value)
            {
                crtUpdateRequired = true;
                _gratingChanged = true;
            }
            slitPitch = value;
        }
    }
    public float SlitHeight
    {
        get => slitHeight;
        set
        {
            if (slitHeight != value)
            {
                _gratingChanged = true;
                crtUpdateRequired = true;
            }
            slitHeight = value;
        }
    }

    public float RowPitch
    {
        get => rowPitch;
        set
        {
            if (rowPitch != value)
            {
                _gratingChanged = true;
                crtUpdateRequired = true;
            }
            rowPitch = value;
        }
    }

    public void UpdateGratingSettings(int slits, int rows, float slitWide, float slitHigh, float slitInterval, float rowInterval)
    {
        RowCount = rows;
        SlitCount = slits;
        SlitWidth = slitWide;
        SlitHeight = slitHigh;
        SlitPitch = slitInterval;
        RowPitch = rowInterval;
        _gratingSize.x = (slitPitch * (slitCount - 1)) + slitWidth;

        if (rowCount <= 0)
            _gratingSize.y = slitPitch;
        else
            _gratingSize.y = (rowPitch * (rowCount - 1)) + slitHeight;
        /*
        if (slitModel != null)
            slitModel.UpdateGratingSettings(slits, rows, slitWidth, slitHeight, slitPitch, rowPitch);*/
    }


    private void setCRTParams()
    {
        _displayPixels.x = screenPixelsAcross;
        _displayPixels.y = (int)((_displayPixels.x * screenDimensions.y) / screenDimensions.x);

        _gratingSize.x = (slitPitch * (SlitCount - 1)) + slitWidth;
        if (rowCount <= 0)
            _gratingSize.y = rowPitch;
        else
            _gratingSize.y = (rowPitch * (rowCount - 1)) + slitHeight;
        if (matProbCRT != null)
        {
            matProbCRT.SetFloat("_SlitsToScreen", slitsToScreen);
            matProbCRT.SetFloat("_ScreenWidth", screenDimensions.x);
            matProbCRT.SetFloat("_ScreenHeight", screenDimensions.y);
            matProbCRT.SetInteger("_SlitCount", slitCount);
            matProbCRT.SetInteger("_RowCount", rowCount);
            matProbCRT.SetFloat("_SlitWidth", slitWidth);
            matProbCRT.SetFloat("_SlitHeight", slitHeight);
            matProbCRT.SetFloat("_SlitPitch", slitPitch);
            matProbCRT.SetFloat("_RowPitch", rowPitch);
        }
    }


    private void setCRTPass(ScatterType scatterType)
    {
        if (probabilityCRT == null)
            return;
        crtUpdateRequired |= scatteringType != scatterType;
        scatteringType = scatterType;
        switch (scatterType)
        {
            case ScatterType.Shadow:
                probabilityCRT.shaderPass = 1;
                break;
            case ScatterType.Huygens:
                probabilityCRT.shaderPass = 2;
                break;
            case ScatterType.Duane:
            default:
                probabilityCRT.shaderPass = 0;
                break;
        }
    }

    private bool _initialized = false;

    private ScatterType _currentScatterType = (ScatterType)(-1);
    void Update()
    {
        if (!_initialized)
            StartUp();
        if (scatteringType != _currentScatterType)
        {
            _currentScatterType = scatteringType;
            setCRTPass(scatteringType);
            crtUpdateRequired = true;
        }
        if (_gratingChanged)
        {
            setCRTParams();
            _gratingChanged = false;
            crtUpdateRequired = true;
        }
        if (crtUpdateRequired)
        {
            crtUpdateRequired = false;
            if (probabilityCRT != null)
            {
                probabilityCRT.Update(1);
            }
        }
    }

    void StartUp()
    {
        SlitWidth = slitWidth;
        SlitHeight = slitHeight;
        Intensity = intensity;
        _initialized = true;
        crtUpdateRequired = true;
    }

    private void Init()
    {
        if (intensityControl != null)
        {
            intensity = Mathf.Clamp(intensity, 0, intensityMax);
            intensityControl.SetLimits(0, intensityMax);
            intensityControl.SetValue(intensity);
            intensityControl.Interactable = showProbability;
            intensityControl.ClientVariableName = nameof(intensity);
        }
        if (togProbability != null)
        {
            togProbability.IsBoolean = true;
            togProbability.setState(showProbability);
            togProbability.ClientVariableName = nameof(showProbability);
        }
        if (_targetScreen == null)
            _targetScreen = transform.parent.Find("Target Screen");
        if (_targetScreen != null)
        {
            foreach (Transform tmpXfrm in _targetScreen)
            {
                if (tmpXfrm.name == "Front" && _screenQuad == null)
                {
                    _screenQuad = tmpXfrm.gameObject;
                }
            }
        }

        if (_screenQuad != null)
        {
            screenDimensions = new Vector2(_screenQuad.transform.localScale.x, _screenQuad.transform.localScale.y);
            _screenMaterial = _screenQuad.GetComponent<MeshRenderer>().material;
        }
        // Calculate parameters
        _displayPixels.x = screenPixelsAcross;
        _displayPixels.y = (int)((_displayPixels.x * screenDimensions.y) / screenDimensions.x);
        if (probabilityCRT != null)
        {
            probabilityCRT.Release();
            if (matProbCRT != null)
                probabilityCRT.material = matProbCRT;
            probabilityCRT.autoGenerateMips = false;
            probabilityCRT.enableRandomWrite = false;
            probabilityCRT.doubleBuffered = false;
            probabilityCRT.initializationMode = CustomRenderTextureUpdateMode.OnDemand;
            probabilityCRT.updateMode = CustomRenderTextureUpdateMode.OnDemand;
            probabilityCRT.Create();
        }
        _initialized = false;
    }
    public void Start()
    {
        Init();
        StartUp();
    }
}
