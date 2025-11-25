using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 渲染Pass分析工具
/// 帮助理解和分析当前场景的渲染Pass
/// </summary>
public class RenderPassAnalyzer : EditorWindow
{
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/渲染Pass分析工具")]
    static void OpenWindow()
    {
        var window = GetWindow<RenderPassAnalyzer>("Pass分析");
        window.minSize = new Vector2(700, 600);
        window.Show();
    }
    
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // 标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };
        
        GUILayout.Space(10);
        GUILayout.Label("Unity 渲染Pass完整流程分析", titleStyle);
        GUILayout.Space(20);
        
        // === 第一部分：当前管线信息 ===
        DrawSection("当前渲染管线", () =>
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            
            if (pipeline == null)
            {
                EditorGUILayout.HelpBox("Built-in 渲染管线", MessageType.Info);
                DrawBuiltInInfo();
            }
            else if (pipeline.GetType().Name.Contains("Universal"))
            {
                EditorGUILayout.HelpBox("URP（Universal Render Pipeline）", MessageType.Info);
                DrawURPInfo();
            }
            else
            {
                EditorGUILayout.HelpBox($"其他管线：{pipeline.GetType().Name}", MessageType.Info);
            }
        });
        
        GUILayout.Space(10);
        
        // === 第二部分：场景统计 ===
        DrawSection("场景统计", () =>
        {
            var renderers = FindObjectsOfType<MeshRenderer>();
            var lights = FindObjectsOfType<Light>();
            
            EditorGUILayout.LabelField("渲染器数量：", renderers.Length.ToString());
            
            int opaqueCount = 0;
            int transparentCount = 0;
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null)
                {
                    if (renderer.sharedMaterial.renderQueue < 3000)
                        opaqueCount++;
                    else
                        transparentCount++;
                }
            }
            
            EditorGUILayout.LabelField("  - 不透明物体：", opaqueCount.ToString());
            EditorGUILayout.LabelField("  - 透明物体：", transparentCount.ToString());
            
            GUILayout.Space(5);
            
            EditorGUILayout.LabelField("光源数量：", lights.Length.ToString());
            
            int dirLights = 0;
            int pointLights = 0;
            int spotLights = 0;
            
            foreach (var light in lights)
            {
                if (!light.enabled) continue;
                
                switch (light.type)
                {
                    case LightType.Directional: dirLights++; break;
                    case LightType.Point: pointLights++; break;
                    case LightType.Spot: spotLights++; break;
                }
            }
            
            EditorGUILayout.LabelField("  - 方向光：", dirLights.ToString());
            EditorGUILayout.LabelField("  - 点光源：", pointLights.ToString());
            EditorGUILayout.LabelField("  - 聚光灯：", spotLights.ToString());
        });
        
        GUILayout.Space(10);
        
        // === 第三部分：Pass流程预览 ===
        DrawSection("渲染Pass流程预览", () =>
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            
            if (pipeline == null)
            {
                Camera camera = Camera.main;
                if (camera != null && camera.actualRenderingPath == RenderingPath.Forward)
                {
                    DrawBuiltInForwardFlow();
                }
                else if (camera != null && camera.actualRenderingPath == RenderingPath.DeferredShading)
                {
                    DrawBuiltInDeferredFlow();
                }
            }
            else if (pipeline.GetType().Name.Contains("Universal"))
            {
                DrawURPFlow();
            }
        });
        
        GUILayout.Space(10);
        
        // === 第四部分：快速操作 ===
        DrawSection("快速操作", () =>
        {
            if (GUILayout.Button("打开 Frame Debugger", GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");
            }
            
            EditorGUILayout.HelpBox(
                "Frame Debugger使用指南：\n" +
                "1. 点击上方按钮打开Frame Debugger\n" +
                "2. 进入Play模式\n" +
                "3. 点击 Enable 按钮\n" +
                "4. 查看详细的Pass执行顺序",
                MessageType.Info
            );
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("打开 Profiler", GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("打开完整文档", GUILayout.Height(35)))
            {
                string docPath = System.IO.Path.Combine(Application.dataPath, "..", 
                    "Unity_Complete_RenderPass_Flow.md");
                if (System.IO.File.Exists(docPath))
                {
                    System.Diagnostics.Process.Start(docPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("文档", 
                        "文档位于项目根目录：\nUnity_Complete_RenderPass_Flow.md", "好的");
                }
            }
        });
        
        GUILayout.Space(10);
        
        EditorGUILayout.EndScrollView();
    }
    
    void DrawSection(string title, System.Action content)
    {
        EditorGUILayout.BeginVertical("box");
        
        GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14
        };
        GUILayout.Label(title, sectionStyle);
        
        GUILayout.Space(5);
        content?.Invoke();
        
        EditorGUILayout.EndVertical();
    }
    
    void DrawBuiltInInfo()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        
        GUILayout.Space(5);
        EditorGUILayout.LabelField("渲染路径：", camera.actualRenderingPath.ToString());
        
        if (camera.actualRenderingPath == RenderingPath.Forward)
        {
            EditorGUILayout.LabelField("像素光源限制：", QualitySettings.pixelLightCount.ToString());
            
            EditorGUILayout.HelpBox(
                "Built-in Forward特点：\n" +
                "• ForwardBase处理主光源\n" +
                "• 每个附加光源一个ForwardAdd Pass\n" +
                "• 光源数量直接影响DrawCall",
                MessageType.Info
            );
        }
        else if (camera.actualRenderingPath == RenderingPath.DeferredShading)
        {
            EditorGUILayout.HelpBox(
                "Built-in Deferred特点：\n" +
                "• 先渲染到GBuffer\n" +
                "• 再统一计算光照\n" +
                "• 光源数量影响较小\n" +
                "• 不支持透明物体和MSAA",
                MessageType.Info
            );
        }
    }
    
    void DrawURPInfo()
    {
        GUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "URP Forward特点：\n" +
            "• UniversalForward Pass处理所有光源\n" +
            "• SRP Batcher自动批处理\n" +
            "• 性能优于Built-in Forward\n" +
            "• 适合移动和PC平台",
            MessageType.Info
        );
    }
    
    void DrawBuiltInForwardFlow()
    {
        GUIStyle passStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true
        };
        
        EditorGUILayout.LabelField("<b>1. Shadow Pass</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"ShadowCaster\"", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>2. ForwardBase Pass</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"ForwardBase\"", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   └─ 主光源 + 环境光", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>3. ForwardAdd Pass (×N次)</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"ForwardAdd\"", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   └─ 每个附加光源一次", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>4. Skybox</b>", passStyle);
        EditorGUILayout.LabelField("<b>5. Transparent</b>", passStyle);
        EditorGUILayout.LabelField("<b>6. Post-Processing</b>", passStyle);
        EditorGUILayout.LabelField("<b>7. UI</b>", passStyle);
    }
    
    void DrawBuiltInDeferredFlow()
    {
        GUIStyle passStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true
        };
        
        EditorGUILayout.LabelField("<b>1. Shadow Pass</b>", passStyle);
        EditorGUILayout.LabelField("<b>2. Deferred Pass (GBuffer)</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"Deferred\"", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   └─ 输出到RT0-RT3", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>3. Deferred Lighting Pass</b>", passStyle);
        EditorGUILayout.LabelField("   └─ 读取GBuffer", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   └─ 应用所有光源", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>4. Skybox</b>", passStyle);
        EditorGUILayout.LabelField("<b>5. Transparent (Forward)</b>", passStyle);
        EditorGUILayout.LabelField("<b>6. Post-Processing</b>", passStyle);
        EditorGUILayout.LabelField("<b>7. UI</b>", passStyle);
    }
    
    void DrawURPFlow()
    {
        GUIStyle passStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true
        };
        
        EditorGUILayout.LabelField("<b>1. Setup Pass</b>", passStyle);
        EditorGUILayout.LabelField("   └─ 设置全局变量和光照", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>2. Shadow Pass</b>", passStyle);
        EditorGUILayout.LabelField("   └─ 级联阴影 + 阴影图集", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>3. Depth Prepass (可选)</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"DepthOnly\"", EditorStyles.miniLabel);
        
        EditorGUILayout.LabelField("<b>4. UniversalForward Pass ⭐</b>", passStyle);
        EditorGUILayout.LabelField("   └─ LightMode=\"UniversalForward\"", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   └─ <color=yellow>单Pass处理所有光源！</color>", passStyle);
        
        EditorGUILayout.LabelField("<b>5. Skybox</b>", passStyle);
        EditorGUILayout.LabelField("<b>6. Copy Color (可选)</b>", passStyle);
        EditorGUILayout.LabelField("<b>7. Transparent</b>", passStyle);
        EditorGUILayout.LabelField("<b>8. Post-Processing (Uber Shader)</b>", passStyle);
        EditorGUILayout.LabelField("<b>9. UI</b>", passStyle);
    }
}
