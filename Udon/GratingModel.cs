
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GratingModel : UdonSharpBehaviour
{
    [SerializeField] private int slitCount = 2;
    [SerializeField] private int rowCount = 1;
    [SerializeField] private float gratingThickness = 0.01f;
    [SerializeField] private float slitWidth = 0.01f;
    [SerializeField] private float slitHeight = 0.01f;
    [SerializeField] private float slitPitch = 0.05f;
    [SerializeField] private float rowPitch = 0.05f;
    [SerializeField] private Vector2 minDimensions = new Vector2(0.5f, 0.5f);
    private bool _gratingChanged = false;
    [SerializeField]
    private Vector2 _gratingDims = Vector2.one;
    private float sidePanelWidth;
    private float basePanelHeight;

    [SerializeField]
    private GameObject barPrefab;
    private const int MAX_SLITS = 17;
    // Locak instances of the grating elements
    private Transform[] _vertBars;
    private Transform[] _horizBars;
    [SerializeField]
    private Transform panelLeft;
    [SerializeField]
    private Transform panelRight;
    [SerializeField]
    private Transform panelUpper;
    [SerializeField]
    private Transform panelLower;

    public int RowCount
    {
        set
        {
            _gratingChanged |= rowCount != value;
            rowCount = value;
        }
        get { return rowCount; }
    }

    public int SlitCount
    {
        set
        {
            _gratingChanged |= value != slitCount;
            slitCount = value;
        }
        get { return slitCount; }
    }


    public float SlitWidth
    {
        set
        {
            _gratingChanged |= slitWidth != value;
            slitWidth = value;
        }
        get { return slitWidth; }
    }
    public float SlitPitch
    {
        get => slitPitch;
        set
        {
            _gratingChanged |= slitPitch != value;
            slitPitch = value;
        }
    }
    public float SlitHeight
    {
        get => slitHeight;
        set
        {
            _gratingChanged |= slitHeight != value;
            slitHeight = value;
        }
    }

    public float RowPitch
    {
        get => rowPitch;
        set
        {
            _gratingChanged |= rowPitch != value;
            rowPitch = value;
        }
    }



    Transform localFromPrefab(GameObject prototype, string name)
    {
        GameObject go = Instantiate(prototype);
        if (go != null)
        {
            go.name = name;
            go.transform.SetParent(this.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
        return go.transform;
    }
    
    void setupLattice()
    {
        // Destroy previous bar objects
        if (_vertBars != null)
        {
            for (int i = 0; i < _vertBars.Length; i++)
            {
                if (_vertBars[i] != null)
                    Destroy(_vertBars[i].gameObject);
            }
        }
        if (_vertBars == null || _vertBars.Length < (MAX_SLITS - 1))
            _vertBars = new Transform[MAX_SLITS - 1];
        if (_horizBars != null)
        {
            for (int i = 0; i < _horizBars.Length; i++)
            {
                if (_horizBars[i] != null)
                    Destroy(_horizBars[i].gameObject);
            }
        }
        if (_horizBars == null || _horizBars.Length < (MAX_SLITS - 1))
            _horizBars = new Transform[MAX_SLITS - 1];
        int nR = (rowCount <= 0 ? 1 : rowCount);

        // Set dimensons for the construction of the lattice;
        float studWidth = slitPitch - slitWidth;
        float nogWidth = rowPitch - slitHeight;

        _gratingDims.x = (slitPitch * (slitCount - 1)) + slitWidth;

        if (rowCount <= 0)
        {
            _gratingDims.y = Mathf.Max(minDimensions.y, rowPitch);
            basePanelHeight = 0.0f;
        }
        else
        {
            _gratingDims.y = (rowPitch * (rowCount - 1)) + slitHeight;
            if ((_gratingDims.y + rowPitch) > minDimensions.y)
                basePanelHeight = rowPitch * 0.5f;
            else
                basePanelHeight = (minDimensions.y - _gratingDims.y) * 0.5f;

        }

        if ((_gratingDims.x + slitPitch) > minDimensions.x)
            sidePanelWidth = slitPitch * 0.5f;
        else
            sidePanelWidth = (minDimensions.x - _gratingDims.x) * 0.5f;

        float overallWidth = sidePanelWidth * 2.0f + _gratingDims.x;

        // Set up lattice parameters
        Vector3 latticePos = (gameObject.transform.position);
        Vector3 latticeRight = transform.TransformDirection(Vector3.forward);

        float gratingHalfWidth = (_gratingDims.x / 2.0f);
        float gratingHalfHeight = (_gratingDims.y / 2.0f);

        float slitDelta = slitWidth + studWidth;
        float slotDelta = slitHeight + nogWidth;

        // Calculate positions of side and top panels
        Vector3 LeftPanelPos = Vector3.right * (gratingHalfWidth + (sidePanelWidth / 2.0f));
        Vector3 BottomPanelPos = Vector3.down * ((basePanelHeight / 2.0f) + gratingHalfHeight);
        Vector3 RightPanelPos = Vector3.left * (gratingHalfWidth + (sidePanelWidth / 2.0f));
        Vector3 TopPanelPos = Vector3.up * ((basePanelHeight / 2.0f) + gratingHalfHeight);

        // Log Parameters
        if (barPrefab != null)
        {
            // Set Scales for the Slit and Block Prefabs
            if (panelLeft == null)
                panelLeft = localFromPrefab(barPrefab, "Left Panel");
            panelLeft.localPosition = LeftPanelPos;
            panelLeft.transform.localScale = new Vector3(sidePanelWidth, _gratingDims.y, gratingThickness);

            if (panelLower == null)
                panelLower = localFromPrefab(barPrefab, "Bottom Panel");
            panelLower.localPosition = BottomPanelPos;
            panelLower.transform.localScale = new Vector3(overallWidth, basePanelHeight, gratingThickness);

            // Set scale and then position of the first spacer
            float StudOffset = gratingHalfWidth - (slitWidth + (studWidth / 2.0F));

            for (int nSlit = 0; nSlit < slitCount; nSlit++)
            {
                if ((nSlit + 1) < slitCount)
                {
                    _vertBars[nSlit] = localFromPrefab(barPrefab, string.Format("Stud_{0}", nSlit + 1));
                    _vertBars[nSlit].localPosition = (Vector3.right * StudOffset);
                    _vertBars[nSlit].transform.localScale = new Vector3(studWidth, _gratingDims.y, gratingThickness);
                    StudOffset -= slitDelta;
                }
            }
            float NogOffset = gratingHalfHeight - (slitHeight + (nogWidth / 2.0F));
            for (int nSlot = 0; nSlot < rowCount; nSlot++)
            {
                if ((nSlot + 1) < rowCount)
                {
                    _horizBars[nSlot] = localFromPrefab(barPrefab, string.Format("Nog_{0}", nSlot + 1));
                    _horizBars[nSlot].transform.localScale = new Vector3(_gratingDims.x, nogWidth, gratingThickness);
                    _horizBars[nSlot].transform.localPosition = (Vector3.up * NogOffset);
                    NogOffset -= slotDelta;
                }
            }
            if (panelRight == null)
            {
                panelRight = localFromPrefab(barPrefab, "Right Panel");
            }
            panelRight.localPosition = RightPanelPos;
            panelRight.localScale = new Vector3(sidePanelWidth, _gratingDims.y, gratingThickness);
            if (panelUpper == null)
                panelUpper = localFromPrefab(barPrefab, "Top Panel");
            panelUpper.localScale = new Vector3(overallWidth, basePanelHeight, gratingThickness);
            panelUpper.localPosition = TopPanelPos;
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
        _gratingDims.x = (slitPitch * (slitCount - 1)) + slitWidth;

        if (rowCount <= 0)
            _gratingDims.y = slitPitch;
        else
            _gratingDims.y = (rowPitch * (rowCount - 1)) + slitHeight;

    }

    void Update()
    {
        if (_gratingChanged)
        {
            _gratingChanged = false;
            setupLattice();
        }
    }

    void StartUp()
    {
        _gratingChanged = true;
    }
    // Use this for pre-startup initialization

    public void Start()
    {
        StartUp();
    }
}
