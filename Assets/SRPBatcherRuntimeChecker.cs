using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SRPBatcherRuntimeChecker : MonoBehaviour
{
    [Header("显示设置")]
    public bool showOnScreenInfo = true;
    public KeyCode toggleKey = KeyCode.F1;
    
    private bool isVisible = true;
    
    void Start()
    {
        CheckSRPBatcherAtStart();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
        }
    }
    
    void CheckSRPBatcherAtStart()
    {
        Debug.Log("========== SRP Batcher 启动检查 ==========");
        
        // 检查全局状态
        bool globalEnabled = GraphicsSettings.useScriptableRenderPipelineBatching;
        Debug.Log($"全局 SRP Batcher 状态: {(globalEnabled ? "✅ 启用" : "❌ 未启用")}");
        
        // 检查当前渲染管线
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline is UniversalRenderPipelineAsset urpAsset)
        {
            Debug.Log($"✅ 使用URP渲染管线");
            Debug.Log($"Unity版本: {Application.unityVersion}");
            
            // 在Unity 2022.3中，SRP Batcher通常默认启用
            if (globalEnabled)
            {
                Debug.Log("✅ SRP Batcher 正常工作！");
                Debug.Log("提示：打开 Window → Analysis → Frame Debugger 可以看到 'SRP Batch' 标记");
            }
            else
            {
                Debug.LogWarning("⚠️ SRP Batcher 未启用！");
                Debug.LogWarning("请使用编辑器菜单：Tools → URP设置诊断工具 来启用它");
            }
        }
        else if (pipeline == null)
        {
            Debug.LogError("❌ 未设置渲染管线！");
        }
        else
        {
            Debug.LogError($"❌ 不是URP管线: {pipeline.GetType().Name}");
        }
        
        Debug.Log("==========================================");
    }
    
    void OnGUI()
    {
        if (!showOnScreenInfo || !isVisible) return;
        
        // 创建半透明背景
        GUI.Box(new Rect(10, 10, 350, 180), "");
        
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
        
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        
        float y = 20;
        
        GUI.Label(new Rect(20, y, 300, 25), "SRP Batcher 状态", titleStyle);
        y += 30;
        
        // 显示全局状态
        bool globalEnabled = GraphicsSettings.useScriptableRenderPipelineBatching;
        GUI.Label(new Rect(20, y, 150, 20), "SRP Batcher:", normalStyle);
        statusStyle.normal.textColor = globalEnabled ? Color.green : Color.red;
        GUI.Label(new Rect(180, y, 150, 20), globalEnabled ? "✅ 启用" : "❌ 未启用", statusStyle);
        y += 25;
        
        // 显示Unity版本
        GUI.Label(new Rect(20, y, 150, 20), "Unity版本:", normalStyle);
        GUI.Label(new Rect(180, y, 150, 20), Application.unityVersion, normalStyle);
        y += 25;
        
        // 显示渲染管线
        var pipeline = GraphicsSettings.currentRenderPipeline;
        GUI.Label(new Rect(20, y, 150, 20), "渲染管线:", normalStyle);
        string pipelineName = pipeline != null ? "URP" : "未设置";
        GUI.Label(new Rect(180, y, 150, 20), pipelineName, normalStyle);
        y += 25;
        
        // 显示FPS
        float fps = 1.0f / Time.deltaTime;
        GUI.Label(new Rect(20, y, 150, 20), "FPS:", normalStyle);
        Color fpsColor = fps > 55 ? Color.green : fps > 30 ? Color.yellow : Color.red;
        statusStyle.normal.textColor = fpsColor;
        GUI.Label(new Rect(180, y, 150, 20), $"{fps:F1}", statusStyle);
        y += 30;
        
        // 提示信息
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Color.gray }
        };
        GUI.Label(new Rect(20, y, 300, 20), $"按 {toggleKey} 键切换显示", hintStyle);
        y += 15;
        GUI.Label(new Rect(20, y, 300, 20), "打开 Frame Debugger 查看详细信息", hintStyle);
    }
}
