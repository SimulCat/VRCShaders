
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[RequireComponent(typeof(Slider))]
public class UdonSlider : UdonSharpBehaviour
{
    private Slider mySlider;
    [SerializeField]
    private TextMeshProUGUI sliderLabel;
    [SerializeField]
    private TextMeshProUGUI sliderTitle;
    [SerializeField,Tooltip("Apply damping by setting above zero")]
    private float dampingTime = 0f;
    [SerializeField]
    private bool hideLabel = false;
    [SerializeField]
    private bool unitsInteger = false;
    [SerializeField]
    private bool displayInteger = false;
    [SerializeField]
    private string sliderUnit;
    [SerializeField]
    UdonBehaviour SliderClient;
    [SerializeField]
    string clientVariableName = "SliderValueVar";
    [SerializeField]
    string clientPtrEvent = "slidePtr";
    private float currentValue;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SyncedValue))]
    private float syncedValue;
    [SerializeField]
    private float maxValue = 1;
    [SerializeField]
    private float minValue = 0;

    [SerializeField]
    private bool interactible = true;
    // UdonSync stuff
    private VRCPlayerApi player;
    private bool locallyOwned = false;
    private bool started = false;
    const float smoothThreshold = 0.001f;

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        locallyOwned = Networking.IsOwner(this.gameObject);
    }

    private bool pointerIsDown = false;
    public bool PointerIsDown
    {
        get => pointerIsDown;
    }
   
    public bool Interactible
    {
        get
        {
            return interactible;
        }
        set
        {
            interactible = value;
            if (mySlider != null)
                mySlider.interactable = value;
        }
    }

    public void SetValue(float value)
    {
        if (!started)
        {
            syncedValue = value;
            currentValue = value;
            return;
        }
        if (locallyOwned)
        {
            SyncedValue = value;
        }
    }

    public void SetLimits(float min, float max)
    {
        minValue = min;
        maxValue = max;
        if (!started)
            return;
        mySlider.minValue = minValue;
        mySlider.maxValue = maxValue;
    }
    public string TitleText
    {
        get
        {
            if (sliderTitle == null)
                return "";
            return sliderTitle.text;
        }
        set
        {
            if (sliderTitle == null)
                return;
            sliderTitle.text = value;
        }
    }
    public string SliderUnit
    {
        get => sliderUnit;
        set
        {
            sliderUnit = value;
            Updatelabel();
        }
    }
    private void Updatelabel()
    {
        if (sliderLabel == null || hideLabel)
            return;
        float displayValue = syncedValue;
        if (displayInteger)
            displayValue = Mathf.RoundToInt(displayValue);
        if (unitsInteger || displayInteger)
            sliderLabel.text = string.Format("{0}{1}", (int)displayValue, sliderUnit);
        else
            sliderLabel.text = string.Format("{0:0.0}{1}", displayValue, sliderUnit);
    }

    public float SyncedValue
    {
        get => syncedValue;
        set
        {
            syncedValue = value;
            if (!pointerIsDown)
                mySlider.SetValueWithoutNotify(syncedValue);
            if (dampingTime <= 0)
                CurrentValue = syncedValue;
            Updatelabel();
            if (started)
                RequestSerialization();
        }
    }

    public float CurrentValue
    {
        get => currentValue;
        private set
        {
            currentValue = value;
            if (iHaveClientVar)
            {
                if (unitsInteger)
                    SliderClient.SetProgramVariable<int>(clientVariableName, Mathf.RoundToInt(currentValue));
                else
                    SliderClient.SetProgramVariable<Single>(clientVariableName, currentValue);
            }
        }
    }
    public float MaxValue
    {
        get => maxValue;
        set
        {
            maxValue = value;
            if (mySlider != null)
                mySlider.maxValue = maxValue;
        }
    }

    public float MinValue
    {
        get => minValue;
        set
        {
            minValue = value;
            if (mySlider != null)
                mySlider.minValue = minValue;
        }
    }

    public void onValue()
    {
        if (pointerIsDown)
        {
            SyncedValue = mySlider.value;
        }
    }

    public void ptrDn()
    {
        if (!locallyOwned)
            Networking.SetOwner(player, gameObject);

        pointerIsDown = true;
        if (iHaveClientPtr)
            SliderClient.SendCustomEvent(clientPtrEvent);
    }
    public void ptrUp()
    {
        pointerIsDown = false;
    }

    private float smthVel = 0;

    public void Update()
    {
        if (dampingTime <= 0f  || currentValue == syncedValue)
            return;
        if (Mathf.Abs(1f - syncedValue / currentValue) > smoothThreshold)
            CurrentValue = Mathf.SmoothDamp(currentValue, syncedValue, ref smthVel, dampingTime);
        else
            CurrentValue = syncedValue;
    }

    private bool iHaveClientVar = false;
    private bool iHaveClientPtr = false;
    public void Start()
    {
        mySlider = GetComponent<Slider>();
        player = Networking.LocalPlayer;
        locallyOwned = Networking.IsOwner(gameObject);

        iHaveClientVar = (SliderClient != null) && (!string.IsNullOrEmpty(clientVariableName));
        iHaveClientPtr = (SliderClient != null) && (!string.IsNullOrEmpty(clientPtrEvent));

        if (sliderLabel != null && hideLabel)
            sliderLabel.text = "";

        mySlider.interactable = interactible;
        mySlider.minValue = minValue;
        mySlider.maxValue = maxValue;
        currentValue = syncedValue;
        SyncedValue = syncedValue;
        started = true;
    }
}
