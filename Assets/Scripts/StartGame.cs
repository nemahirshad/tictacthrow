using TouchScript.Hit;
using TouchScript.Gestures;
using UnityEngine;

public class StartGame : MonoBehaviour
{
    private void OnDestroy()
    {
        GetComponent<TapGesture>().Tapped -= DetectStart;
    }

    private void Start()
    {
        GetComponent<TapGesture>().Tapped += DetectStart;
    }

    private void DetectStart(object sender, System.EventArgs e)
    {
        GameManager.instance.OnRestart();
    }
}
