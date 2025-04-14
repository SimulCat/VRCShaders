
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.UI;
using VRC.Udon;

//public enum CrystalTypes { Simple, Ionic, FaceCentred, BodyCentred };

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CrystalDemo : UdonSharpBehaviour
{
    [Header("Model Components")]
    [SerializeField]
    MeshRenderer meshCrystal;
    [SerializeField]
    MeshRenderer meshReciprocal;
    [SerializeField]
    MeshRenderer meshEwald;
    [SerializeField]
    UdonBehaviour beamPointer;
    //[SerializeField]
    bool iHaveCrystal = false;
    //[SerializeField]
    bool iHaveReciprocal = false;
    //[SerializeField]
    bool iHaveEwald = false;

    [Header("UI Controls and Settings")]

    //UI Controls
    [SerializeField,Tooltip("Slider rotates crystal body")]
    UdonSlider rotationControl = null;
    bool iHaveRotationControl = false;
    
    // Beam Angle
    [SerializeField]
    UdonSlider beamAngleSlider = null;
    bool iHaveBeamAngle = false;

    public Toggle selectRect;
    public Toggle selectIonic;
    public Toggle selectFace;
    public Toggle selectBody;
    public Toggle selectCubic;

    [SerializeField]
    UdonBehaviour angstromSliderX;
    bool iHaveControlX;
    [SerializeField]
    UdonBehaviour angstromSliderY;
    bool iHaveControlY;
    [SerializeField]
    UdonBehaviour angstromSliderZ;
    bool iHaveControlZ;

    [Header("Settings")]
    [SerializeField, FieldChangeCallback(nameof(WorldBeamVector))]
    private Vector3 worldBeamVector = Vector3.right;
    [SerializeField, FieldChangeCallback(nameof(BeamMaxMinMomentum))]
    private Vector2 beamMaxMinMomentum = new Vector2(30, 5);
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(UnitCellCubic))] bool unitCellCubic = false;
    [SerializeField, FieldChangeCallback(nameof(CellX))] float cellX = 3.2f;
    [SerializeField, FieldChangeCallback(nameof(CellY))] float cellY = 3.2f;
    [SerializeField, FieldChangeCallback(nameof(CellZ))] float cellZ = 3.2f;

    [Header("State Variables")]
    [SerializeField, FieldChangeCallback(nameof(CrystalTheta))] private float crystalTheta = 0;
    [SerializeField, FieldChangeCallback(nameof(BeamAngle))] private float beamAngle = 0;

    [Header("Unit Cell Parameters")]
    [SerializeField]
    public Vector3 latticePitchAngstroms = new Vector3(3, 3, 3);

    [UdonSynced, FieldChangeCallback(nameof(CrystalTypes))]
    public CrystalTypes crystalType = CrystalTypes.Simple;

    private Vector3 primitiveA1;
    private Vector3 primitiveA2;
    private Vector3 primitiveA3;

    [Header("Reciprocal Parameters")]
    private float AngstromToP24 = 6.62607015f;  // 1/d Angstrom as KeV

    public Vector3 reciprocalDims = new Vector3(24, 24, 24);

    public CrystalTypes reciprocalType = CrystalTypes.Simple;

    private Vector3 reciprocalA1;
    private Vector3 reciprocalA2;
    private Vector3 reciprocalA3;


    //[Header("Serialized as needed for verification in editor")]
    //[SerializeField]
    Material matReciprocal;
   // [SerializeField]
    Material matCrystal;
    //[SerializeField]
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
    private bool started = false;

    // UI State properties

    private void UpdatePitch()
    {
        if ( !started)
            return;
        Vector3 newDims = unitCellCubic ? new Vector3(CellX, CellX, CellX) : new Vector3(CellX, CellY, CellZ);
        if (latticePitchAngstroms != newDims)
        {
            latticePitchAngstroms = newDims;
            updateLattice(crystalType);
        }
        Vector4 ModelPitch = new Vector4(latticePitchAngstroms.x, latticePitchAngstroms.y, latticePitchAngstroms.z, 0);
        if (crystalType == CrystalTypes.Simple)
            ModelPitch *= 0.5f;
        if (iHaveCrystal)
        {
            matCrystal.SetVector("_LatticePitch", ModelPitch);
            matCrystal.SetInteger("_LatticeType", (int)crystalType);
            matCrystal.SetInteger("_OriginAtCorner", 0);
        }
        Vector4 RecipPitch = new Vector4(reciprocalDims.x, reciprocalDims.y, reciprocalDims.z, 0);
        if (iHaveReciprocal)
        {
            matReciprocal.SetVector("_LatticePitch", RecipPitch);
            matReciprocal.SetInteger("_LatticeType", (int)reciprocalType);
            matReciprocal.SetInteger("_OriginAtCorner", 1);
        }
        if (iHaveEwald)
        {
            matEwald.SetVector("_LatticePitch", RecipPitch);
            matEwald.SetInteger("_LatticeType", (int)reciprocalType);
            matReciprocal.SetInteger("_OriginAtCorner", 1);
        }
    }

    public void updateLattice(CrystalTypes type)
    {
        if (!started)
            return;
        //Debug.Log("Setlattice:C");
        switch (type)
        {
            case CrystalTypes.Ionic:
                primitiveA1 = 0.5f * latticePitchAngstroms.x * Vector3.right;
                primitiveA2 = 0.5f * latticePitchAngstroms.y * Vector3.up;
                primitiveA3 = 0.5f * latticePitchAngstroms.z * Vector3.forward;
                break;
            case CrystalTypes.Simple:
                primitiveA1 = latticePitchAngstroms.x * Vector3.right;
                primitiveA2 = latticePitchAngstroms.y * Vector3.up;
                primitiveA3 = latticePitchAngstroms.z * Vector3.forward;
                break;
            case CrystalTypes.BodyCentred:
                /*	A1 = - brA/2 * x + latticePitchAngstroms.y/2 * y + latticePitchAngstroms.z/2 * z
                    A2 =   brA/2 * x - latticePitchAngstroms.y/2 * y + latticePitchAngstroms.z/2 * z
                    A3 =   brA/2 * x + latticePitchAngstroms.y/2 * y - latticePitchAngstroms.z/2 * z */
                primitiveA1 = (-latticePitchAngstroms.x / 2 * Vector3.right) + latticePitchAngstroms.y / 2 * Vector3.up + latticePitchAngstroms.z / 2 * Vector3.forward;
                primitiveA2 = latticePitchAngstroms.x / 2 * Vector3.right + (-latticePitchAngstroms.y / 2 * Vector3.up) + latticePitchAngstroms.z / 2 * Vector3.forward;
                primitiveA3 = latticePitchAngstroms.x / 2 * Vector3.right + latticePitchAngstroms.y / 2 * Vector3.up + (-latticePitchAngstroms.z / 2 * Vector3.forward);
                break;
            case CrystalTypes.FaceCentred:
                primitiveA1 = (latticePitchAngstroms.y / 2 * Vector3.up) + (latticePitchAngstroms.z / 2 * Vector3.forward);
                primitiveA2 = (latticePitchAngstroms.x / 2 * Vector3.right) + (latticePitchAngstroms.z / 2 * Vector3.forward);
                primitiveA3 = (latticePitchAngstroms.x / 2 * Vector3.right) + (latticePitchAngstroms.y / 2 * Vector3.up);
                break;
        }
        float invVolume = 1 / Vector3.Dot(primitiveA1, Vector3.Cross(primitiveA2, primitiveA3));
        float cellScale = invVolume * AngstromToP24;
        //Debug.Log("Setlattice:R");

        reciprocalA1 = cellScale * Vector3.Cross(primitiveA2, primitiveA3);
        reciprocalA2 = cellScale * Vector3.Cross(primitiveA3, primitiveA1);
        reciprocalA3 = cellScale * Vector3.Cross(primitiveA1, primitiveA2);
        switch (type)
        {
            case CrystalTypes.FaceCentred:
                reciprocalType = CrystalTypes.BodyCentred;
                reciprocalDims.x = (reciprocalA2 + reciprocalA3).magnitude;
                reciprocalDims.y = (reciprocalA1 + reciprocalA3).magnitude;
                reciprocalDims.z = (reciprocalA2 + reciprocalA1).magnitude;
                break;
            case CrystalTypes.BodyCentred:
                reciprocalType = CrystalTypes.FaceCentred;
                reciprocalDims.x = (reciprocalA2 - reciprocalA1 + reciprocalA3).magnitude;
                reciprocalDims.y = (reciprocalA3 - reciprocalA2 + reciprocalA1).magnitude;
                reciprocalDims.z = (reciprocalA1 - reciprocalA3 + reciprocalA2).magnitude;
                break;
            case CrystalTypes.Ionic:
                reciprocalType = CrystalTypes.Ionic;
                reciprocalDims.x = reciprocalA1.magnitude;
                reciprocalDims.y = reciprocalA2.magnitude;
                reciprocalDims.z = reciprocalA3.magnitude;
                break;
            default:
                reciprocalDims.x = reciprocalA1.magnitude;
                reciprocalDims.y = reciprocalA2.magnitude;
                reciprocalDims.z = reciprocalA3.magnitude;
                reciprocalType = CrystalTypes.Simple;
                break;
        }
        UpdatePitch();
    }


    /// <summary>
    /// Handle Crystal Type
    /// </summary>
    private void showCrystalType()
    {
        switch (crystalType)
        {
            case CrystalTypes.Simple:
                if (selectRect != null && !selectRect.isOn)
                    selectRect.SetIsOnWithoutNotify(true);
                break;
            case CrystalTypes.Ionic:
                if (selectIonic != null && !selectIonic.isOn)
                    selectIonic.SetIsOnWithoutNotify(true);
                break;
            case CrystalTypes.FaceCentred:
                if (selectFace != null && !selectFace.isOn)
                    selectFace.SetIsOnWithoutNotify(true);
                break;
            case CrystalTypes.BodyCentred:
                if (selectBody != null && !selectBody.isOn)
                    selectBody.SetIsOnWithoutNotify(true);
                break;
        }
    }
    private CrystalTypes CrystalTypes
    {
        get => crystalType;
        set
        {
            bool isNew = crystalType != value;
            crystalType = value;
            showCrystalType();
            if (isNew)
                updateLattice(crystalType);
            RequestSerialization();
        }
    }

    /// <summary>
    /// Handle Cystal Cell Dimensions
    /// </summary>
    /// 
    private bool UnitCellCubic
    {
        get => unitCellCubic;
        set
        {
            unitCellCubic = value;
            if (iHaveControlY)
                angstromSliderY.gameObject.SetActive(!unitCellCubic);
            if (iHaveControlZ)
                angstromSliderZ.gameObject.SetActive(!unitCellCubic);
            if (selectCubic != null && selectCubic.isOn != value)
                selectCubic.SetIsOnWithoutNotify(value);
            UpdatePitch();
            RequestSerialization();
        }
    }
    private float CellX
    {
        get => cellX;
        set
        {
            cellX = value;
            UpdatePitch();
        }
    }
    private float CellY
    {
        get => cellY;
        set
        {
            cellY = value;
            UpdatePitch();
        }
    }
    private float CellZ
    {
        get => cellZ;
        set
        {
            cellZ = value;
            UpdatePitch();
        }
    }

    /// <summary>
    /// Handlers for toggle Events
    /// </summary>
    public void dimPtr() 
    {
        if (!iamOwner)
            Networking.SetOwner(player,gameObject);
    }

    public void togCubic()
    {
        if (selectCubic != null)
        {
            if (unitCellCubic != selectCubic.isOn)
            {
                UnitCellCubic = !unitCellCubic;
            }
        }
    }

    public void togSimple()
    {
        if (selectRect != null && selectRect.isOn)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            CrystalTypes = CrystalTypes.Simple;
        }
    }

    public void togIonic()
    {
        if (selectIonic != null && selectIonic.isOn)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            CrystalTypes = CrystalTypes.Ionic;
        }
    }
    public void togFace()
    {
        if (selectFace != null && selectFace.isOn)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            CrystalTypes = CrystalTypes.FaceCentred;
        }
    }
    public void togBody()
    {
        if (selectBody != null && selectBody.isOn)
        {
            if (!iamOwner)
                Networking.SetOwner(player, gameObject);
            CrystalTypes = CrystalTypes.BodyCentred;
        }
    }


    /// <summary>
    /// Handle Beam Elevation event
    /// </summary>
    /// 
    private float BeamAngle
    {
        get => beamAngle;
        set
        {
            beamAngle = Mathf.Clamp(value, -90, 90);
            float beamRadians = beamAngle * Mathf.Deg2Rad;
            WorldBeamVector = new Vector3(Mathf.Cos(beamRadians), -Mathf.Sin(beamRadians), 0);
            if (beamPointer != null)
                beamPointer.SetProgramVariable("thetaDegrees",-beamAngle);
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
            Quaternion newRotation = Quaternion.Euler(Vector3.up * crystalTheta);
            if (iHaveReciprocal)
                reciprocalXfrm.localRotation = newRotation;
            if (iHaveCrystal)
                crystalXfrm.localRotation = newRotation;
            if (iHaveEwald)
                ewaldXfrm.localRotation = newRotation;
            setCrystalBeam();
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
        iHaveControlX = angstromSliderX != null;
        iHaveControlY = angstromSliderY != null;
        iHaveControlZ = angstromSliderZ != null;
        iHaveBeamAngle = beamAngleSlider != null;
        iHaveRotationControl = rotationControl != null;

        CellX = cellX;
        if (iHaveControlX)
            angstromSliderX.SetProgramVariable<float>("syncedValue",cellX);
        CellY = cellY;
        if (iHaveControlY)
            angstromSliderY.SetProgramVariable<float>("syncedValue", cellY);
        CellZ = cellZ;
        if (iHaveControlZ)
            angstromSliderX.SetProgramVariable<float>("syncedValue", cellZ);

        CrystalTypes = crystalType;
        BeamAngle = beamAngle;
        if (iHaveBeamAngle)
            beamAngleSlider.SetValue(beamAngle);
        CrystalTheta = crystalTheta;
        if (iHaveRotationControl)
            rotationControl.SetValue(crystalTheta);
        UnitCellCubic = unitCellCubic;
        started = true;
        updateLattice(crystalType);
    }
}
