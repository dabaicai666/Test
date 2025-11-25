using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 渲染Pass可视化器
/// 实时显示当前帧的所有渲染Pass
/// </summary>
public class RenderPassVisualizer : MonoBehaviour
{
    [Header("显示设置")]
    public bool showOnScreen = true;
    public KeyCode toggleKey = KeyCode.F2;
    
    [Header("统计信息")]
    public int totalSetPassCalls = 0;
    public int totalDrawCalls = 0;
    public int totalTriangles = 0;
    public int totalVertices = 0;
    
    private bool isVisible = true;
    private List<string> passHistory = new List<string>();
    private Vector2 scrollPosition;
    
    void Start()
    {
        AnalyzeRenderPipeline();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
        }
    }
    
    void AnalyzeRenderPipeline()
    {
        Debug.Log("========== 渲染Pass流程分析 ==========\n");
        
        var pipeline = GraphicsSettings.currentRenderPipeline;
        
        if (pipeline == null)
        {
            AnalyzeBuiltInPipeline();
        }
        else if (pipeline.GetType().Name.Contains("Universal"))
        {
            AnalyzeURPPipeline();
        }
        else if (pipeline.GetType().Name.Contains("HDRenderPipeline"))
        {
            Debug.Log("当前使用：HDRP（High Definition Render Pipeline）");
        }
        
        Debug.Log("\n========================================");
    }
    
    void AnalyzeBuiltInPipeline()
    {
        Debug.Log("【渲染管线】Built-in Render Pipeline\n");
        
        Camera camera = Camera.main;
        if (camera == null) return;
        
        RenderingPath path = camera.actualRenderingPath;
        Debug.Log($"【渲染路径】{path}\n");
        
        if (path == RenderingPath.Forward)
        {
            Debug.Log("=== Built-in Forward 渲染Pass流程 ===\n");
            Debug.Log("1. [CPU] 剔除阶段");
            Debug.Log("   └─ 视锥体剔除 + 遮挡剔除\n");
            
            Debug.Log("2. [GPU] 阴影Pass");
            Debug.Log("   └─ LightMode=\"ShadowCaster\"");
            Debug.Log("   └─ 为每个投射阴影的光源渲染ShadowMap\n");
            
            Debug.Log("3. [GPU] 深度/法线预通道（可选）");
            Debug.Log("   └─ LightMode=\"DepthNormals\"");
            Debug.Log("   └─ 输出：_CameraDepthNormalsTexture\n");
            
            Debug.Log("4. [GPU] 不透明物体主渲染");
            Debug.Log("   ├─ Pass A: ForwardBase");
            Debug.Log("   │   └─ LightMode=\"ForwardBase\"");
            Debug.Log("   │   └─ 处理：主光源 + 环境光 + 光照贴图");
            Debug.Log("   │");
            Debug.Log("   └─ Pass B: ForwardAdd（×N次，每个附加光源）");
            Debug.Log("       └─ LightMode=\"ForwardAdd\"");
            Debug.Log("       └─ Blend One One（累加）\n");
            
            int pixelLights = QualitySettings.pixelLightCount;
            Debug.Log($"   当前像素光源限制：{pixelLights}\n");
            
            Debug.Log("5. [GPU] 天空盒渲染");
            Debug.Log("   └─ 只渲染深度为远平面的像素\n");
            
            Debug.Log("6. [GPU] 透明物体渲染");
            Debug.Log("   ├─ 从后到前排序");
            Debug.Log("   ├─ ForwardBase Pass");
            Debug.Log("   └─ ForwardAdd Pass（×N次）\n");
            
            Debug.Log("7. [GPU] 后处理");
            Debug.Log("   └─ Post-Processing Stack v2\n");
            
            Debug.Log("8. [GPU] UI渲染");
            Debug.Log("   └─ Canvas（Overlay模式）\n");
        }
        else if (path == RenderingPath.DeferredShading)
        {
            Debug.Log("=== Built-in Deferred 渲染Pass流程 ===\n");
            Debug.Log("1. [CPU] 剔除阶段\n");
            
            Debug.Log("2. [GPU] 阴影Pass");
            Debug.Log("   └─ LightMode=\"ShadowCaster\"\n");
            
            Debug.Log("3. [GPU] 不透明物体GBuffer Pass");
            Debug.Log("   └─ LightMode=\"Deferred\"");
            Debug.Log("   └─ 输出到GBuffer（RT0~RT3）");
            Debug.Log("   └─ GBuffer包含：Albedo, Specular, Normal, Emission\n");
            
            Debug.Log("4. [GPU] Deferred Lighting Pass");
            Debug.Log("   ├─ 读取GBuffer");
            Debug.Log("   ├─ 全屏Quad应用主光源");
            Debug.Log("   └─ Light Volume应用附加光源\n");
            
            Debug.Log("5. [GPU] Deferred Reflections Pass");
            Debug.Log("   └─ 应用反射探针\n");
            
            Debug.Log("6. [GPU] 天空盒渲染\n");
            
            Debug.Log("7. [GPU] 透明物体渲染（回退到Forward）");
            Debug.Log("   └─ ForwardBase + ForwardAdd\n");
            
            Debug.Log("8. [GPU] 后处理\n");
            
            Debug.Log("9. [GPU] UI渲染\n");
        }
    }
    
    void AnalyzeURPPipeline()
    {
        Debug.Log("【渲染管线】URP（Universal Render Pipeline）\n");
        
        Debug.Log("=== URP 渲染Pass流程 ===\n");
        
        Debug.Log("1. [CPU] 剔除阶段");
        Debug.Log("   └─ ScriptableRenderContext.Cull()\n");
        
        Debug.Log("2. [GPU] Setup Pass");
        Debug.Log("   ├─ 设置全局Shader变量");
        Debug.Log("   └─ 设置光照数据\n");
        
        Debug.Log("3. [GPU] 阴影Pass");
        Debug.Log("   ├─ Main Light Shadow Pass");
        Debug.Log("   │   └─ LightMode=\"ShadowCaster\"");
        Debug.Log("   │   └─ 级联阴影（Cascaded Shadow Maps）");
        Debug.Log("   │   └─ 输出：_MainLightShadowmapTexture");
        Debug.Log("   │");
        Debug.Log("   └─ Additional Lights Shadow Pass");
        Debug.Log("       └─ LightMode=\"ShadowCaster\"");
        Debug.Log("       └─ Shadow Atlas");
        Debug.Log("       └─ 输出：_AdditionalLightsShadowmapTexture\n");
        
        Debug.Log("4. [GPU] 深度预通道（可选）");
        Debug.Log("   └─ LightMode=\"DepthOnly\"");
        Debug.Log("   └─ 输出：_CameraDepthTexture\n");
        
        Debug.Log("5. [GPU] 深度法线预通道（可选）");
        Debug.Log("   └─ LightMode=\"DepthNormals\"");
        Debug.Log("   └─ 输出：_CameraDepthNormalsTexture\n");
        
        Debug.Log("6. [GPU] SSAO Pass（可选）");
        Debug.Log("   └─ 屏幕空间环境光遮蔽\n");
        
        Debug.Log("7. [GPU] 不透明物体主渲染");
        Debug.Log("   └─ UniversalForward Pass ⭐");
        Debug.Log("       └─ LightMode=\"UniversalForward\"");
        Debug.Log("       └─ 在一个Pass中处理所有光源！");
        Debug.Log("       └─ 主光源 + 附加光源（0-8个）");
        Debug.Log("       └─ SRP Batcher自动批处理\n");
        
        Debug.Log("8. [GPU] 天空盒渲染\n");
        
        Debug.Log("9. [GPU] 拷贝颜色纹理（可选）");
        Debug.Log("   └─ 输出：_CameraOpaqueTexture");
        Debug.Log("   └─ 供透明物体读取背景\n");
        
        Debug.Log("10. [GPU] 透明物体渲染");
        Debug.Log("    └─ UniversalForward Pass");
        Debug.Log("    └─ 同样在一个Pass处理所有光源\n");
        
        Debug.Log("11. [GPU] 后处理");
        Debug.Log("    └─ Uber Shader（多效果合并）");
        Debug.Log("    └─ Bloom + DoF + Color Grading + ...\n");
        
        Debug.Log("12. [GPU] UI渲染");
        Debug.Log("    └─ Canvas（Overlay模式）\n");
        
        Debug.Log("⭐ URP优势：");
        Debug.Log("  ✅ Forward Pass处理所有光源（不像Built-in需要ForwardAdd）");
        Debug.Log("  ✅ SRP Batcher大幅减少CPU开销");
        Debug.Log("  ✅ Uber Shader后处理更高效");
        Debug.Log("  ✅ 更适合移动平台\n");
    }
    
    void OnGUI()
    {
        if (!showOnScreen || !isVisible) return;
        
        // 背景框
        GUI.Box(new Rect(10, 10, 400, 300), "");
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        
        GUIStyle normalStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };
        
        float y = 20;
        
        // 标题
        GUI.Label(new Rect(20, y, 380, 25), "渲染Pass流程可视化", titleStyle);
        y += 30;
        
        // 管线信息
        var pipeline = GraphicsSettings.currentRenderPipeline;
        string pipelineName = pipeline == null ? "Built-in" : 
                             pipeline.GetType().Name.Contains("Universal") ? "URP" : "HDRP";
        
        GUI.Label(new Rect(20, y, 150, 20), "渲染管线:", normalStyle);
        GUI.Label(new Rect(180, y, 200, 20), pipelineName, normalStyle);
        y += 25;
        
        // 渲染路径
        if (pipeline == null)
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                GUI.Label(new Rect(20, y, 150, 20), "渲染路径:", normalStyle);
                GUI.Label(new Rect(180, y, 200, 20), camera.actualRenderingPath.ToString(), normalStyle);
                y += 25;
            }
        }
        
        // 主要Pass列表
        GUI.Label(new Rect(20, y, 380, 20), "主要Pass流程:", titleStyle);
        y += 25;
        
        GUIStyle passStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Color.yellow }
        };
        
        if (pipelineName == "URP")
        {
            string[] passes = new string[]
            {
                "1. Setup Pass",
                "2. Shadow Pass",
                "3. Depth Prepass",
                "4. UniversalForward ⭐",
                "5. Skybox",
                "6. Transparent",
                "7. Post-Processing",
                "8. UI"
            };
            
            foreach (var pass in passes)
            {
                GUI.Label(new Rect(30, y, 350, 20), pass, passStyle);
                y += 18;
            }
        }
        else
        {
            string[] passes = new string[]
            {
                "1. Shadow Pass",
                "2. ForwardBase/Deferred",
                "3. ForwardAdd (×N)",
                "4. Skybox",
                "5. Transparent",
                "6. Post-Processing",
                "7. UI"
            };
            
            foreach (var pass in passes)
            {
                GUI.Label(new Rect(30, y, 350, 20), pass, passStyle);
                y += 18;
            }
        }
        
        y += 10;
        
        // 提示
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Color.gray }
        };
        
        GUI.Label(new Rect(20, y, 380, 20), $"按 {toggleKey} 键切换显示", hintStyle);
        y += 15;
        GUI.Label(new Rect(20, y, 380, 20), "打开 Frame Debugger 查看详细Pass", hintStyle);
        y += 15;
        GUI.Label(new Rect(20, y, 380, 20), "查看文档：Unity_Complete_RenderPass_Flow.md", hintStyle);
    }
}
