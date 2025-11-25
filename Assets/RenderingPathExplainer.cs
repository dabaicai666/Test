using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Unity渲染路径详解
/// 
/// 渲染路径（Rendering Path）决定了Unity如何渲染光照和着色
/// </summary>
public class RenderingPathExplainer : MonoBehaviour
{
    [Header("渲染路径信息")]
    [SerializeField] private bool showDebugInfo = true;
    
    void Start()
    {
        ExplainRenderingPaths();
        ShowCurrentRenderingPath();
    }
    
    /// <summary>
    /// 解释所有渲染路径
    /// </summary>
    void ExplainRenderingPaths()
    {
        Debug.Log("========== Unity 渲染路径详解 ==========\n");
        
        Debug.Log("📍 传统渲染管线（Built-in）的渲染路径：\n");
        
        Debug.Log("1. Forward Rendering（前向渲染）");
        Debug.Log("   - 特点：对每个物体，遍历所有影响它的光源");
        Debug.Log("   - Pass：Base Pass（主光源）+ Additional Pass（每个附加光源一个Pass）");
        Debug.Log("   - 优点：实现简单，透明物体友好");
        Debug.Log("   - 缺点：光源多时性能差（O(物体数×光源数)）");
        Debug.Log("   - 适用：移动平台、光源较少的场景\n");
        
        Debug.Log("2. Deferred Rendering（延迟渲染）");
        Debug.Log("   - 特点：先渲染几何信息到GBuffer，再统一计算光照");
        Debug.Log("   - Pass：Deferred Pass（几何）+ Lighting Pass（光照）");
        Debug.Log("   - 优点：光源数量对性能影响小（O(物体数+光源数)）");
        Debug.Log("   - 缺点：不支持透明、不支持MSAA、显存占用高");
        Debug.Log("   - 适用：PC、主机、大量光源的场景\n");
        
        Debug.Log("3. Legacy Vertex Lit（顶点光照，已废弃）");
        Debug.Log("   - 特点：在顶点着色器计算光照");
        Debug.Log("   - 优点：性能最好");
        Debug.Log("   - 缺点：质量最差，已不推荐使用\n");
        
        Debug.Log("📍 URP（Universal Render Pipeline）的渲染路径：\n");
        
        Debug.Log("4. URP Forward（URP前向渲染）");
        Debug.Log("   - 特点：改进的前向渲染，单Pass处理所有光源");
        Debug.Log("   - Pass：Universal Forward Pass（一个Pass处理主光源+多个附加光源）");
        Debug.Log("   - 优点：性能优于传统Forward，支持更多特性");
        Debug.Log("   - 缺点：仍然受光源数量影响");
        Debug.Log("   - 适用：移动端和PC的通用方案\n");
        
        Debug.Log("5. URP Deferred（URP延迟渲染，Unity 2021+）");
        Debug.Log("   - 特点：URP版本的延迟渲染");
        Debug.Log("   - Pass：GBuffer Pass + Deferred Lighting Pass");
        Debug.Log("   - 优点：支持大量光源");
        Debug.Log("   - 缺点：不支持透明、移动端性能可能不佳");
        Debug.Log("   - 适用：PC端大量光源场景\n");
        
        Debug.Log("📍 HDRP（High Definition Render Pipeline）的渲染路径：\n");
        
        Debug.Log("6. HDRP Forward（前向渲染）");
        Debug.Log("   - 特点：高质量前向渲染");
        Debug.Log("   - 适用：透明物体较多的高质量场景\n");
        
        Debug.Log("7. HDRP Deferred（延迟渲染，默认）");
        Debug.Log("   - 特点：高质量延迟渲染");
        Debug.Log("   - 适用：高端PC、主机的AAA游戏\n");
        
        Debug.Log("==========================================\n");
    }
    
    /// <summary>
    /// 显示当前渲染路径
    /// </summary>
    void ShowCurrentRenderingPath()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("未找到主摄像机");
            return;
        }
        
        Debug.Log("========== 当前渲染设置 ==========\n");
        
        // 检查当前使用的渲染管线
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
        {
            // Built-in 渲染管线
            Debug.Log("当前管线：Built-in Render Pipeline（传统管线）");
            
            // 获取渲染路径
            RenderingPath renderingPath = mainCamera.actualRenderingPath;
            Debug.Log($"摄像机渲染路径：{renderingPath}");
            
            switch (renderingPath)
            {
                case RenderingPath.Forward:
                    Debug.Log("详细说明：");
                    Debug.Log("  - 使用前向渲染");
                    Debug.Log("  - Base Pass：渲染主光源 + 环境光 + 自发光");
                    Debug.Log("  - Additional Pass：每个额外光源一个Pass");
                    Debug.Log("  - 最大像素光源数：" + QualitySettings.pixelLightCount);
                    break;
                    
                case RenderingPath.DeferredShading:
                    Debug.Log("详细说明：");
                    Debug.Log("  - 使用延迟渲染");
                    Debug.Log("  - Deferred Pass：渲染到GBuffer");
                    Debug.Log("  - GBuffer内容：Albedo + Specular + Normal + Emission + Depth");
                    Debug.Log("  - Lighting Pass：从GBuffer计算光照");
                    break;
                    
                case RenderingPath.VertexLit:
                    Debug.Log("详细说明：顶点光照（已废弃）");
                    break;
            }
        }
        else
        {
            // URP 或 HDRP
            Debug.Log($"当前管线：{pipeline.GetType().Name}");
            
            if (pipeline.GetType().Name.Contains("Universal"))
            {
                Debug.Log("渲染类型：URP（Universal Render Pipeline）");
                Debug.Log("渲染路径：Forward（默认）或 Deferred（需要配置）");
                Debug.Log("说明：URP使用单Pass前向渲染，在一个Pass中处理多个光源");
            }
            else if (pipeline.GetType().Name.Contains("HDRenderPipeline"))
            {
                Debug.Log("渲染类型：HDRP（High Definition Render Pipeline）");
                Debug.Log("渲染路径：Deferred（默认）");
            }
        }
        
        Debug.Log("\n==================================\n");
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        GUI.Box(new Rect(10, 10, 300, 120), "");
        
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };
        
        float y = 20;
        
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
        {
            GUI.Label(new Rect(20, y, 280, 25), "管线：Built-in", style);
            y += 25;
            
            GUI.Label(new Rect(20, y, 280, 25), 
                $"路径：{mainCamera.actualRenderingPath}", style);
            y += 25;
            
            if (mainCamera.actualRenderingPath == RenderingPath.Forward)
            {
                GUI.Label(new Rect(20, y, 280, 25), 
                    $"最大像素光：{QualitySettings.pixelLightCount}", style);
            }
        }
        else
        {
            string pipelineName = pipeline.GetType().Name.Contains("Universal") ? "URP" : "HDRP";
            GUI.Label(new Rect(20, y, 280, 25), $"管线：{pipelineName}", style);
            y += 25;
            
            GUI.Label(new Rect(20, y, 280, 25), 
                pipelineName == "URP" ? "路径：Forward/Deferred" : "路径：Deferred", style);
        }
    }
}
