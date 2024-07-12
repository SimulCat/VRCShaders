
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SyncedTween : UdonSharpBehaviour
{
    [Header("Assignment during implementation")]
    [SerializeField] UdonBehaviour[] linkedBehaviors;
    [SerializeField] string linkedVariableName;

    [SerializeField, Tooltip("Animation Curve")]
    AnimationCurve tweenCurve = AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);
    [SerializeField]
    private Toggle stateToggle;
    [SerializeField]
    float easing = 0.75f;

    
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(SyncedState))]
    private bool syncedState = false;
    private bool isPlaying = false;
    bool locallyOwned = true;
    private VRCPlayerApi localPlayer;

    private bool SyncedState
    {
        get => syncedState;
        set
        {
            isPlaying |= value != syncedState;
            syncedState = value;
            if (stateToggle != null && stateToggle.isOn != syncedState)
                    stateToggle.isOn = syncedState;
            RequestSerialization();
        }
    }

    void SendOutValues(float value)
    {
       
        foreach (UdonBehaviour receiver in linkedBehaviors)
        {
            receiver.SetProgramVariable<float>(linkedVariableName, value);
        }
    }

    float animationTime = 0;

    public void onToggle()
    {
        if (stateToggle == null)
            return;
        bool togVal = stateToggle.isOn;
        if (togVal != syncedState)
        {
            if (!locallyOwned)
                Networking.SetOwner(localPlayer, gameObject);
            SyncedState = togVal;
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        base.OnOwnershipTransferred(player);
        locallyOwned = player.isLocal;
    }

    private void Update()
    {
        if (!isPlaying)
            return;
        animationTime = Mathf.Clamp01(animationTime + (Time.deltaTime * (syncedState ? easing : -easing)));
        isPlaying = animationTime > 0 && animationTime < 1;
        SendOutValues(tweenCurve.Evaluate(animationTime));
    }

    private void Start()
    {
        if (stateToggle == null)
            stateToggle = GetComponent<Toggle>();
        if (string.IsNullOrEmpty(linkedVariableName))
            linkedVariableName = gameObject.name;
        localPlayer = Networking.LocalPlayer;
        locallyOwned = Networking.IsOwner(gameObject);
        if (stateToggle != null)
            SyncedState = stateToggle.isOn;
        else
            SyncedState = syncedState;
        animationTime = syncedState ? 1f : 0f;
    }
}
