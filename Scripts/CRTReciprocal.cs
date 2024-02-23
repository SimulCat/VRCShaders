
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
// A Udon model of the quantum reactions in a crystal based on William Duane's 1923 hypotheisis
// This version aims to use a custom render texture to speed up the calculation of the set of reaction
// Vectors and the beam angle of incidence.
public class CRTReciprocal : UdonSharpBehaviour
{
    [SerializeField]
    private CustomRenderTexture customRenderTexture;
    void Start()
    {
        
    }
}
