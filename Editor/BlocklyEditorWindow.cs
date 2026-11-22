using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class BlocklyEditorWindow : EditorWindow
{
    [MenuItem("Window/Easy Block Coding")]
    [MenuItem("Tools/Easy Block Coding")]
    public static void ShowWindow()
    {
        GetWindow<BlocklyEditorWindow>("Easy Block Coding");
    }

    private void OnGUI()
    {
        GUILayout.Label("Easy Block Coding", EditorStyles.boldLabel);
        GUILayout.Label("Quickly create the required objects and open the Blockly editor.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Create Blockly Setup"))
        {
            CreateBlocklySetup();
        }

        if (GUILayout.Button("Open Blockly Editor"))
        {
            OpenBlocklyEditor();
        }

        GUILayout.Space(10);
        GUILayout.Label("File Location:", EditorStyles.boldLabel);
        GUILayout.Label("Assets/StreamingAssets/Blockly/index.html", EditorStyles.wordWrappedLabel);
    }

    private static void CreateBlocklySetup()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "StreamingAssets/Blockly/index.html")))
        {
            EditorUtility.DisplayDialog("File Not Found", "Make sure Assets/StreamingAssets/Blockly/index.html exists.", "OK");
            return;
        }

        var canvas = Object.FindAnyObjectByType<Canvas>()?.gameObject;
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        var panel = CreatePanel(canvas.transform);

        var integrationGO = new GameObject("BlocklyIntegration");
        var integration = integrationGO.AddComponent<BlocklyIntegration>();
        integration.webViewRect = panel.GetComponent<RectTransform>();

        Selection.activeGameObject = integrationGO;
        EditorUtility.SetDirty(integration);
        AssetDatabase.SaveAssets();

        Debug.Log("Blockly setup created. Open the Blockly editor from the Window menu.");
    }

    private static GameObject CreateCanvas()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return canvasGO;
    }

    private static GameObject CreatePanel(Transform parent)
    {
        var panelGO = new GameObject("BlocklyPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(parent, false);
        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10, 10);
        rect.offsetMax = new Vector2(-10, -10);

        var image = panelGO.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.2f);
        return panelGO;
    }

    private static void OpenBlocklyEditor()
    {
        var path = Path.Combine(Application.dataPath, "StreamingAssets/Blockly/index.html");
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("File Not Found", "Make sure Assets/StreamingAssets/Blockly/index.html exists.", "OK");
            return;
        }

        Application.OpenURL("file:///" + path);
    }
}
