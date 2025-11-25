using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// URP Pass流程可视化工具
/// 实时显示当前帧的Pass执行情况
/// </summary>
public class URPPassFlowVisualizer : MonoBehaviour
{
    [Header("显示设置")]
    [SerializeField] private bool showOnScreen = true;
    [SerializeField] private bool showInConsole = false;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    
    [Header("统计信息")]
    [SerializeField] private bool trackPerformance = true;
    [SerializeField] private int sampleFrames = 60;
    
    private float fps;
    private int frameCount;
    private float deltaTime;
    
    private GUIStyle headerStyle;
    private GUIStyle normalStyle;
    private GUIStyle highlightStyle;
    private bool stylesInitialized = false;
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showOnScreen = !showOnScreen;
        }
        
        if (trackPerformance)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            fps = 1.0f / deltaTime;
            frameCount++;
            
            if (frameCount % sampleFrames == 0 && showInConsole)
            {
                LogPassFlow();
            }
        }
    }
    
    private void OnGUI()
    {
        if (!showOnScreen) return;
        
        if (!stylesInitialized)
        {
            InitializeStyles();
        }
        
        DrawPassFlowWindow();
    }
    
    private void InitializeStyles()
    {
        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan }
        };
        
        normalStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };
        
        highlightStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow }
        };
        
        stylesInitialized = true;
    }
    
    private void DrawPassFlowWindow()
    {
        float windowWidth = 600;
        float windowHeight = Screen.height * 0.9f;
        float x = Screen.width - windowWidth - 10;
        float y = 10;
        
        GUILayout.BeginArea(new Rect(x, y, windowWidth, windowHeight), GUI.skin.box);
        
        GUILayout.Label("🎬 URP 渲染Pass流程可视化", headerStyle);
        GUILayout.Space(5);
        
        if (trackPerformance)
        {
            GUILayout.Label($"FPS: {fps:F1} | 帧时间: {deltaTime * 1000:F2}ms", highlightStyle);
        }
        
        GUILayout.Space(10);
        
        // 检测当前渲染管线
        var pipeline = GraphicsSettings.currentRenderPipeline;
        bool isURP = pipeline != null && pipeline.GetType().Name.Contains("Universal");
        
        if (!isURP)
        {
            GUILayout.Label("⚠️ 当前不是URP管线！", highlightStyle);
            GUILayout.Label("请在 Project Settings → Graphics 中设置URP Asset", normalStyle);
            GUILayout.EndArea();
            return;
        }
        
        GUILayout.Label($"✅ 当前管线: {pipeline.GetType().Name}", normalStyle);
        GUILayout.Label($"Unity版本: {Application.unityVersion}", normalStyle);
        
        GUILayout.Space(10);
        DrawSeparator();
        
        // 显示Pass流程
        DrawPassStage("0️⃣", "CPU准备阶段", "剔除、排序、构建命令", Color.gray, 0.5f);
        DrawPassStage("1️⃣", "Setup Pass", "设置全局变量和光照数据", Color.white, 0.1f);
        DrawPassStage("2️⃣", "阴影Pass", "LightMode=\"ShadowCaster\"", Color.cyan, 0.8f);
        DrawPassStage("3️⃣", "深度预通道", "LightMode=\"DepthOnly\" (可选)", Color.blue, 0.5f);
        
        GUILayout.Space(5);
        DrawPassStage("⭐", "UniversalForward Pass", "渲染不透明物体 + 所有光照计算", Color.yellow, 3.0f, true);
        GUILayout.Label("    ├─ 主光源光照计算", normalStyle);
        GUILayout.Label("    ├─ 循环计算附加光源", normalStyle);
        GUILayout.Label("    └─ 环境光和GI", normalStyle);
        GUILayout.Space(5);
        
        DrawPassStage("5️⃣", "天空盒Pass", "渲染背景天空", Color.cyan, 0.3f);
        DrawPassStage("6️⃣", "拷贝颜色纹理", "供透明物体使用 (可选)", Color.magenta, 0.5f);
        DrawPassStage("7️⃣", "透明物体Pass", "同样使用UniversalForward", new Color(0.5f, 1f, 0.5f), 1.5f);
        DrawPassStage("8️⃣", "后处理Pass", "Uber Shader合并效果", Color.magenta, 2.5f);
        DrawPassStage("9️⃣", "UI渲染", "Canvas和UI元素", Color.white, 1.0f);
        
        GUILayout.Space(10);
        DrawSeparator();
        
        // 显示关键概念
        GUILayout.Label("💡 关键理解", headerStyle);
        GUILayout.Label("• UniversalForward = 不透明物体渲染Pass", highlightStyle);
        GUILayout.Label("• 光照在片元着色器中计算", normalStyle);
        GUILayout.Label("• 一个Pass处理所有光源（循环）", normalStyle);
        GUILayout.Label("• 透明物体也用UniversalForward", normalStyle);
        
        GUILayout.Space(10);
        GUILayout.Label($"按 [{toggleKey}] 切换显示", normalStyle);
        
        GUILayout.EndArea();
    }
    
    private void DrawPassStage(string icon, string name, string description, Color color, float estimatedTime, bool highlight = false)
    {
        var originalColor = GUI.color;
        
        if (highlight)
        {
            GUI.color = new Color(1f, 1f, 0.5f, 0.3f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = originalColor;
        }
        
        GUILayout.BeginHorizontal();
        GUILayout.Label(icon, highlightStyle, GUILayout.Width(40));
        
        GUI.color = color;
        GUILayout.Label(name, highlight ? highlightStyle : normalStyle, GUILayout.Width(200));
        GUI.color = originalColor;
        
        GUILayout.Label($"~{estimatedTime:F1}ms", normalStyle);
        GUILayout.EndHorizontal();
        
        GUILayout.Label($"  {description}", normalStyle);
        
        if (highlight)
        {
            GUILayout.EndVertical();
        }
        
        GUILayout.Space(3);
    }
    
    private void DrawSeparator()
    {
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
    }
    
    private void LogPassFlow()
    {
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("🎬 URP 渲染Pass流程");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("0️⃣ CPU准备 → 剔除、排序");
        Debug.Log("1️⃣ Setup Pass → 设置全局变量");
        Debug.Log("2️⃣ Shadow Pass → ShadowCaster");
        Debug.Log("3️⃣ Depth Prepass → DepthOnly (可选)");
        Debug.Log("⭐ UniversalForward Pass → 不透明物体 + 所有光照");
        Debug.Log("   ├─ 主光源光照");
        Debug.Log("   ├─ 循环计算附加光源");
        Debug.Log("   └─ 环境光和GI");
        Debug.Log("5️⃣ Skybox Pass → 天空盒");
        Debug.Log("6️⃣ Copy Color → 拷贝颜色纹理 (可选)");
        Debug.Log("7️⃣ Transparent Pass → UniversalForward");
        Debug.Log("8️⃣ Post-Processing → Uber Shader");
        Debug.Log("9️⃣ UI Pass → Canvas渲染");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log($"FPS: {fps:F1} | 帧时间: {deltaTime * 1000:F2}ms");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(URPPassFlowVisualizer))]
public class URPPassFlowVisualizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "📚 使用说明：\n\n" +
            "1. 将此脚本添加到场景中的任意GameObject\n" +
            "2. 运行游戏后会在屏幕右侧显示Pass流程\n" +
            "3. 按F3（或自定义按键）切换显示\n" +
            "4. 查看UniversalForward如何处理所有光照\n\n" +
            "💡 关键理解：\n" +
            "• UniversalForward Pass = 不透明物体渲染\n" +
            "• 光照计算在这个Pass的片元着色器中完成\n" +
            "• 主光源 + 附加光源全部在一个Pass循环计算",
            MessageType.Info
        );
        
        if (GUILayout.Button("📖 查看详细文档", GUILayout.Height(30)))
        {
            var docPath = System.IO.Path.Combine(
                Application.dataPath.Replace("Assets", ""), 
                "URP_Detailed_Pass_Flow.md"
            );
            
            if (System.IO.File.Exists(docPath))
            {
                Application.OpenURL("file://" + docPath);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "文档未找到", 
                    "找不到 URP_Detailed_Pass_Flow.md\n请确保文档在项目根目录", 
                    "确定"
                );
            }
        }
        
        URPPassFlowVisualizer visualizer = (URPPassFlowVisualizer)target;
        
        EditorGUILayout.Space(10);
        if (GUILayout.Button("🔍 打开Frame Debugger", GUILayout.Height(30)))
        {
            EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");
            EditorGUILayout.HelpBox(
                "Frame Debugger可以查看每个Pass的详细执行顺序！\n" +
                "运行游戏后点击'Enable'开始调试。",
                MessageType.Info
            );
        }
        
        EditorGUILayout.Space(5);
        if (GUILayout.Button("📊 打开Profiler", GUILayout.Height(30)))
        {
            EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
        }
    }
}
#endif
