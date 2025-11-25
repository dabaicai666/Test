using UnityEditor;
using UnityEngine;

public class URPSetupGuide : EditorWindow
{
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/URP设置指南 (Unity 2022.3)")]
    static void OpenWindow()
    {
        var window = GetWindow<URPSetupGuide>("URP设置指南");
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
        GUILayout.Label("Unity 2022.3 URP 设置完整指南", titleStyle);
        GUILayout.Space(20);
        
        // === 第一部分：关于Unity 2022.3的重要说明 ===
        DrawSection("⚠️ Unity 2022.3 重要说明", () =>
        {
            EditorGUILayout.HelpBox(
                "在Unity 2022.3 (URP 14.x)中，SRP Batcher的界面位置可能与旧版本不同！\n\n" +
                "根据您的Unity版本，可能出现以下情况：\n" +
                "• SRP Batcher 已默认启用，无需手动设置\n" +
                "• 设置位置可能在不同的Inspector区域\n" +
                "• 某些情况下，该选项可能被隐藏或移除",
                MessageType.Warning
            );
        });
        
        GUILayout.Space(10);
        
        // === 第二部分：查找URP Asset的方法 ===
        DrawSection("📍 方法1：通过Project Settings查找", () =>
        {
            EditorGUILayout.LabelField("步骤：", EditorStyles.boldLabel);
            DrawStep("1", "打开 Edit → Project Settings");
            DrawStep("2", "左侧选择 'Graphics'");
            DrawStep("3", "右侧找到 'Scriptable Render Pipeline Settings'");
            DrawStep("4", "点击当前设置的Asset（如果有）");
            DrawStep("5", "这会在Inspector中显示URP Asset的完整设置");
            
            GUILayout.Space(5);
            if (GUILayout.Button("打开 Project Settings → Graphics"))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }
        });
        
        GUILayout.Space(10);
        
        // === 第三部分：使用诊断工具 ===
        DrawSection("🔧 方法2：使用诊断工具（推荐）", () =>
        {
            EditorGUILayout.HelpBox(
                "我们提供了专门的诊断工具，可以自动检测和启用SRP Batcher！",
                MessageType.Info
            );
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("打开 URP设置诊断工具", GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Tools/URP设置诊断工具");
            }
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("诊断工具功能：", EditorStyles.boldLabel);
            DrawBullet("自动查找当前使用的URP Asset");
            DrawBullet("显示SRP Batcher的当前状态");
            DrawBullet("一键启用/禁用SRP Batcher");
            DrawBullet("列出所有URP Asset的属性");
        });
        
        GUILayout.Space(10);
        
        // === 第四部分：手动查找Inspector中的设置 ===
        DrawSection("🔍 方法3：在Inspector中手动查找", () =>
        {
            EditorGUILayout.LabelField("可能的位置（取决于Unity版本）：", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            DrawLocation("位置A：General 区域", new string[] {
                "选中 URP Asset",
                "查看 Inspector 面板",
                "在 'General' 部分中查找",
                "可能有 'SRP Batcher' 复选框"
            });
            
            GUILayout.Space(5);
            
            DrawLocation("位置B：Advanced 区域（常见）", new string[] {
                "选中 URP Asset",
                "滚动到 Inspector 最底部",
                "展开 'Advanced' 折叠栏",
                "找到 'SRP Batcher' 复选框"
            });
            
            GUILayout.Space(5);
            
            DrawLocation("位置C：Quality 区域", new string[] {
                "选中 URP Asset",
                "查看 'Quality' 部分",
                "某些版本可能在这里"
            });
        });
        
        GUILayout.Space(10);
        
        // === 第五部分：如果找不到设置 ===
        DrawSection("❓ 如果找不到SRP Batcher设置", () =>
        {
            EditorGUILayout.HelpBox(
                "在某些Unity 2022.3版本中，SRP Batcher可能：\n" +
                "1. 已经默认启用，不需要手动设置\n" +
                "2. 通过代码自动管理\n" +
                "3. 在URP包更新后位置改变",
                MessageType.Info
            );
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("验证SRP Batcher是否工作：", EditorStyles.boldLabel);
            
            DrawStep("1", "在场景中创建一些测试物体");
            DrawStep("2", "进入Play模式");
            DrawStep("3", "打开 Window → Analysis → Frame Debugger");
            DrawStep("4", "Enable Frame Debugger");
            DrawStep("5", "查找 'SRP Batch' 或 'Render.SRPBatch' 字样");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("打开 Frame Debugger"))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "如果在Frame Debugger中看到 'SRP Batch' 字样，说明SRP Batcher已经在工作！",
                MessageType.Info
            );
        });
        
        GUILayout.Space(10);
        
        // === 第六部分：检查URP版本 ===
        DrawSection("📦 检查URP包版本", () =>
        {
            EditorGUILayout.LabelField("当前Unity版本：", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(Application.unityVersion);
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("打开 Package Manager 查看URP版本"))
            {
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
            }
            
            GUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "Unity 2022.3 通常使用 URP 14.x\n" +
                "不同的URP版本可能有不同的设置界面",
                MessageType.Info
            );
        });
        
        GUILayout.Space(10);
        
        // === 第七部分：快速操作 ===
        DrawSection("⚡ 快速操作", () =>
        {
            if (GUILayout.Button("运行完整诊断", GUILayout.Height(35)))
            {
                EditorApplication.ExecuteMenuItem("Tools/URP设置诊断工具");
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("查看当前渲染管线设置", GUILayout.Height(35)))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("创建测试场景（验证SRP Batcher）", GUILayout.Height(35)))
            {
                CreateTestScene();
            }
        });
        
        GUILayout.Space(20);
        
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
    
    void DrawStep(string number, string text)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label(number + ".", GUILayout.Width(20));
        GUILayout.Label(text);
        EditorGUILayout.EndHorizontal();
    }
    
    void DrawBullet(string text)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label("•", GUILayout.Width(20));
        GUILayout.Label(text);
        EditorGUILayout.EndHorizontal();
    }
    
    void DrawLocation(string title, string[] steps)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label(title, EditorStyles.boldLabel);
        foreach (var step in steps)
        {
            DrawBullet(step);
        }
        EditorGUILayout.EndVertical();
    }
    
    void CreateTestScene()
    {
        // 创建测试场景来验证SRP Batcher
        if (EditorUtility.DisplayDialog(
            "创建测试场景",
            "这将创建一个包含多个物体的测试场景，用于验证SRP Batcher是否工作。\n\n继续？",
            "创建", "取消"))
        {
            // 清理现有物体
            var existing = GameObject.Find("SRP_Test_Container");
            if (existing != null)
            {
                DestroyImmediate(existing);
            }
            
            // 创建容器
            GameObject container = new GameObject("SRP_Test_Container");
            
            // 创建材质
            Material sharedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sharedMat.name = "SharedMaterial";
            
            // 创建10个立方体
            for (int i = 0; i < 10; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"TestCube_{i}";
                cube.transform.position = new Vector3(i * 2, 0, 0);
                cube.transform.parent = container.transform;
                cube.GetComponent<MeshRenderer>().sharedMaterial = sharedMat;
            }
            
            // 添加检查脚本
            container.AddComponent<SRPBatcherRuntimeChecker>();
            
            // 保存材质
            string matPath = "Assets/TestSharedMaterial.mat";
            AssetDatabase.CreateAsset(sharedMat, matPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeGameObject = container;
            
            Debug.Log("✅ 测试场景已创建！");
            Debug.Log("进入Play模式，然后打开Frame Debugger查看SRP Batcher是否工作");
            
            EditorUtility.DisplayDialog(
                "测试场景已创建",
                "测试场景已创建成功！\n\n" +
                "下一步：\n" +
                "1. 进入 Play 模式\n" +
                "2. 打开 Window → Analysis → Frame Debugger\n" +
                "3. Enable Frame Debugger\n" +
                "4. 查找 'SRP Batch' 字样",
                "好的"
            );
        }
    }
}
