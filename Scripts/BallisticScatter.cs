
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using static UnityEngine.ParticleSystem;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BallisticScatter : UdonSharpBehaviour
{
    [SerializeField]
    CustomRenderTexture simCRT;
    bool iHaveParticleCRT = false;
    [SerializeField]
    Material matSim = null;
    [SerializeField]
    string texName = "_MomentumTex2D";
    [SerializeField]
    bool matSimIsValid = false;
    
    [SerializeField, Tooltip("Distribution Points")]
    private int pointsWide = 256;
    [SerializeField, Tooltip("Planck Value for simulation; lambda=h/p")]
    private float planckSim = 12;


    [SerializeField,Range(1,17)]
    private int slitCount = 2;          // _SlitCount("Num Sources", float)
    [SerializeField]
    private float slitPitch = 45f;        // _SlitPitch("Slit Pitch", float)
    [SerializeField]
    private float slitWidth = 12f;        // _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
    [SerializeField, Range(1, 5)]
    private float simScale;
    [SerializeField]
    private float maxMomentum = 10f;
    [SerializeField]
    private float particleMomentum = 1;       // _ParticleK("pi*p/h", float) = 0.26179939

    [SerializeField]
    private float particleK = 0.26179939f;       // _ParticleK("pi*p/h", float) = 0.26179939
    [SerializeField] bool crtUpdateRequired = false;
    [SerializeField] bool gratingUpdateRequired = false;
    [SerializeField] Color[] texData;
    public void SetGrating(int numSlits, float widthSlit, float pitchSlits, float momentumMax)
    {
        Debug.Log(string.Format("{0} SetGrating: maxP={1} #slit1={2} width={3} pitch={4}", gameObject.name, momentumMax, numSlits, widthSlit, pitchSlits));

        bool isChanged = numSlits != slitCount || widthSlit != slitWidth || pitchSlits != slitPitch;
        isChanged |= maxMomentum != momentumMax;
        slitCount = numSlits;
        slitWidth = widthSlit;
        slitPitch = pitchSlits;
        maxMomentum = momentumMax;
        gratingUpdateRequired = isChanged;
        if (isChanged)
        {
            if (matSimIsValid) 
            {
                matSim.SetFloat("_SlitCount", slitCount);
                matSim.SetFloat("_SlitWidth", slitWidth);
                matSim.SetFloat("_SlitPitch", slitPitch);
                matSim.SetFloat("_Scale", simScale);
            }
        }
    }

    public float PlanckSim
    {
        get => planckSim;
        set
        {
            if(planckSim != value)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            planckSim = value;
        }
    }
    public float SimScale
    {
        get => simScale;
        set
        {
            if (value != simScale)
                crtUpdateRequired = true;
            simScale = value;
            if (matSimIsValid)
                matSim.SetFloat("_Scale", simScale);
        }
    }

    public int SlitCount
    {
        get => slitCount;
        set
        {
            if (value != slitCount)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitCount = value;
            if (matSimIsValid)
                matSim.SetFloat("_SlitCount", slitCount);
        }
    }
    public float SlitWidth
    {
        get => slitWidth;
        set
        {
            if (value != slitWidth)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitWidth = value;
            if (matSimIsValid)
                matSim.SetFloat("_SlitWidth", slitWidth);

        }
    }

    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            if (value != slitPitch)
            {
                gratingUpdateRequired = true;
                crtUpdateRequired = true;
            }
            slitPitch = value;
            if (matSimIsValid)
                matSim.SetFloat("_SlitPitch", slitPitch);

        }
    }

    public float ParticleMomentum
    {
        get => particleMomentum;
        set
        {
            crtUpdateRequired |= value != particleMomentum;
            particleMomentum = value;
            particleK = Mathf.PI * particleMomentum / planckSim;
            if (matSimIsValid)
                matSim.SetFloat("_ParticleK", particleK);
        }
    }

    public void DefineTexture(Material theMaterial, string thePropertyName)
    {
        matSim = theMaterial;
        texName = !string.IsNullOrWhiteSpace(thePropertyName) ? thePropertyName : "_MainTex";
        matSimIsValid = (matSim != null);
        if (matSimIsValid)
            matSimIsValid = matSim.HasProperty(texName); 
    }

    private float sampleDistribution(float crossMomentum)
    {
        float spatialK = Mathf.PI * crossMomentum / planckSim;

        float slitPhase = crossMomentum * slitWidth * particleK;

        float apertureProbSq = Mathf.Abs(slitPhase) > 0.000001f ? Mathf.Sin(slitPhase) / slitPhase : 1.0f;
        apertureProbSq *= apertureProbSq;
        float multiSlitProbSq = 1f;
        if (slitCount > 1)
        {
            float gratingPhase = crossMomentum * slitPitch * particleK;
            if (slitCount == 2)
                multiSlitProbSq = Mathf.Cos(gratingPhase) * 2;
            else
            {
                float sinGrPhase = Mathf.Sin(gratingPhase);
                multiSlitProbSq = (Mathf.Abs(sinGrPhase) < 0.000001f) ? slitCount : Mathf.Sin(slitCount * gratingPhase) / sinGrPhase;
            }
            multiSlitProbSq *= multiSlitProbSq;
        }
        return multiSlitProbSq * apertureProbSq;
    }

    public bool CreateTexture()
    {
        if (texData == null || texData.Length != pointsWide)
            texData = new Color[pointsWide];
        if (!matSimIsValid)
        {
            Debug.LogWarning(gameObject.name + "BallisticScatter->Create Texture->[No Material]");
            return false;
        }
        var tex = new Texture2D(pointsWide, 1, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color col = Color.blue;
        float sum = 0;
        for (int i = 0; i < pointsWide; i++)
        {
            float p = (maxMomentum*i)/pointsWide;
            float f = sampleDistribution(p);
            texData[i] = new Color(f, sum, p,1f);
            sum += f;
    //        xColor.r = gratingTransform[i];
    //        xColor.g = gratingTransform[i];
    //        xColor.b = reverseLookup[i];
        }
        float total = texData[pointsWide-1].g;
        //for (int i = 0;i < pointsWide; i++)
        //    texData[i].g /= total;
        tex.SetPixels(0, 0, pointsWide, 1, texData, 0);
        tex.Apply();
        matSim.SetTexture(texName, tex);
        Debug.Log(" Created Texture: [" + texName + "]");
        crtUpdateRequired = true;
        return true;
    }

    private void Update()
    {
        if (gratingUpdateRequired)
        {
            CreateTexture();
            crtUpdateRequired = true;
            gratingUpdateRequired = false;
        }
        if (crtUpdateRequired)
        {
            crtUpdateRequired = false;
            if (iHaveParticleCRT)
                simCRT.Update(1);
        }
    }


    void Start()
    {
        texData = new Color[pointsWide];
        iHaveParticleCRT = simCRT != null;
        if (iHaveParticleCRT)
            matSim = simCRT.material;
        DefineTexture(matSim, texName);
    }
}
