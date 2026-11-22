using UnityEngine;
using System;

public class WebViewObject : MonoBehaviour
{
    public Action<string> onJS;
    public Action<string> onError;
    public Action<string> onLoaded;

    public void Init(Action<string> cb = null, Action<string> err = null, Action<string> ld = null)
    {
        onJS = cb;
        onError = err;
        onLoaded = ld;
    }

    public void SetMargins(int left, int top, int right, int bottom)
    {
        // Platform-specific margin support kan hier toegevoegd worden.
    }

    public void SetVisibility(bool v)
    {
        gameObject.SetActive(v);
    }

    public void LoadURL(string url)
    {
        // Deze placeholder opent de pagina in de systeembrowser.
        // Voor een echte native Unity WebView in Unity 6 kun je een dedicated package gebruiken.
        Application.OpenURL(url);
        onLoaded?.Invoke(url);
    }

    public void EvaluateJS(string js)
    {
        Debug.Log("[WebViewObject] EvaluateJS: " + js);
    }

    public void CallFromJS(string message)
    {
        onJS?.Invoke(message);
    }
}
