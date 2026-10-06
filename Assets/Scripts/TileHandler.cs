using System;
using TouchScript.Gestures;
using UnityEngine;

public class TileHandler : MonoBehaviour
{
    [SerializeField] int tileNumber;

    [Tooltip("IMPORTANT: Assign the SpriteRenderer component from the CHILD GameObject that displays the X/O symbol here.")]
    [SerializeField] SpriteRenderer symbolSpriteRenderer;

    private TapGesture tapGestureComponent;

    private void Awake()
    {
        if (symbolSpriteRenderer == null)
        {
            Debug.LogError($"TileHandler on '{gameObject.name}': SymbolSpriteRenderer is not assigned in the Inspector. Please assign the child's SpriteRenderer.", this);
        }
    }

    private void OnEnable()
    {
        tapGestureComponent = GetComponent<TapGesture>();
        if (tapGestureComponent != null)
        {
            tapGestureComponent.Tapped += TappedHandler;
        }
        else
        {
            Debug.LogError($"TapGesture component not found on {gameObject.name}. Tap input will not work for this tile.", this);
        }
    }

    private void OnDisable()
    {
        if (tapGestureComponent != null)
        {
            tapGestureComponent.Tapped -= TappedHandler;
        }
    }

    public void CleanUp()
    {
        if (symbolSpriteRenderer != null)
        {
            symbolSpriteRenderer.sprite = null;
            symbolSpriteRenderer.transform.localScale = Vector3.one;
        }
    }

    public SpriteRenderer GetSymbolSpriteRenderer()
    {
        return symbolSpriteRenderer;
    }

    private void TappedHandler(object sender, EventArgs e)
    {
        Debug.Log($"TAP DETECTED on GameObject: '{gameObject.name}' with Tile Number: {tileNumber}", this.gameObject);

        if (GameManager.instance != null && GameManager.instance.canPlay)
        {
            if (symbolSpriteRenderer != null)
            {
                GameManager.tileHit?.Invoke(symbolSpriteRenderer, tileNumber);
            }
            else
            {
                Debug.LogError($"SymbolSpriteRenderer not assigned or found in TileHandler for {gameObject.name}, cannot process tap.", this);
            }
        }
    }

}
