
using System.Xml.Linq;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class CRTBallisticParticles : UdonSharpBehaviour
{
    
    [SerializeField] CustomRenderTexture simCRT;
    [SerializeField] Material matCRT;
    [SerializeField] bool iHaveCRT;

    [SerializeField] private Vector2Int SimDimensions = new Vector2Int(512,320);

    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitCount))]
    private int slitCount;

    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch;


    [SerializeField, Range(1,10), UdonSynced, FieldChangeCallback(nameof(ParticleSpeed))]
    private float particleSpeed;
    [SerializeField]
    private bool gratingChanged = false;
    [SerializeField]
    private int pointsWide = 512;

    private float ParticleSpeed
    {
        get { return particleSpeed; }
        set 
        { 
            value = Mathf.Max(value, 1f);
            particleSpeed = value; 
            gratingChanged = true;
            speedToGratingPhase(particleSpeed);
            if (speedSlider != null && !speedSlider.PointerDown && speedSlider.CurrentValue != particleSpeed)
                speedSlider.SetValue(particleSpeed);
            RequestSerialization();
        }
    }

    [Header("UI Elements")]
    // UI
    [SerializeField]
    UdonSlider pitchSlider;
    [SerializeField]
    UdonSlider widthSlider;
    [SerializeField]
    UdonSlider speedSlider;
    [SerializeField]
    TextMeshProUGUI slitCountLabel;

    [Header("Constants"), SerializeField]
    private int MAX_SLITS = 17;
    [SerializeField]
    private float speedToSimLambda = 10; // Speed 1 matches wavelength 10

    [Header("Useful Feedback (Debug)")]
    [SerializeField]
    private float distributionTheta;
    //[SerializeField]
    //private float[] gratingTransform;
    [SerializeField]
    private float particleLambda;

    int simWidth
    {
        get => SimDimensions.y;
        set { SimDimensions.y = value; }
    }
    int simLength
    {
        get => SimDimensions.x;
        set { SimDimensions.x = value; }
    }


    public float SlitPitch
    {
        get => slitPitch;
        private set
        {
            if (value != slitPitch)
            {
                slitPitch = value;
                gratingChanged = true;
                if (iHaveCRT)
                    matCRT.SetFloat("_SlitPitch", value);
            }
            if (pitchSlider != null && !pitchSlider.PointerDown && slitPitch != pitchSlider.CurrentValue)
            {
                    pitchSlider.SetValue(value);
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
                gratingChanged = true;
                if (iHaveCRT)
                    matCRT.SetFloat("_SlitWidth", value);
            }
            if (widthSlider != null && !widthSlider.PointerDown && widthSlider.CurrentValue != widthSlider.CurrentValue)
                widthSlider.SetValue(value);
            RequestSerialization();
        }
    }

    public void incSlits()
    {
        SlitCount = slitCount + 1;
    }
    public void decSlits()
    {
        SlitCount = slitCount - 1;
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
                gratingChanged = true;
                if (slitCountLabel != null)
                {
                    slitCountLabel.text = value.ToString();
                }
            }
            RequestSerialization();
        }
    }

    private float speedToGratingPhase(float speed)
    {
        float value = Mathf.PI * speedToSimLambda / (2 * speed);
        if (iHaveCRT)
        {
            matCRT.SetFloat("_MomentumToPhase",value);
        }
        return value;
    }

    /*
    public bool CreateTexture(string texName)
    {
        if (string.IsNullOrEmpty(texName))
            texName = "_MomentumTex2D";
        if (!iHaveCRT)
        {
            Debug.LogWarning(gameObject.name + " Create Texture: [No Material]");
            return false;
        }
        var tex = new Texture2D(pointsWide, 1, TextureFormat.RGBAFloat, false);
        Color xColor = Color.clear;
        for (int i = 0; i < pointsWide; i++)
        {
            xColor.r = gratingTransform[i];
            xColor.g = gratingTransform[i];
            xColor.b = gratingTransform[i];
            tex.SetPixel(i, 0, xColor);
        }
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        matCRT.SetTexture(texName, tex);
        Debug.Log(gameObject.name + ": Created Texture: [" + texName + "]");
        return true;
    }

    private void loadGrating(float lambda)
    {
        Debug.Log(gameObject.name + ": Load Grating(lambda=" + lambda.ToString() + ")");
        if (slitWidth <= 0)
            return;
        gratingChanged = false;
        if (gratingTransform == null || gratingTransform.Length < pointsWide)
            gratingTransform = new float[pointsWide];
        // Calculte aperture parameters in terms of width per (min particleSpeed)
        Debug.Log(gameObject.name + string.Format(": loadGrating() {0} aperture={1} pitch={2}", gameObject.name, slitWidth, slitPitch));
        // Assume momentum spectrum is symmetrical so calculate from zero.
        float integralSum = 0f;
        float singleSlitValueSq;
        float manySlitValueSq;
        float dSinqd;
        float dX;
        float thisValue;
        float thetaMaxSingle = Mathf.Asin((7.0f * lambda / slitWidth));
        distributionTheta = Mathf.PI;
        if (thetaMaxSingle < Mathf.PI)
            distributionTheta = thetaMaxSingle;

        for (int nPoint = 0; nPoint < pointsWide; nPoint++)
        {
            singleSlitValueSq = 1;
            dX = (distributionTheta * nPoint) / pointsWide;
            if (nPoint != 0)
            {
                float ssTheta = dX * slitWidth;
                singleSlitValueSq = Mathf.Sin(ssTheta) / ssTheta;
                singleSlitValueSq *= singleSlitValueSq;
            }
            thisValue = singleSlitValueSq;
            if (slitCount > 1)
            {
                dSinqd = Mathf.Sin(dX * slitPitch);
                if (dSinqd == 0)
                    manySlitValueSq = slitCount;
                else
                    manySlitValueSq = Mathf.Sin(slitCount * dX * slitPitch) / dSinqd;
                manySlitValueSq *= manySlitValueSq;
                thisValue = singleSlitValueSq * manySlitValueSq;
            }
            integralSum += thisValue;
            gratingTransform[nPoint] = thisValue;
        }
        // Now Convert Distribution to a Normalized Distribution 0 to pointsWide;
        float normScale = pointsWide / integralSum;
        for (int nPoint = 0; nPoint < pointsWide; nPoint++)
            gratingTransform[nPoint] *= normScale;

        if (iHaveCRT)
            CreateTexture("_MomentumTex2D");
    }
    */
    /*
    private void reloadGrating()
    {
        quantumScatter.SetGratingByPitch(SlitCount, SlitWidth, SlitPitch, particleSpeedMin);
        Debug.Log(gameObject.name + ": reloadGrating()");
        quantumScatter.Touch();
    }
    */

    private void Update()
    {
        if (!gratingChanged)
            return;
        // if (!iHaveQuantumScatter || !quantumScatter.IsStarted)
        //     return;
        particleLambda = speedToSimLambda / particleSpeed;
        //loadGrating(particleLambda);
        speedToGratingPhase(particleSpeed);

        if (widthSlider != null && !widthSlider.PointerDown)
            widthSlider.SetValue(slitWidth);
        if (pitchSlider != null && !pitchSlider.PointerDown)
            pitchSlider.SetValue(slitPitch);
    }
    void Start()
    {
        if (simCRT != null)
            matCRT = simCRT.material;
        iHaveCRT = (simCRT != null && matCRT != null);
        if (iHaveCRT)
        {
            SimDimensions = new Vector2Int(simCRT.width, simCRT.height);

            SlitCount = Mathf.RoundToInt(matCRT.GetFloat("_NumSources"));
            SlitPitch = matCRT.GetFloat("_SlitPitch");
            SlitWidth = matCRT.GetFloat("_SlitWidth");
        }
        gratingChanged |= true;
    }
}
