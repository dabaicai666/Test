using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public class URPSettingsDiagnostic : EditorWindow
{
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/URP设置诊断工具")]
    static void OpenWindow()
    {
        var window = GetWindow<URPSettingsDiagnostic>("URP设置诊断");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }
    
    void OnGUI()
    {
        GUILayout.Label("URP Asset 完整信息诊断", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        var pipeline = GraphicsSettings.currentRenderPipeline;
        
        if (pipeline == null)
        {
            EditorGUILayout.HelpBox("❌ 未找到渲染管线Asset！项目可能没有使用URP。", MessageType.Error);
            
            GUILayout.Space(10);
            if (GUILayout.Button("创建URP Asset", GUILayout.Height(30)))
            {
                CreateURPAsset();
            }
            return;
        }
        
        if (pipeline is UniversalRenderPipelineAsset urpAsset)
        {
            EditorGUILayout.HelpBox($"✅ 找到URP Asset", MessageType.Info);
            
            // 显示Asset路径
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Asset路径:", GUILayout.Width(80));
            EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(urpAsset), GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("在Project窗口中定位此Asset"))
            {
                EditorGUIUtility.PingObject(urpAsset);
                Selection.activeObject = urpAsset;
            }
            
            GUILayout.Space(10);
            
            // 显示所有可序列化的属性
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("=== URP Asset 所有属性 ===", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            SerializedObject so = new SerializedObject(urpAsset);
            SerializedProperty prop = so.GetIterator();
            
            bool foundSRPBatcher = false;
            SerializedProperty srpBatcherProp = null;
            
            if (prop.NextVisible(true))
            {
                do
                {
                    // 查找SRP Batcher相关的属性
                    if (prop.name.ToLower().Contains("srp") || 
                        prop.name.ToLower().Contains("batch"))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(prop.name, GUILayout.Width(200));
                        EditorGUILayout.LabelField($"= {GetPropertyValue(prop)}", EditorStyles.boldLabel);
                        EditorGUILayout.EndHorizontal();
                        
                        if (prop.name.Contains("UseSRPBatcher") || prop.name == "m_UseSRPBatcher")
                        {
                            foundSRPBatcher = true;
                            srpBatcherProp = prop.Copy();
                        }
                    }
                    else
                    {
                        // 显示其他属性（折叠）
                        EditorGUI.indentLevel = prop.depth;
                        EditorGUILayout.LabelField($"{prop.name}: {GetPropertyValue(prop)}", EditorStyles.miniLabel);
                    }
                }
                while (prop.NextVisible(false));
            }
            
            EditorGUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // SRP Batcher控制区域
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("=== SRP Batcher 控制 ===", EditorStyles.boldLabel);
            
            // 检查全局状态
            bool globalEnabled = GraphicsSettings.useScriptableRenderPipelineBatching;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("全局SRP Batcher状态:", GUILayout.Width(150));
            EditorGUILayout.LabelField(globalEnabled ? "✅ 启用" : "❌ 未启用", 
                globalEnabled ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();
            
            // 尝试直接读取属性
            var useSRPBatcherProp = so.FindProperty("m_UseSRPBatcher");
            if (useSRPBatcherProp != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Asset中的SRP Batcher:", GUILayout.Width(150));
                EditorGUILayout.LabelField(useSRPBatcherProp.boolValue ? "✅ 启用" : "❌ 未启用",
                    useSRPBatcherProp.boolValue ? EditorStyles.boldLabel : EditorStyles.label);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                
                // 切换按钮
                if (GUILayout.Button(useSRPBatcherProp.boolValue ? "禁用 SRP Batcher" : "启用 SRP Batcher", 
                    GUILayout.Height(30)))
                {
                    useSRPBatcherProp.boolValue = !useSRPBatcherProp.boolValue;
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"✅ SRP Batcher已{(useSRPBatcherProp.boolValue ? "启用" : "禁用")}！");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("⚠️ 在Asset中未找到m_UseSRPBatcher属性", MessageType.Warning);
                EditorGUILayout.HelpBox("这可能意味着：\n1. SRP Batcher在此版本中默认启用\n2. 属性名称已更改\n3. 需要通过其他方式设置", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // 运行时检查
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("=== 运行时状态 ===", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("游戏运行中，可以查看Frame Debugger验证SRP Batcher是否工作", MessageType.Info);
                
                if (GUILayout.Button("打开 Frame Debugger"))
                {
                    EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");
                }
                EditorGUILayout.EndVertical();
            }
            
        }
        else
        {
            EditorGUILayout.HelpBox($"❌ 当前渲染管线不是URP: {pipeline.GetType().Name}", MessageType.Error);
        }
    }
    
    string GetPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return prop.boolValue.ToString();
            case SerializedPropertyType.Integer:
                return prop.intValue.ToString();
            case SerializedPropertyType.Float:
                return prop.floatValue.ToString("F2");
            case SerializedPropertyType.String:
                return prop.stringValue;
            case SerializedPropertyType.Enum:
                return prop.enumDisplayNames[prop.enumValueIndex];
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
            default:
                return prop.propertyType.ToString();
        }
    }
    
    void CreateURPAsset()
    {
        // 在Assets根目录创建Settings文件夹
        string folderPath = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }
        
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/UniversalRP.asset");
        string rendererPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/UniversalRP_Renderer.asset");
        
        // 创建Renderer
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);
        
        // 创建URP Asset
        var urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        AssetDatabase.CreateAsset(urpAsset, assetPath);
        
        // 设置Renderer
        SerializedObject so = new SerializedObject(urpAsset);
        SerializedProperty renderersProp = so.FindProperty("m_RendererDataList");
        if (renderersProp != null && renderersProp.isArray)
        {
            renderersProp.arraySize = 1;
            renderersProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            so.ApplyModifiedProperties();
        }
        
        // 启用SRP Batcher
        SerializedProperty srpBatcherProp = so.FindProperty("m_UseSRPBatcher");
        if (srpBatcherProp != null)
        {
            srpBatcherProp.boolValue = true;
            so.ApplyModifiedProperties();
        }
        
        AssetDatabase.SaveAssets();
        
        // 设置为当前渲染管线
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        
        Debug.Log($"✅ 已创建URP Asset: {assetPath}");
        Debug.Log($"✅ 已创建Renderer: {rendererPath}");
        
        EditorGUIUtility.PingObject(urpAsset);
        Selection.activeObject = urpAsset;
    }
}
