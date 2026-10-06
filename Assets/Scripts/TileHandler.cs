using System;
using TouchScript.Gestures;
using UnityEngine;

[RequireComponent(typeof(TapGesture))]
public class TileHandler : MonoBehaviour
{
    [SerializeField] int tileNumber;
    [SerializeField] SpriteRenderer symbolSpriteRenderer;
    public int TileNumber { get { return tileNumber; } }
    TapGesture tap;
    Color originalColor = Color.white;
    void Awake()
    {
        tap = GetComponent<TapGesture>();
        if (symbolSpriteRenderer != null) originalColor = symbolSpriteRenderer.color;
        else Debug.LogError("Missing symbol renderer on " + name,this);
    }
    void OnEnable()
    {
        if (tap == null) tap = GetComponent<TapGesture>();
        if (tap != null) tap.Tapped += Tapped;
    }
    void OnDisable() { if (tap != null) tap.Tapped -= Tapped; }
    public void ConfigureInput(float contactTime,float travelCentimeters)
    {
        if (tap == null) tap = GetComponent<TapGesture>();
        if (tap == null) return;
        tap.TimeLimit = contactTime;
        tap.DistanceLimit = travelCentimeters;
    }
    public void CleanUp()
    {
        if (symbolSpriteRenderer == null) return;
        LeanTween.cancel(symbolSpriteRenderer.gameObject);
        symbolSpriteRenderer.sprite = null;
        symbolSpriteRenderer.color = originalColor;
        symbolSpriteRenderer.transform.localScale = Vector3.one;
    }
    public SpriteRenderer GetSymbolSpriteRenderer() { return symbolSpriteRenderer; }
    void Tapped(object sender,EventArgs args)
    {
        if (GameManager.instance != null) GameManager.instance.ReceiveHit(this,tap.ScreenPosition);
    }
}
