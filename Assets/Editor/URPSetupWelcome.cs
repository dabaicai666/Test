using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class URPSetupWelcome : EditorWindow
{
    private const string SHOW_ON_START_KEY = "URPSetup_ShowWelcome";
    private static bool showOnStartup = true;
    
    static URPSetupWelcome()
    {
        EditorApplication.delayCall += ShowWelcomeWindow;
    }
    
    static void ShowWelcomeWindow()
    {
        showOnStartup = EditorPrefs.GetBool(SHOW_ON_START_KEY, true);
        
        if (showOnStartup)
        {
            OpenWindow();
        }
    }
    
    [MenuItem("Tools/URP欢迎向导")]
    static void OpenWindow()
    {
        var window = GetWindow<URPSetupWelcome>("URP设置向导");
        window.minSize = new Vector2(500, 400);
        window.maxSize = new Vector2(500, 400);
        window.Show();
    }
    
    void OnGUI()
    {
        // 标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.3f, 0.6f, 1f) }
        };
        
        GUILayout.Space(20);
        GUILayout.Label("🎉 Unity URP 设置工具", titleStyle);
        GUILayout.Space(10);
        
        // 版本信息
        GUIStyle versionStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = Color.gray }
        };
        GUILayout.Label($"Unity {Application.unityVersion} 专用", versionStyle);
        GUILayout.Space(20);
        
        // 说明
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("欢迎使用URP设置工具集！", EditorStyles.boldLabel);
        GUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "这个工具集可以帮助您：\n\n" +
            "✅ 快速找到和配置SRP Batcher\n" +
            "✅ 诊断URP Asset设置\n" +
            "✅ 验证渲染管线配置\n" +
            "✅ 创建测试场景",
            MessageType.Info
        );
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(20);
        
        // 快速操作按钮
        EditorGUILayout.LabelField("选择一个工具开始：", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // 按钮1：诊断工具
        if (GUILayout.Button("🔧 URP设置诊断工具（推荐开始这里）", GUILayout.Height(40)))
        {
            EditorApplication.ExecuteMenuItem("Tools/URP设置诊断工具");
        }
        EditorGUILayout.HelpBox("自动检测URP Asset，一键启用SRP Batcher", MessageType.None);
        
        GUILayout.Space(10);
        
        // 按钮2：设置指南
        if (GUILayout.Button("📖 URP设置指南", GUILayout.Height(40)))
        {
            EditorApplication.ExecuteMenuItem("Tools/URP设置指南 (Unity 2022.3)");
        }
        EditorGUILayout.HelpBox("详细的设置说明和手动操作指南", MessageType.None);
        
        GUILayout.Space(10);
        
        // 按钮3：阅读文档
        if (GUILayout.Button("📄 查看完整文档", GUILayout.Height(40)))
        {
            string readmePath = System.IO.Path.Combine(Application.dataPath, "..", "README_URP_Setup.md");
            if (System.IO.File.Exists(readmePath))
            {
                System.Diagnostics.Process.Start(readmePath);
            }
            else
            {
                EditorUtility.DisplayDialog("文档", 
                    "文档位于项目根目录：\nREADME_URP_Setup.md", "好的");
            }
        }
        EditorGUILayout.HelpBox("打开完整的Markdown文档", MessageType.None);
        
        GUILayout.Space(20);
        
        // 底部选项
        EditorGUILayout.BeginVertical("box");
        bool newShowOnStartup = EditorGUILayout.Toggle("启动时显示此窗口", showOnStartup);
        if (newShowOnStartup != showOnStartup)
        {
            showOnStartup = newShowOnStartup;
            EditorPrefs.SetBool(SHOW_ON_START_KEY, showOnStartup);
        }
        
        GUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("关闭", GUILayout.Width(100)))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
}
