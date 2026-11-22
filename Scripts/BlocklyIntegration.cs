using System;
using System.IO;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
#endif

[Serializable]
public class BlocklySaveRequest
{
    public bool saveCSharp;
    public string fileName;
    public string code;
}

public class BlocklyIntegration : MonoBehaviour
{
    private static BlocklyIntegration activeIntegration;
#if UNITY_EDITOR
    private const int BridgePort = 49381;
#endif
    public GameObject targetObject;
    [Tooltip("Sleep hier het GameObject in waar de bever naartoe moet lopen.")]
    public GameObject movementTarget;
    [Tooltip("Sleep hier de TextMeshPro-tekst in waar je bericht moet verschijnen.")]
    public TMP_Text tpmText;
    public RectTransform webViewRect;
    private WebViewObject webView;
    private BlocklyInterpreter interpreter;
#if UNITY_EDITOR
    private readonly ConcurrentQueue<string> pendingMessages = new ConcurrentQueue<string>();
    private HttpListener bridgeListener;
    private Thread bridgeThread;
    private string blocklyPagePath;
#endif

    void Awake()
    {
        if (activeIntegration != null && activeIntegration != this)
        {
            Debug.LogWarning("[Blockly] Extra BlocklyIntegration overgeslagen; gebruik één BlocklyIntegration in de scene.");
            enabled = false;
            return;
        }

        activeIntegration = this;

        if (targetObject == null)
        {
            Debug.LogWarning("[Blockly] Stel targetObject in op de bever; anders worden Blockly-acties uitgevoerd op BlocklyIntegration.");
            targetObject = gameObject;
        }

        interpreter = targetObject.GetComponent<BlocklyInterpreter>();
        if (interpreter == null)
            interpreter = targetObject.AddComponent<BlocklyInterpreter>();
        interpreter.SetMovementTarget(movementTarget);

        webView = (new GameObject("WebViewObject")).AddComponent<WebViewObject>();
        webView.Init(
            cb: (msg) =>
            {
                if (string.IsNullOrEmpty(msg))
                    return;
                Debug.Log("[Blockly] message: " + msg);
                interpreter.HandleMessage(msg);
            },
            err: (msg) =>
            {
                Debug.LogError("[Blockly] WebView Error: " + msg);
            },
            ld: (msg) =>
            {
                Debug.Log("[Blockly] page loaded: " + msg);
            });

        if (webViewRect != null)
            ApplyWebViewMargins(webViewRect);
        else
            webView.SetMargins(0, 0, 0, 0);

        webView.SetVisibility(true);

#if UNITY_EDITOR
        if (StartLocalBridge(out var bridgeUrl))
            webView.LoadURL(bridgeUrl);
#else
        webView.LoadURL(GetStreamingAssetUrl("Blockly/index.html"));
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        while (pendingMessages.TryDequeue(out var message))
        {
            Debug.Log("[Blockly] message: " + message);
            OnBlocklyMessage(message);
        }
    }

    private bool StartLocalBridge(out string bridgeUrl)
    {
        blocklyPagePath = Path.Combine(Application.streamingAssetsPath, "Blockly/index.html");
        if (!File.Exists(blocklyPagePath))
        {
            Debug.LogError("[Blockly] Blockly-pagina niet gevonden: " + blocklyPagePath);
            bridgeUrl = null;
            return false;
        }

        bridgeListener = new HttpListener();
        bridgeListener.Prefixes.Add("http://127.0.0.1:" + BridgePort + "/");
        try
        {
            bridgeListener.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError("[Blockly] Lokale verbinding starten mislukt: " + ex.Message);
            bridgeListener = null;
            bridgeUrl = null;
            return false;
        }

        bridgeThread = new Thread(ServeLocalRequests) { IsBackground = true };
        bridgeThread.Start();
        bridgeUrl = "http://127.0.0.1:" + BridgePort + "/";
        return true;
    }

    private void ServeLocalRequests()
    {
        var listener = bridgeListener;
        while (listener != null && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            try
            {
                if (context.Request.HttpMethod == "GET" &&
                    (context.Request.Url.AbsolutePath == "/" || context.Request.Url.AbsolutePath == "/index.html"))
                {
                    byte[] page = File.ReadAllBytes(blocklyPagePath);
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.StatusCode = 200;
                    context.Response.OutputStream.Write(page, 0, page.Length);
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/command")
                {
                    string message;
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        message = reader.ReadToEnd();
                    pendingMessages.Enqueue(message);
                    byte[] response = Encoding.UTF8.GetBytes("OK");
                    context.Response.StatusCode = 200;
                    context.Response.OutputStream.Write(response, 0, response.Length);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            catch
            {
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
#endif

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (bridgeListener != null)
        {
            bridgeListener.Close();
            bridgeListener = null;
        }

        if (bridgeThread != null && bridgeThread.IsAlive)
            bridgeThread.Join(200);
#endif
        if (activeIntegration == this)
            activeIntegration = null;
    }

    private void ApplyWebViewMargins(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float left = corners[0].x;
        float bottom = corners[0].y;
        float right = corners[2].x;
        float top = corners[2].y;

        Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(null, new Vector3(left, bottom, 0));
        Vector2 screenTR = RectTransformUtility.WorldToScreenPoint(null, new Vector3(right, top, 0));

        int marginLeft = Mathf.RoundToInt(screenBL.x);
        int marginTop = Mathf.RoundToInt(Screen.height - screenTR.y);
        int marginRight = Mathf.RoundToInt(Screen.width - screenTR.x);
        int marginBottom = Mathf.RoundToInt(screenBL.y);

        webView.SetMargins(marginLeft, marginTop, marginRight, marginBottom);
    }

    private string GetStreamingAssetUrl(string relativePath)
    {
#if UNITY_ANDROID
        return "jar:file://" + Application.dataPath + "!/assets/StreamingAssets/" + relativePath;
#elif UNITY_IOS
        return Application.streamingAssetsPath + "/" + relativePath;
#else
        return "file:///" + Application.streamingAssetsPath + "/" + relativePath;
#endif
    }

    public void OnBlocklyMessage(string json)
    {
        if (TryHandleSaveRequest(json))
            return;

        if (interpreter != null)
            interpreter.HandleMessage(json);
    }

    private bool TryHandleSaveRequest(string json)
    {
        try
        {
            var saveRequest = JsonUtility.FromJson<BlocklySaveRequest>(json);
            if (saveRequest != null && saveRequest.saveCSharp && !string.IsNullOrEmpty(saveRequest.code))
            {
                SaveCSharpFile(saveRequest.fileName, saveRequest.code);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Blockly] Geen save request: {ex.Message}");
        }

        return false;
    }

    private void SaveCSharpFile(string fileName, string code)
    {
        if (string.IsNullOrEmpty(fileName))
            fileName = "BlocklyProgram.cs";

        string directory = Application.isEditor ? Path.Combine(Application.dataPath, "Generated") : Application.persistentDataPath;
        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, fileName);
        try
        {
            File.WriteAllText(filePath, code);
            Debug.Log($"[Blockly] C# opgeslagen naar: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Blockly] Fout bij opslaan C#-bestand: {ex.Message}");
        }
    }
}
