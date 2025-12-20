using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ReciprocalView : UdonSharpBehaviour
{
    [SerializeField]
    MeshRenderer meshEwald;
    [SerializeField]
    MeshRenderer meshCrystal;
    [SerializeField]
    Material matEwald;
    [SerializeField]
    Material matCrystal;

    [Header("UI Controls and Settings")]

    [SerializeField]
    UdonSlider beamAngleSlider = null;
    bool iHaveBeamAngle = false;

    [SerializeField]
    UdonSlider rotationControl = null;
    bool iHaveRotationControl = false;

    [Header("State Variables")]
    [SerializeField, FieldChangeCallback(nameof(CrystalTheta))] private float crystalTheta = 0;
    [SerializeField, FieldChangeCallback(nameof(BeamAngle))] private float beamAngle = 0;

    [SerializeField, FieldChangeCallback(nameof(WorldBeamVector))]
    private Vector3 worldBeamVector = Vector3.right;
    [SerializeField, FieldChangeCallback(nameof(BeamMaxMinMomentum))]
    private Vector2 beamMaxMinMomentum = new Vector2(30, 5);

    [Header("Just for feedback in editor")]
    [SerializeField]
    Transform ewaldXfrm;
    [SerializeField]
    Transform crystalXfrm;
    private VRCPlayerApi player;
    private bool iamOwner = false;



    bool iHaveEwald;
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
            Quaternion newRotation = Quaternion.Euler(Vector3.up * crystalTheta);
            if (ewaldXfrm != null)
                ewaldXfrm.localRotation = newRotation;
            if (crystalXfrm != null)
                crystalXfrm.localRotation = newRotation;
            setCrystalBeam();
        } 
    }

    public float BeamAngle
    {
        get => beamAngle;
        set
        {
            beamAngle = Mathf.Clamp(value, -90, 90);
            float beamRadians = beamAngle*Mathf.Deg2Rad;
            WorldBeamVector = new Vector3(Mathf.Cos(beamRadians),-Mathf.Sin(beamRadians),0); 
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
        if (!iHaveEwald) return;
        Vector3 beamVector = ewaldXfrm.InverseTransformDirection(worldBeamVector);
        matEwald.SetVector("_MaxMinP", beamMaxMinMomentum);
        matEwald.SetVector("_BeamVector", transform.InverseTransformDirection(beamVector));
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
        if (meshEwald != null)
        {
            ewaldXfrm = meshEwald.transform;
            if (matEwald == null)
                matEwald = meshEwald.material;
        }
        if (meshCrystal != null)
        {
            crystalXfrm = meshCrystal.transform;
            if (matCrystal == null)
                matCrystal = meshCrystal.material;
        }
        //Transform crystalXfrm;
        iHaveEwald = matEwald != null;
        iHaveXtalMaterial = matCrystal != null;
        iHaveBeamAngle = beamAngleSlider != null;
        iHaveRotationControl = rotationControl != null;
        CrystalTheta = crystalTheta;
        if (iHaveRotationControl)
            rotationControl.SetValue(crystalTheta);
        BeamAngle = beamAngle;
        if (iHaveBeamAngle)
            beamAngleSlider.SetValue(beamAngle);
    }
}
