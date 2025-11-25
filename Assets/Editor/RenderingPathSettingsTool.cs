using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 渲染路径设置工具
/// 提供可视化界面来查看和修改渲染路径设置
/// </summary>
public class RenderingPathSettingsTool : EditorWindow
{
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/渲染路径设置工具")]
    static void OpenWindow()
    {
        var window = GetWindow<RenderingPathSettingsTool>("渲染路径设置");
        window.minSize = new Vector2(600, 500);
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
        GUILayout.Label("Unity 渲染路径设置工具", titleStyle);
        GUILayout.Space(10);
        
        // === 第一部分：检测当前管线 ===
        DrawSection("当前渲染管线", () =>
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            
            if (pipeline == null)
            {
                EditorGUILayout.HelpBox("使用 Built-in 渲染管线", MessageType.Info);
                DrawBuiltInSettings();
            }
            else if (pipeline.GetType().Name.Contains("Universal"))
            {
                EditorGUILayout.HelpBox("使用 URP（Universal Render Pipeline）", MessageType.Info);
                DrawURPSettings();
            }
            else if (pipeline.GetType().Name.Contains("HDRenderPipeline"))
            {
                EditorGUILayout.HelpBox("使用 HDRP（High Definition Render Pipeline）", MessageType.Info);
                DrawHDRPSettings();
            }
            else
            {
                EditorGUILayout.HelpBox($"使用自定义渲染管线：{pipeline.GetType().Name}", MessageType.Warning);
            }
        });
        
        GUILayout.Space(10);
        
        // === 第二部分：Quality Settings ===
        DrawSection("Quality Settings（全局设置）", () =>
        {
            if (GUILayout.Button("打开 Quality Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Quality");
            }
            
            GUILayout.Space(5);
            
            EditorGUILayout.LabelField("当前质量级别：", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        });
        
        GUILayout.Space(10);
        
        // === 第三部分：摄像机设置 ===
        DrawSection("摄像机设置", () =>
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            
            if (cameras.Length == 0)
            {
                EditorGUILayout.HelpBox("场景中没有摄像机", MessageType.Warning);
                return;
            }
            
            foreach (var camera in cameras)
            {
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.LabelField(camera.name, EditorStyles.boldLabel);
                
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline == null)
                {
                    // Built-in
                    EditorGUILayout.LabelField($"渲染路径：{camera.renderingPath}");
                    EditorGUILayout.LabelField($"实际路径：{camera.actualRenderingPath}");
                    
                    if (GUILayout.Button("选中此摄像机"))
                    {
                        Selection.activeGameObject = camera.gameObject;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("URP/HDRP中，渲染路径由Renderer Asset控制", MessageType.Info);
                    
                    if (GUILayout.Button("选中此摄像机"))
                    {
                        Selection.activeGameObject = camera.gameObject;
                    }
                }
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        });
        
        GUILayout.Space(10);
        
        // === 第四部分：快速操作 ===
        DrawSection("快速操作", () =>
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            
            if (pipeline == null)
            {
                // Built-in
                if (GUILayout.Button("切换到 Forward Rendering", GUILayout.Height(35)))
                {
                    QualitySettings.renderingPath = RenderingPath.Forward;
                    Debug.Log("已切换到 Forward Rendering");
                }
                
                if (GUILayout.Button("切换到 Deferred Rendering", GUILayout.Height(35)))
                {
                    QualitySettings.renderingPath = RenderingPath.DeferredShading;
                    Debug.Log("已切换到 Deferred Rendering");
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "URP/HDRP的渲染路径需要在Renderer Asset中设置\n" +
                    "点击下方按钮查找和选中Renderer Asset",
                    MessageType.Info
                );
                
                if (GUILayout.Button("查找并选中 Renderer Asset", GUILayout.Height(35)))
                {
                    FindAndSelectRendererAsset();
                }
            }
        });
        
        GUILayout.Space(10);
        
        // === 第五部分：诊断信息 ===
        DrawSection("诊断信息", () =>
        {
            if (GUILayout.Button("生成详细诊断报告", GUILayout.Height(35)))
            {
                GenerateDiagnosticReport();
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
    
    void DrawBuiltInSettings()
    {
        GUILayout.Space(5);
        
        EditorGUILayout.LabelField("渲染路径：", EditorStyles.boldLabel);
        RenderingPath currentPath = QualitySettings.renderingPath;
        EditorGUILayout.LabelField($"  {currentPath}");
        
        if (currentPath == RenderingPath.Forward)
        {
            EditorGUILayout.LabelField("像素光源数量：", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  {QualitySettings.pixelLightCount}");
            
            EditorGUILayout.HelpBox(
                "Forward渲染特点：\n" +
                "• Base Pass处理主光源\n" +
                "• 每个附加光源一个Additional Pass\n" +
                $"• 当前最多{QualitySettings.pixelLightCount}个像素光源",
                MessageType.Info
            );
        }
        else if (currentPath == RenderingPath.DeferredShading)
        {
            EditorGUILayout.HelpBox(
                "Deferred渲染特点：\n" +
                "• 先渲染到GBuffer\n" +
                "• 再统一计算光照\n" +
                "• 支持大量光源\n" +
                "• 不支持透明物体和MSAA",
                MessageType.Info
            );
        }
    }
    
    void DrawURPSettings()
    {
        GUILayout.Space(5);
        
        EditorGUILayout.LabelField("URP Asset路径：", EditorStyles.boldLabel);
        var urpAsset = GraphicsSettings.currentRenderPipeline;
        string assetPath = AssetDatabase.GetAssetPath(urpAsset);
        EditorGUILayout.SelectableLabel(assetPath, GUILayout.Height(18));
        
        if (GUILayout.Button("在Project窗口中定位"))
        {
            EditorGUIUtility.PingObject(urpAsset);
            Selection.activeObject = urpAsset;
        }
        
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "URP渲染路径设置步骤：\n" +
            "1. 选中上方的URP Asset\n" +
            "2. 在Inspector中找到 'Renderer List'\n" +
            "3. 点击Renderer（通常是ForwardRenderer）\n" +
            "4. 在Renderer Asset中找到 'Rendering Path'\n" +
            "5. 选择 Forward 或 Deferred",
            MessageType.Info
        );
    }
    
    void DrawHDRPSettings()
    {
        GUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "HDRP默认使用Deferred渲染路径\n" +
            "可以在HDRP Asset中配置详细设置",
            MessageType.Info
        );
    }
    
    void FindAndSelectRendererAsset()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return;
        
        // 使用反射获取Renderer
        var rendererDataList = pipeline.GetType()
            .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(pipeline) as ScriptableObject[];
        
        if (rendererDataList != null && rendererDataList.Length > 0)
        {
            var firstRenderer = rendererDataList[0];
            EditorGUIUtility.PingObject(firstRenderer);
            Selection.activeObject = firstRenderer;
            
            Debug.Log($"✅ 已选中 Renderer Asset: {firstRenderer.name}");
            Debug.Log($"路径：{AssetDatabase.GetAssetPath(firstRenderer)}");
        }
        else
        {
            Debug.LogWarning("未找到Renderer Asset");
        }
    }
    
    void GenerateDiagnosticReport()
    {
        Debug.Log("========== 渲染路径诊断报告 ==========\n");
        
        // 管线信息
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
        {
            Debug.Log("【渲染管线】Built-in Render Pipeline");
            Debug.Log($"【渲染路径】{QualitySettings.renderingPath}");
            
            if (QualitySettings.renderingPath == RenderingPath.Forward)
            {
                Debug.Log($"【像素光源数量】{QualitySettings.pixelLightCount}");
            }
        }
        else
        {
            Debug.Log($"【渲染管线】{pipeline.GetType().Name}");
            Debug.Log($"【Asset路径】{AssetDatabase.GetAssetPath(pipeline)}");
        }
        
        Debug.Log($"【当前质量级别】{QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        Debug.Log($"【Unity版本】{Application.unityVersion}");
        
        // 摄像机信息
        Camera[] cameras = FindObjectsOfType<Camera>();
        Debug.Log($"\n【场景摄像机数量】{cameras.Length}");
        
        foreach (var camera in cameras)
        {
            Debug.Log($"\n摄像机：{camera.name}");
            
            if (pipeline == null)
            {
                Debug.Log($"  - 设置的渲染路径：{camera.renderingPath}");
                Debug.Log($"  - 实际渲染路径：{camera.actualRenderingPath}");
            }
            else
            {
                Debug.Log($"  - 渲染路径：由Renderer Asset控制");
            }
            
            Debug.Log($"  - HDR：{camera.allowHDR}");
            Debug.Log($"  - MSAA：{camera.allowMSAA}");
        }
        
        // 光源信息
        Light[] lights = FindObjectsOfType<Light>();
        Debug.Log($"\n【场景光源数量】{lights.Length}");
        
        int directionalCount = 0;
        int pointCount = 0;
        int spotCount = 0;
        
        foreach (var light in lights)
        {
            if (!light.enabled) continue;
            
            switch (light.type)
            {
                case LightType.Directional: directionalCount++; break;
                case LightType.Point: pointCount++; break;
                case LightType.Spot: spotCount++; break;
            }
        }
        
        Debug.Log($"  - 方向光：{directionalCount}");
        Debug.Log($"  - 点光源：{pointCount}");
        Debug.Log($"  - 聚光灯：{spotCount}");
        
        // 性能分析
        Debug.Log("\n【性能分析】");
        
        if (pipeline == null)
        {
            if (QualitySettings.renderingPath == RenderingPath.Forward)
            {
                int additionalLights = directionalCount - 1 + pointCount + spotCount;
                Debug.Log($"Forward渲染：附加光源数量 = {additionalLights}");
                Debug.Log($"预估Additional Pass = 物体数 × {additionalLights}");
                
                if (additionalLights > QualitySettings.pixelLightCount)
                {
                    Debug.LogWarning($"⚠️ 附加光源({additionalLights})超过像素光源限制({QualitySettings.pixelLightCount})");
                    Debug.LogWarning("部分光源会降级为顶点光照");
                }
            }
            else if (QualitySettings.renderingPath == RenderingPath.DeferredShading)
            {
                Debug.Log($"Deferred渲染：光源数量({lights.Length})对性能影响较小");
                Debug.Log("适合大量光源的场景");
            }
        }
        
        Debug.Log("\n========== 报告结束 ==========\n");
    }
}
