using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ReciprocalView : UdonSharpBehaviour
{
    [SerializeField]
    MeshRenderer meshReciprocal;
    [SerializeField]
    MeshRenderer meshCrystal;
    [SerializeField]
    Material matReciprocal;
    [SerializeField]
    Material matCrystal;
    private VRCPlayerApi player;
    private bool iamOwner = false;

    [Header("UI Controls and Settings")]

    [SerializeField]
    UdonSlider beamAngleSlider = null;
    bool iHaveBeamAngle = false;

    [SerializeField]
    UdonSlider rotationControl = null;
    bool iHaveRotationControl = false;

    [Header("State Variables")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(CrystalTheta))] private float crystalTheta = 0;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(BeamAngle))] private float beamAngle = 0;

    [SerializeField, FieldChangeCallback(nameof(WorldBeamVector))]
    private Vector3 worldBeamVector = Vector3.right;
    [SerializeField, FieldChangeCallback(nameof(BeamMaxMinMomentum))]
    private Vector2 beamMaxMinMomentum = new Vector2(30, 5);

    [Header("Just for feedback in editor")]
    [SerializeField]
    Transform reciprocalXfrm;
    [SerializeField]
    Transform crystalXfrm;



    bool iHaveRecipMaterial;
    bool iHaveXtalMaterial;

    /// <summary>
    /// Handle Crystal angle slider event
    /// </summary>
    /// 
    public float CrystalTheta { 
        get => crystalTheta; 
        set
        {
            crystalTheta = Mathf.Clamp(value,-90,90);
            if (iHaveRotationControl && !rotationControl.PointerDown)
                rotationControl.SetValue(crystalTheta);
            Quaternion newRotation = Quaternion.Euler(Vector3.up * crystalTheta);
            if (reciprocalXfrm != null)
                reciprocalXfrm.localRotation = newRotation;
            if (crystalXfrm != null)
                crystalXfrm.localRotation = newRotation;
            setCrystalBeam();
            RequestSerialization();
        } 
    }

    public float BeamAngle
    {
        get => beamAngle;
        set
        {
            beamAngle = Mathf.Clamp(value, -90, 90);
            if (iHaveBeamAngle && !beamAngleSlider.PointerDown)
                beamAngleSlider.SetValue(beamAngle);
            float beamRadians = beamAngle*Mathf.Deg2Rad;
            WorldBeamVector = new Vector3(Mathf.Cos(beamRadians),-Mathf.Sin(beamRadians),0); 
            RequestSerialization();
        }
    }

    public void thetaPtr()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
    }

    public void beamPtr()
    {
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
    }

    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    /// <summary>
    /// Sets the direction of the incident beam, if it has changed from the previous value then recalculate the set of possible reactions to match;
    /// </summary>
    /// 
    private void setCrystalBeam()
    {
        if (!iHaveRecipMaterial) return;
        Vector3 beamVector = reciprocalXfrm.InverseTransformDirection(worldBeamVector);
        matReciprocal.SetVector("_MaxMinP", beamMaxMinMomentum);
        matReciprocal.SetVector("_BeamVector", transform.InverseTransformDirection(beamVector));
    }

    bool beamVecUpdated = true;
    public Vector3 WorldBeamVector
    {
        get => worldBeamVector;
        set
        {
            if (value == Vector3.zero) 
                value = Vector3.right;
            value = value.normalized;
            beamVecUpdated |= worldBeamVector != value;
            worldBeamVector = value;
            if (beamVecUpdated)
            {
                setCrystalBeam();
                beamVecUpdated = false;
            }
        }
    }

    public Vector2 BeamMaxMinMomentum
    {
        get => beamMaxMinMomentum;
        set
        {
            bool updated = (beamMaxMinMomentum != value);
            beamMaxMinMomentum = value;
            if (updated)
            {
                setCrystalBeam();
            }
        }
    }


    void Start()
    {
        ReviewOwnerShip();
        if (meshReciprocal != null)
        {
            reciprocalXfrm = meshReciprocal.transform;
            if (matReciprocal == null)
                matReciprocal = meshReciprocal.material;
        }
        if (meshCrystal != null)
        {
            crystalXfrm = meshCrystal.transform;
            if (matCrystal == null)
                matCrystal = meshCrystal.material;
        }
        //Transform crystalXfrm;
        iHaveRecipMaterial = matReciprocal != null;
        iHaveXtalMaterial = matCrystal != null;
        iHaveBeamAngle = beamAngleSlider != null;
        iHaveRotationControl = rotationControl != null;
        if (iHaveRotationControl)
            rotationControl.SetValue(crystalTheta);
        if (iHaveBeamAngle)
            beamAngleSlider.SetValue(beamAngle);
        BeamAngle = beamAngle;
        CrystalTheta = crystalTheta;
    }
}
