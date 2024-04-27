
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CrystalDemo : UdonSharpBehaviour
{
    [SerializeField]
    MeshRenderer meshCrystal;
    [SerializeField]
    MeshRenderer meshReciprocal;
    [SerializeField]
    MeshRenderer meshEwald;

    bool iHaveCrystal = false;
    bool iHaveReciprocal = false;
    bool iHaveEwald = false;

    [Header("UI Controls and Settings")]

    //UI Controls
    [SerializeField]
    UdonSlider rotationControl = null;
    bool iHaveRotationControl = false;
    
    // Beam Angle
    [SerializeField]
    UdonSlider beamAngleSlider = null;
    bool iHaveBeamAngle = false;
       
    [SerializeField, FieldChangeCallback(nameof(WorldBeamVector))]
    private Vector3 worldBeamVector = Vector3.right;
    [SerializeField, FieldChangeCallback(nameof(BeamMaxMinMomentum))]
    private Vector2 beamMaxMinMomentum = new Vector2(30, 5);


    [Header("State Variables")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(CrystalTheta))] private float crystalTheta = 0;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(BeamAngle))] private float beamAngle = 0;


    [Header("Serialized as needed for verification in editor")]
    [SerializeField]
    Material matReciprocal;
    [SerializeField]
    Material matCrystal;
    [SerializeField]
    Material matEwald;
    //[SerializeField]
    Transform reciprocalXfrm;
    //[SerializeField]
    Transform crystalXfrm;
   // [SerializeField]
    Transform ewaldXfrm;

    // GameObject Network Ownership State
    private VRCPlayerApi player;
    private bool iamOwner = false;

    // UI State properties
    /// <summary>
    /// Handle Beam Elevation event
    /// </summary>
    /// 

    public float BeamAngle
    {
        get => beamAngle;
        set
        {
            beamAngle = Mathf.Clamp(value, -90, 90);
            if (iHaveBeamAngle && !beamAngleSlider.PointerDown)
                beamAngleSlider.SetValue(beamAngle);
            float beamRadians = beamAngle * Mathf.Deg2Rad;
            WorldBeamVector = new Vector3(Mathf.Cos(beamRadians), -Mathf.Sin(beamRadians), 0);
            RequestSerialization();
        }
    }

    /// <summary>
    /// Handle Crystal angle slider event
    /// </summary>
    /// 
    public float CrystalTheta
    {
        get => crystalTheta;
        set
        {
            crystalTheta = Mathf.Clamp(value, -90, 90);
            if (iHaveRotationControl && !rotationControl.PointerDown)
                rotationControl.SetValue(crystalTheta);
            Quaternion newRotation = Quaternion.Euler(Vector3.up * crystalTheta);
            if (iHaveReciprocal)
                reciprocalXfrm.localRotation = newRotation;
            if (iHaveCrystal)
                crystalXfrm.localRotation = newRotation;
            if (iHaveEwald)
                ewaldXfrm.localRotation = newRotation;
            setCrystalBeam();
            RequestSerialization();
        }
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

    /// <summary>
    /// Sets the direction of the incident beam, if it has changed from the previous value then recalculate the set of possible reactions to match;
    /// </summary>
    /// 
    private void setCrystalBeam()
    {
        if (iHaveEwald)
        {
            Vector3 beamVector = ewaldXfrm.InverseTransformDirection(worldBeamVector);
            matEwald.SetVector("_MaxMinP", beamMaxMinMomentum);
            matEwald.SetVector("_BeamVector", transform.InverseTransformDirection(beamVector));
        }
    }

    /// <summary>
    ///  Event Handlers
    /// </summary>
    /// 
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

    void Start()
    {
        ReviewOwnerShip();
        iHaveEwald = meshEwald != null;
        if (iHaveEwald)
        {
            ewaldXfrm = meshEwald.transform;
            matEwald = meshEwald.material;
            iHaveEwald = matEwald != null;
        }
        iHaveCrystal = meshCrystal != null;
        if (iHaveCrystal)
        {
            crystalXfrm = meshCrystal.transform;
            matCrystal = meshCrystal.material;
            iHaveCrystal = matCrystal != null;
        }
        iHaveReciprocal = meshReciprocal != null;
        if (iHaveReciprocal)
        {
            reciprocalXfrm = meshReciprocal.transform;
            matReciprocal = meshReciprocal.material;
            iHaveReciprocal |= matReciprocal != null;
        }
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
