using System;
using TouchScript.Gestures;
using TouchScript.Hit;
using UnityEngine;

public class TUIOTap : MonoBehaviour
{
    private void OnEnable()
    {
        GetComponent<TapGesture>().Tapped += tappedHandler;
    }

    private void OnDisable()
    {
        GetComponent<TapGesture>().Tapped -= tappedHandler;
    }
    private void tappedHandler(object sender, EventArgs e)
    {
        var gesture = sender as TapGesture;
        HitData hit = gesture.GetScreenPositionHitData();
        Debug.Log("tapped once");
    }
}
