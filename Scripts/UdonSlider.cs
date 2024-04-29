
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[RequireComponent(typeof(Slider))]
public class UdonSlider : UdonSharpBehaviour
{
    private Slider mySlider;
    [SerializeField]
    private TextMeshProUGUI sliderLabel;
    [SerializeField]
    private TextMeshProUGUI sliderTitle;
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
    [SerializeField]
    private float currentValue;
    [SerializeField]
    private float maxValue = 1;
    [SerializeField]
    private float minValue = 0;

    private float reportedValue = 0.0f;
    [SerializeField]
    private bool interactible = true;
    
    private bool pointerDown = false;
    public bool PointerDown
    {
        get => pointerDown;
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
        if (mySlider != null)
            mySlider.SetValueWithoutNotify(value);
        reportedValue = value;
        CurrentValue = value;
    }

    public void SetLimits(float min, float max)
    {
        minValue = min;
        maxValue = max;
        if (mySlider == null)
            return;
        mySlider.minValue = minValue;
        mySlider.maxValue = maxValue;
    }
    public string TitleText
    {
        get
        {
            if (!iHaveTitle)
                return "";
            return sliderTitle.text;
        }
        set
        {
            if (!iHaveTitle)
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
        if (sliderLabel == null)
            return;
        if (hideLabel)
        {
            sliderLabel.text = "";
            return;
        }
        float displayValue = currentValue;
        if (displayInteger)
            displayValue = Mathf.RoundToInt(displayValue);
        if (unitsInteger || displayInteger)
            sliderLabel.text = string.Format("{0}{1}", (int)displayValue, sliderUnit);
        else
            sliderLabel.text = string.Format("{0:0.0}{1}", displayValue, sliderUnit);
    }
    public float CurrentValue
    {
        get => currentValue;
        private set
        {
            currentValue = value;
            if (reportedValue != currentValue)
            {
                reportedValue = currentValue;
                if (iHaveClientVar)
                {
                    if (unitsInteger)
                        SliderClient.SetProgramVariable<int>(clientVariableName, Mathf.RoundToInt(currentValue));
                    else
                        SliderClient.SetProgramVariable<Single>(clientVariableName, currentValue);
                }
            }
            Updatelabel();
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
        //Debug.Log(gameObject.name + ":UdonSlider"+mySlider.value);
        CurrentValue = mySlider.value;
    }

    public void ptrDn()
    {
        pointerDown = true;
        if (iHaveClientPtr)
            SliderClient.SendCustomEvent(clientPtrEvent);
    }
    public void ptrUp()
    {
        pointerDown = false;
    }

    private bool iHaveTitle = false;
    private bool iHaveClientVar = false;
    private bool iHaveClientPtr = false;
    public void Start()
    {
        mySlider = GetComponent<Slider>();
        iHaveTitle = sliderTitle != null;
        iHaveClientVar = (SliderClient != null) && (!string.IsNullOrEmpty(clientVariableName));
        iHaveClientPtr = (SliderClient != null) && (!string.IsNullOrEmpty(clientPtrEvent));
        if (sliderLabel == null)
            hideLabel = true;
        mySlider.interactable = interactible;
        mySlider.minValue = minValue;
        mySlider.maxValue = maxValue;
        mySlider.SetValueWithoutNotify(currentValue);
    }
}
