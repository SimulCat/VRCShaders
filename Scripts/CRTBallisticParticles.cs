
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

    [SerializeField, FieldChangeCallback(nameof(SlitCount))]
    private int slitCount;
    [SerializeField, FieldChangeCallback(nameof(SlitWidth))]
    private float slitWidth;
    [SerializeField, FieldChangeCallback(nameof(SlitPitch))]
    private float slitPitch;
    [SerializeField, FieldChangeCallback(nameof(SimScale))]
    private float simScale;


    [SerializeField, Tooltip("Planck Value for simulation; lambda=h/p"), UdonSynced, FieldChangeCallback(nameof(PlanckSim))]
    private float planckSim = 12;

    float PlanckSim
    {
        get=>planckSim; 
        set
        {
            planckSim = value;
            updateParticleK();
        }
    }

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
            updateParticleK();
        }
    }

    [Header("Constants"), SerializeField]
    private int MAX_SLITS = 17;
    [SerializeField]
    private float particleMass = 1; // Speed 1 matches wavelength 10

    [Header("Useful Feedback (Debug)")]
    [SerializeField]
    private float particleMomentum;
    [SerializeField]
    private float particleK;


    public float SimScale
    {
        get => simScale; 
        set 
        { 
            gratingChanged = true;
            if (matCRT != null)
                matCRT.SetFloat("_Scale", simScale);
            simScale = value; 
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
                gratingChanged = true;
                if (iHaveCRT)
                    matCRT.SetFloat("_SlitPitch", value);
            }
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
        }
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
                if (iHaveCRT)
                    matCRT.SetFloat("_SlitCount", slitCount);
                gratingChanged = true;
            }
        }
    }

    private void updateParticleK()
    { //_particleK("pi*p/h", float)
        particleK = Mathf.PI * (particleMass * particleSpeed) / planckSim;
        if (iHaveCRT)
        {
            matCRT.SetFloat("_ParticleK",particleK);
            gratingChanged = true;
        }
    }

    public void init()
    {
        if (iHaveCRT)
        {
            updateParticleK();
            matCRT.SetFloat("_SlitCount", slitCount);
            matCRT.SetFloat("_SlitPitch", slitPitch);
            matCRT.SetFloat("_SlitWidth", slitWidth);
            matCRT.SetFloat("_Scale", simScale);
        }

    }
    private void Update()
    {
        if (!gratingChanged)
            return;
        simCRT.Update(1);
        gratingChanged = false;
    }
    void Start()
    {
        if (simCRT != null)
            matCRT = simCRT.material;
        iHaveCRT = (simCRT != null && matCRT != null);
        /*
        if (iHaveCRT)
        {
            SlitCount = Mathf.RoundToInt(matCRT.GetFloat("_SlitCount"));
            SlitPitch = matCRT.GetFloat("_SlitPitch");
            SlitWidth = matCRT.GetFloat("_SlitWidth");
            SimScale = matCRT.GetFloat("_Scale");
        }
        */
        gratingChanged |= true;
    }
}
