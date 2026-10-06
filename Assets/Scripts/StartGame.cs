using System;
using TouchScript.Gestures;
using UnityEngine;

[RequireComponent(typeof(TapGesture))]
public class StartGame : MonoBehaviour
{
    TapGesture tap;
    void OnEnable()
    {
        tap = GetComponent<TapGesture>();
        if (tap != null) tap.Tapped += DetectStart;
    }
    void OnDisable() { if (tap != null) tap.Tapped -= DetectStart; }
    void DetectStart(object sender,EventArgs args)
    {
        if (GameManager.instance != null) GameManager.instance.OnRestart();
    }
}
