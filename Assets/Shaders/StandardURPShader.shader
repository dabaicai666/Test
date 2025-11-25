// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Unity 2022 标准URP Shader - 完整注释版
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 
// 功能：
//   ✅ 支持SRP Batcher
//   ✅ PBR光照（主光源 + 附加光源）
//   ✅ 阴影接收和投射
//   ✅ 法线贴图
//   ✅ 自发光
//   ✅ 环境光遮蔽
//   ✅ 雾效
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Shader "Custom/StandardURPShader"
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Properties（属性面板）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 这里定义的属性会在Inspector面板中显示，供美术调整
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    Properties
    {
        // ============================================
        // 基础属性
        // ============================================
        
        // 2D = 2D纹理
        // "white" = 默认值（内置纹理）
        // {} = 旧版材质属性选项（Unity 2022已不使用）
        _BaseMap("Base Map (反照率贴图)", 2D) = "white" {}
        
        // Color = 颜色选择器
        // (1,1,1,1) = 默认值RGBA（白色不透明）
        _BaseColor("Base Color (基础颜色)", Color) = (1, 1, 1, 1)
        
        // ============================================
        // 法线贴图
        // ============================================
        
        // "bump" = 默认法线贴图（平面法线）
        _BumpMap("Normal Map (法线贴图)", 2D) = "bump" {}
        
        // Range(min, max) = 滑动条
        // 用于控制法线贴图的强度
        _BumpScale("Normal Scale (法线强度)", Range(0, 2)) = 1.0
        
        // ============================================
        // PBR材质属性
        // ============================================
        
        // 金属度：0=非金属（木头、布料），1=金属（铁、金）
        _Metallic("Metallic (金属度)", Range(0, 1)) = 0.0
        
        // 光滑度：0=粗糙，1=光滑
        // 影响高光反射的锐利程度
        _Smoothness("Smoothness (光滑度)", Range(0, 1)) = 0.5
        
        // ============================================
        // 遮蔽贴图
        // ============================================
        
        // "white" = 默认无遮蔽
        // R通道通常存储AO（环境光遮蔽）
        // G通道可存储其他数据（如粗糙度）
        _OcclusionMap("Occlusion Map (AO贴图)", 2D) = "white" {}
        
        // AO强度：0=无效果，1=完全遮蔽
        _OcclusionStrength("Occlusion Strength (AO强度)", Range(0, 1)) = 1.0
        
        // ============================================
        // 自发光
        // ============================================
        
        // "black" = 默认不发光
        _EmissionMap("Emission Map (自发光贴图)", 2D) = "black" {}
        
        // HDR = 支持高动态范围颜色（可以>1.0，用于发光效果）
        [HDR] _EmissionColor("Emission Color (自发光颜色)", Color) = (0, 0, 0, 0)
        
        // ============================================
        // 渲染设置（用于Shader变体）
        // ============================================
        
        // [Toggle] = 在Inspector中显示为复选框
        // _NORMALMAP = 定义的关键字，用于条件编译
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map (使用法线贴图)", Float) = 1.0
        
        [Toggle(_EMISSION)] _UseEmission("Use Emission (使用自发光)", Float) = 0.0
        
        // ============================================
        // 透明度相关（高级）
        // ============================================
        
        // [Enum] = 下拉菜单
        // UnityEngine.Rendering.CullMode = 引用Unity的枚举类型
        // Off=0, Front=1, Back=2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode (剔除模式)", Float) = 2
        
        // 深度写入：On=1, Off=0
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite (深度写入)", Float) = 1
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // SubShader（子着色器）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Unity会按顺序检查SubShader，选择第一个硬件支持的
    // 现代引擎通常只需要一个SubShader
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    SubShader
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // SubShader级别的Tags（标签）⭐ 重要！
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 这些Tags控制整个Shader的渲染行为
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Tags
        {
            // ============================================
            // "RenderType" - 渲染类型标签 ⭐
            // ============================================
            // 用途：
            //   1. 用于Shader替换（Shader Replacement）
            //   2. 帮助渲染管线识别物体类型
            //   3. Frame Debugger中的分类依据
            //
            // 常见值：
            //   "Opaque"           - 不透明物体（默认）
            //   "Transparent"      - 透明物体
            //   "TransparentCutout" - Alpha裁剪（树叶、栅栏）
            //   "Background"       - 背景物体（天空盒）
            //   "Overlay"          - 叠加物体
            //
            // ⭐ 重要：这个值必须和实际渲染行为匹配
            "RenderType" = "Opaque"
            
            // ============================================
            // "RenderPipeline" - 渲染管线标签 ⭐⭐⭐
            // ============================================
            // 用途：
            //   指定Shader属于哪个渲染管线
            //   如果不匹配，Shader会显示为洋红色错误材质
            //
            // 可选值：
            //   "UniversalPipeline" - URP管线（必须！）
            //   "HDRenderPipeline"  - HDRP管线
            //   ""                  - Built-in管线（不写或留空）
            //
            // ⭐ URP Shader必须写这个！
            "RenderPipeline" = "UniversalPipeline"
            
            // ============================================
            // "Queue" - 渲染队列 ⭐
            // ============================================
            // 用途：
            //   控制物体的渲染顺序
            //   数字越小越先渲染
            //
            // 预定义队列（从小到大）：
            //   "Background"        - 1000  （天空盒等）
            //   "Geometry"          - 2000  （默认，不透明物体）⭐
            //   "AlphaTest"         - 2450  （Alpha裁剪）
            //   "Transparent"       - 3000  （透明物体，从后到前）
            //   "Overlay"           - 4000  （UI等）
            //
            // 可以加偏移：
            //   "Geometry+10"  = 2010（比普通Geometry晚一点）
            //   "Transparent-50" = 2950
            //
            // ⭐ 不透明物体用"Geometry"
            // ⭐ 透明物体用"Transparent"
            "Queue" = "Geometry"
            
            // ============================================
            // "IgnoreProjector" - 忽略投影器（旧功能）
            // ============================================
            // 用途：
            //   控制是否接受Projector组件的投影
            //   URP已移除Projector，但保留兼容性
            //
            // 值：
            //   "True"  - 忽略投影器
            //   "False" - 接受投影器（默认）
            "IgnoreProjector" = "True"
            
            // ============================================
            // "UniversalMaterialType" - URP材质类型（可选）
            // ============================================
            // 用途：
            //   URP特有，用于材质分类和优化
            //
            // 可选值：
            //   "Lit"      - 标准光照材质（默认）
            //   "SimpleLit" - 简化光照
            //   "Unlit"    - 无光照
            // "UniversalMaterialType" = "Lit"
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // LOD（细节层次）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：
        //   根据硬件性能选择不同复杂度的Shader
        //   通过 Shader.globalMaximumLOD 控制
        //
        // 标准值：
        //   300 - 高质量（PBR + 复杂光照）
        //   200 - 中等质量（简化光照）
        //   100 - 低质量（顶点光照）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        LOD 300
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 1: UniversalForward Pass（主渲染Pass）⭐⭐⭐
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 这是最重要的Pass！
        // 用于渲染不透明物体，计算所有光照
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            // ============================================
            // Pass名称
            // ============================================
            // 用途：
            //   1. 在Frame Debugger中显示
            //   2. 可用于通过名称引用此Pass
            //   3. 便于调试和识别
            //
            // 命名规范：
            //   URP标准Pass名称：
            //     - "UniversalForward"（主渲染）
            //     - "ShadowCaster"（阴影投射）
            //     - "DepthOnly"（深度预通道）
            //     - "Meta"（烘焙用）
            Name "UniversalForward"
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Pass级别的Tags ⭐⭐⭐ 最重要！
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 这些Tags决定Pass在渲染管线中的执行时机
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            Tags
            {
                // ============================================
                // "LightMode" - 光照模式 ⭐⭐⭐ 最关键！
                // ============================================
                // 用途：
                //   告诉渲染管线这个Pass在哪个阶段执行
                //   决定Pass能访问哪些光照数据
                //
                // URP常用值：
                //   "UniversalForward"      - 主渲染Pass，处理所有光照 ⭐
                //   "UniversalForwardOnly"  - 仅前向渲染（强制）
                //   "UniversalGBuffer"      - Deferred模式的GBuffer Pass
                //   "UniversalDeferred"     - Deferred光照计算Pass
                //   "ShadowCaster"          - 渲染到阴影贴图
                //   "DepthOnly"             - 仅深度预通道
                //   "DepthNormals"          - 深度+法线预通道
                //   "Meta"                  - 光照贴图烘焙
                //   "Universal2D"           - 2D渲染
                //
                // ⭐ 不透明物体主渲染必须用 "UniversalForward"
                // ⭐ 这个值错了，Pass不会被执行！
                "LightMode" = "UniversalForward"
            }
            
            // ============================================
            // 渲染状态设置
            // ============================================
            // 这些指令控制GPU的渲染行为
            // ============================================
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Cull - 背面剔除
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制剔除哪一面的三角形，提高性能
            //
            // 可选值：
            //   Back  - 剔除背面（默认，最常用）⭐
            //   Front - 剔除正面（用于内部渲染）
            //   Off   - 不剔除（双面渲染，性能消耗大）
            //
            // 判断依据：
            //   顶点顺序为顺时针=正面，逆时针=背面
            //
            // [_Cull] - 引用Properties中定义的_Cull变量
            //          可在Inspector中动态调整
            Cull [_Cull]
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // ZWrite - 深度写入
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制是否将像素的深度写入深度缓冲
            //
            // 可选值：
            //   On  - 写入深度（不透明物体必须）⭐
            //   Off - 不写入深度（透明物体）
            //
            // 重要性：
            //   深度缓冲用于Early-Z优化
            //   先渲染的物体会遮挡后面的物体
            //   不透明物体必须开启，否则会有渲染错误
            ZWrite [_ZWrite]
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // ZTest - 深度测试
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   决定像素是否应该被渲染（基于深度比较）
            //
            // 可选值：
            //   Less      - 深度<当前值（被遮挡的不渲染）
            //   LEqual    - 深度≤当前值（默认）⭐
            //   Greater   - 深度>当前值
            //   GEqual    - 深度≥当前值
            //   Equal     - 深度=当前值
            //   NotEqual  - 深度≠当前值
            //   Always    - 总是通过（忽略深度）
            //   Never     - 从不通过
            //
            // LEqual = 深度小于等于才渲染（正常情况）
            ZTest LEqual
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Blend - 混合模式
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制新像素颜色如何与已有颜色混合
            //
            // 格式：
            //   Blend SrcFactor DstFactor
            //
            // 常用组合：
            //   Off 或不写               - 不混合（不透明）⭐
            //   One Zero                 - 完全覆盖（等同于Off）
            //   SrcAlpha OneMinusSrcAlpha - 标准Alpha混合（透明）
            //   One One                  - 相加混合（发光效果）
            //   SrcAlpha One             - Alpha相加（柔和叠加）
            //
            // 不透明物体不需要混合，所以这里注释掉
            // Blend Off
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // ColorMask - 颜色通道遮罩
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制写入哪些颜色通道
            //
            // 可选值：
            //   RGBA - 写入所有通道（默认）
            //   RGB  - 只写RGB，不写A
            //   R    - 只写红色通道
            //   0    - 不写任何颜色（只写深度，用于阴影Pass）
            //
            // 不透明物体通常写入所有通道
            // ColorMask RGBA
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // HLSLPROGRAM - Shader代码块开始
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Unity 2022使用HLSL语言（High Level Shading Language）
            // 旧版Unity使用CGPROGRAM（已废弃）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            HLSLPROGRAM
            
            // ============================================
            // 编译目标
            // ============================================
            // 用途：
            //   指定Shader编译的最低GPU功能级别
            //
            // 可选值：
            //   2.0 - Shader Model 2.0（非常旧）
            //   2.5 - Shader Model 2.5
            //   3.0 - Shader Model 3.0
            //   3.5 - Shader Model 3.5（移动端）
            //   4.0 - Shader Model 4.0（DX10）
            //   4.5 - Shader Model 4.5（DX11）⭐ 推荐
            //   4.6 - Shader Model 4.6
            //   5.0 - Shader Model 5.0（DX12, Vulkan）
            //
            // ⭐ URP推荐4.5（PC）或3.5（移动端）
            #pragma target 4.5
            
            // ============================================
            // 着色器函数入口
            // ============================================
            // 用途：
            //   指定顶点着色器和片元着色器的函数名
            //
            // 格式：
            //   #pragma vertex 顶点着色器函数名
            //   #pragma fragment 片元着色器函数名
            //
            // ⭐ 必须定义，否则Shader不会编译
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            // ============================================
            // Shader变体关键字（多重编译）⭐ 重要
            // ============================================
            // 用途：
            //   根据不同条件编译不同版本的Shader
            //   只包含实际使用的功能，减少运行时开销
            //
            // 格式：
            //   #pragma multi_compile 关键字1 关键字2 ...
            //   #pragma shader_feature 关键字1 关键字2 ...
            //
            // 区别：
            //   multi_compile: 所有变体都打包到Build中
            //   shader_feature: 只打包材质使用的变体（推荐）
            //
            // ⭐ 下划线(_)表示"不定义任何关键字"的版本
            // ============================================
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 主光源阴影变体
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制主方向光是否有阴影
            //
            // 变体：
            //   _ (无) - 主光源无阴影
            //   _MAIN_LIGHT_SHADOWS - 主光源有阴影
            //   _MAIN_LIGHT_SHADOWS_CASCADE - 级联阴影
            //   _MAIN_LIGHT_SHADOWS_SCREEN - 屏幕空间阴影（移动端）
            //
            // ⭐ multi_compile：因为是全局设置，所有材质共享
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 附加光源变体
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   控制附加光源的计算方式
            //
            // 变体：
            //   _ (无) - 无附加光源
            //   _ADDITIONAL_LIGHTS_VERTEX - 顶点光照（快速）
            //   _ADDITIONAL_LIGHTS - 像素光照（高质量）⭐
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 附加光源阴影变体
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   点光源和聚光灯的阴影
            //
            // multi_compile_fragment：只在片元着色器中编译
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 光照贴图变体（GI）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   支持烘焙的全局光照
            //
            // 变体：
            //   DIRLIGHTMAP_COMBINED - 方向光照贴图
            //   LIGHTMAP_ON - 开启光照贴图
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 动态全局光照（实时GI）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 光照探针（Light Probes）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   动态物体的环境光照
            //
            // 变体：
            //   LIGHTMAP_SHADOW_MIXING - 混合模式
            //   SHADOWS_SHADOWMASK - 阴影遮罩
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 反射探针（Reflection Probes）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 屏幕空间环境光遮蔽（SSAO）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 仅在URP Asset开启SSAO时生效
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Debug显示模式
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 光源Cookie（光源形状纹理）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 光源图层（Light Layers）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile _ _LIGHT_LAYERS
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 前向渲染路径（Forward+ / Clustered）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma multi_compile _ _FORWARD_PLUS
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 雾效变体
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用途：
            //   支持Unity的雾效系统
            //
            // 变体：
            //   FOG_LINEAR - 线性雾
            //   FOG_EXP    - 指数雾
            //   FOG_EXP2   - 指数平方雾
            #pragma multi_compile_fog
            
            // ============================================
            // 材质功能关键字（Shader Feature）⭐
            // ============================================
            // 用途：
            //   根据材质是否使用某功能编译对应代码
            //   只打包实际使用的变体，节省包体
            //
            // 格式：
            //   #pragma shader_feature _KEYWORD
            //   或
            //   #pragma shader_feature_local _KEYWORD
            //
            // 区别：
            //   shader_feature: 全局关键字
            //   shader_feature_local: 本地关键字（推荐）⭐
            //     避免关键字冲突，性能更好
            // ============================================
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 法线贴图
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 如果材质勾选了"Use Normal Map"，定义_NORMALMAP
            #pragma shader_feature_local _NORMALMAP
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 自发光
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma shader_feature_local _EMISSION
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 接收阴影
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
            
            // ============================================
            // GPU Instancing支持 ⭐ 重要优化
            // ============================================
            // 用途：
            //   允许GPU在一次DrawCall中渲染多个相同网格
            //   大幅减少DrawCall数量
            //
            // 要求：
            //   1. 使用相同材质
            //   2. 使用相同网格
            //   3. 材质属性通过CBUFFER定义
            //
            // ⭐ 强烈建议开启
            #pragma multi_compile_instancing
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // GPU Instancing的程序化变体（可选）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 用于Graphics.DrawMeshInstancedIndirect
            // #pragma instancing_options procedural:setup
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // GPU Instancing支持逐实例属性覆盖
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            #pragma instancing_options renderinglayer
            
            // ============================================
            // Dots Instancing（ECS支持，可选）
            // ============================================
            // 用途：
            //   支持Unity DOTS（Data-Oriented Tech Stack）
            //   和ECS（Entity Component System）
            //
            // 如果项目使用DOTS，取消注释
            // #pragma multi_compile _ DOTS_INSTANCING_ON
            
            // ============================================
            // 包含URP Shader库文件 ⭐⭐⭐
            // ============================================
            // 这些文件提供URP所需的所有函数和变量
            // ⭐ 必须包含，顺序也很重要
            // ============================================
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Core.hlsl - 核心函数库
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 包含：
            //   - 基础数学函数
            //   - 空间变换函数（TransformObjectToWorld等）
            //   - 纹理采样宏
            //   - Unity内置变量（_Time, _WorldSpaceCameraPos等）
            //
            // ⭐ 必须第一个包含
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Lighting.hlsl - 光照函数库
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 包含：
            //   - GetMainLight() - 获取主光源
            //   - GetAdditionalLight() - 获取附加光源
            //   - LightingPhysicallyBased() - PBR光照计算
            //   - 阴影采样函数
            //   - BRDF函数
            //
            // ⭐ 必须包含（用于光照计算）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // SurfaceInput.hlsl - 表面输入定义（可选）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 包含标准的纹理和采样器声明
            // 如果使用自定义输入，可以不包含
            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // ShaderVariablesFunctions.hlsl（自动包含）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 包含各种辅助函数
            
            // ============================================
            // CBUFFER - 常量缓冲区 ⭐⭐⭐ SRP Batcher必需
            // ============================================
            // 用途：
            //   定义材质属性的数据布局
            //   GPU可以直接访问，无需CPU传输
            //
            // SRP Batcher要求：
            //   1. 所有材质属性必须在CBUFFER中
            //   2. CBUFFER名称必须是"UnityPerMaterial"
            //   3. 不能在CBUFFER外定义材质属性
            //
            // ⭐ 正确使用CBUFFER是SRP Batcher兼容的关键
            // ============================================
            CBUFFER_START(UnityPerMaterial)
                // 纹理的Tiling和Offset（_ST后缀）
                // float4 = (TilingX, TilingY, OffsetX, OffsetY)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _EmissionMap_ST;
                
                // 基础颜色
                half4 _BaseColor;
                
                // PBR属性
                half _Metallic;
                half _Smoothness;
                
                // 法线强度
                half _BumpScale;
                
                // AO强度
                half _OcclusionStrength;
                
                // 自发光颜色
                half4 _EmissionColor;
            CBUFFER_END
            
            // ============================================
            // 纹理和采样器声明 ⭐
            // ============================================
            // 格式：
            //   TEXTURE2D(纹理名);          - 声明纹理
            //   SAMPLER(sampler纹理名);     - 声明采样器
            //
            // 为什么分开声明：
            //   现代图形API（DX11+, Vulkan）将纹理和采样器分离
            //   可以用不同的采样器采样同一纹理
            //
            // 命名规范：
            //   采样器名 = sampler + 纹理名
            //   例如：_BaseMap → sampler_BaseMap
            //
            // ⭐ 纹理不能放在CBUFFER中
            // ============================================
            
            // 基础贴图
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            // 法线贴图
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            
            // AO贴图
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);
            
            // 自发光贴图
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            
            // ============================================
            // 结构体定义
            // ============================================
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Attributes - 顶点着色器输入
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 从CPU传入的网格数据
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            struct Attributes
            {
                // 顶点位置（对象空间）
                // POSITION = 语义标识符，告诉GPU这是位置数据
                float4 positionOS   : POSITION;
                
                // 顶点法线（对象空间）
                float3 normalOS     : NORMAL;
                
                // 顶点切线（对象空间）
                // float4：xyz=切线方向，w=切线方向（±1）
                // 用于计算副法线和TBN矩阵（法线贴图需要）
                float4 tangentOS    : TANGENT;
                
                // 纹理坐标0（主UV）
                float2 texcoord     : TEXCOORD0;
                
                // 纹理坐标1（光照贴图UV，静态物体烘焙使用）
                float2 staticLightmapUV : TEXCOORD1;
                
                // 纹理坐标2（动态光照贴图UV）
                float2 dynamicLightmapUV : TEXCOORD2;
                
                // GPU Instancing：实例ID
                // 当使用GPU Instancing时，每个实例有唯一ID
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // Varyings - 顶点着色器输出/片元着色器输入
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 从顶点着色器传递到片元着色器的插值数据
            // GPU会自动进行透视正确的插值
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            struct Varyings
            {
                // UV坐标（已应用Tiling/Offset）
                float2 uv           : TEXCOORD0;
                
                // 世界空间位置
                // 用于：
                //   - 计算视线方向
                //   - 采样阴影
                //   - 附加光源衰减计算
                float3 positionWS   : TEXCOORD1;
                
                // 世界空间法线
                // 用于光照计算
                float3 normalWS     : TEXCOORD2;
                
                // 世界空间切线（用于法线贴图）
                // 只在定义了_NORMALMAP时才使用
                #ifdef _NORMALMAP
                    half4 tangentWS : TEXCOORD3;  // xyz: tangent, w: sign
                #endif
                
                // 雾效因子和顶点光照
                // x: 雾效因子（0=完全雾，1=无雾）
                // yzw: 顶点光照颜色（如果开启顶点光照）
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half4 fogFactorAndVertexLight : TEXCOORD4;
                #else
                    half fogFactor : TEXCOORD4;
                #endif
                
                // 阴影坐标
                // 用于采样阴影贴图
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD5;
                #endif
                
                // 光照贴图UV
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 6);
                
                // 动态光照贴图UV
                #ifdef DYNAMICLIGHTMAP_ON
                    float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                
                // 裁剪空间位置（必须！）
                // SV_POSITION = 系统值语义，GPU用于光栅化
                // 必须是最后一个成员
                float4 positionCS   : SV_POSITION;
                
                // GPU Instancing：实例ID传递
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // ============================================
            // 顶点着色器 ⭐
            // ============================================
            // 功能：
            //   1. 变换顶点到裁剪空间（必须）
            //   2. 计算光照所需的数据（法线、切线等）
            //   3. 准备UV坐标
            //   4. 计算阴影坐标
            //   5. 可选：顶点光照计算
            //
            // 执行时机：每个顶点执行一次
            // 性能：通常不是瓶颈（除非顶点数极多）
            // ============================================
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // GPU Instancing设置
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 必须在函数开始调用
                // 设置正确的实例ID，使UNITY_ACCESS_INSTANCED_PROP工作
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 位置变换 ⭐ 核心
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // GetVertexPositionInputs：URP提供的辅助函数
                // 一次性计算多个空间的位置：
                //   - positionWS（世界空间）
                //   - positionVS（视图空间）
                //   - positionCS（裁剪空间）⭐ 必须输出
                //   - positionNDC（标准化设备坐标）
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // 输出裁剪空间位置（GPU光栅化需要）
                output.positionCS = vertexInput.positionCS;
                
                // 输出世界空间位置（片元着色器需要）
                output.positionWS = vertexInput.positionWS;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 法线和切线变换
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // GetVertexNormalInputs：计算TBN矩阵
                //   - normalWS（世界空间法线）
                //   - tangentWS（世界空间切线）
                //   - bitangentWS（世界空间副法线）
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.normalWS = normalInput.normalWS;
                
                // 如果使用法线贴图，传递切线
                #ifdef _NORMALMAP
                    // w分量存储切线方向（用于计算副法线）
                    real sign = input.tangentOS.w * GetOddNegativeScale();
                    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                #endif
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // UV变换
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // TRANSFORM_TEX：应用Tiling和Offset
                // 宏展开：uv * _BaseMap_ST.xy + _BaseMap_ST.zw
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 光照贴图UV
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // OUTPUT_LIGHTMAP_UV：处理静态光照贴图
                // 如果有光照贴图，输出UV；否则输出球谐光照系数
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                
                // 动态光照贴图
                #ifdef DYNAMICLIGHTMAP_ON
                    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                
                // OUTPUT_SH：计算球谐光照（用于动态物体的环境光）
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 顶点光照（可选优化）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 如果附加光源使用顶点光照模式
                // 在顶点着色器中计算光照，片元着色器插值
                // 性能更好，但质量较低
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half3 vertexLight = VertexLighting(output.positionWS, output.normalWS);
                    output.fogFactorAndVertexLight = half4(ComputeFogFactor(vertexInput.positionCS.z), vertexLight);
                #else
                    output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 阴影坐标
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 计算在阴影贴图中的坐标（用于采样阴影）
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                
                return output;
            }
            
            // ============================================
            // 片元着色器 ⭐⭐⭐ 最重要
            // ============================================
            // 功能：
            //   1. 采样纹理
            //   2. 计算表面属性（BRDF）
            //   3. 计算所有光照（主光源+附加光源）⭐
            //   4. 应用全局光照（GI）
            //   5. 应用雾效
            //   6. 输出最终颜色
            //
            // 执行时机：每个像素执行一次
            // 性能：通常是性能瓶颈⭐
            // ============================================
            half4 LitPassFragment(Varyings input) : SV_Target
            {
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // GPU Instancing设置
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 采样纹理 ⭐
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // SAMPLE_TEXTURE2D：宏，展开为对应API的纹理采样
                //   参数1：纹理
                //   参数2：采样器
                //   参数3：UV坐标
                
                // 基础贴图（反照率 Albedo）
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half alpha = albedoAlpha.a * _BaseColor.a;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 法线计算
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half3 normalWS = input.normalWS;
                
                // 如果使用法线贴图
                #ifdef _NORMALMAP
                    // 采样法线贴图
                    half4 normalMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    
                    // UnpackNormalScale：
                    //   解码法线贴图（从[0,1]到[-1,1]）
                    //   应用法线强度_BumpScale
                    half3 normalTS = UnpackNormalScale(normalMap, _BumpScale);
                    
                    // 构建TBN矩阵，变换法线到世界空间
                    half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    half3x3 TBN = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                    normalWS = normalize(mul(normalTS, TBN));
                #endif
                
                normalWS = NormalizeNormalPerPixel(normalWS);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 视线方向
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // GetWorldSpaceNormalizeViewDir：
                //   计算从像素指向摄像机的单位向量
                //   用于高光计算
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 环境光遮蔽（AO）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                occlusion = LerpWhiteTo(occlusion, _OcclusionStrength);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 自发光
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                half3 emission = 0;
                #ifdef _EMISSION
                    emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 准备InputData（光照系统输入）⭐
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                
                // 阴影坐标
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                // 雾效
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    inputData.fogCoord = input.fogFactorAndVertexLight.x;
                    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #else
                    inputData.fogCoord = input.fogFactor;
                    inputData.vertexLighting = 0;
                #endif
                
                // 全局光照（GI）
                #ifdef DYNAMICLIGHTMAP_ON
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif
                
                // 标准化的阴影坐标
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                
                // 阴影遮罩
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                
                // 环境光遮蔽（可能被SSAO覆盖）
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    inputData.bakedGI *= SampleAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                #endif
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 准备SurfaceData（表面属性）⭐
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                SurfaceData surfaceData;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = 0;  // 不使用高光工作流
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = 0;  // 已经在世界空间处理
                surfaceData.emission = emission;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = alpha;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // ⭐⭐⭐ 计算光照（核心！）⭐⭐⭐
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // UniversalFragmentPBR：URP的主光照函数
                //   内部会：
                //     1. 初始化BRDF数据
                //     2. 计算主光源光照
                //     3. 循环计算所有附加光源 ⭐
                //     4. 应用全局光照（GI）
                //     5. 添加自发光
                //
                // ⭐ 这一个函数调用完成所有光照计算！
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 应用雾效
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 输出最终颜色
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                return color;
            }
            
            ENDHLSLPROGRAM
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 2: ShadowCaster Pass（阴影投射）⭐
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：将物体渲染到阴影贴图中
        // 执行时机：阴影Pass阶段
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }  // ⭐ 关键标识
            
            // ============================================
            // 阴影Pass的渲染状态
            // ============================================
            ZWrite On       // 必须写入深度
            ZTest LEqual    // 深度测试
            ColorMask 0     // ⭐ 不写颜色，只写深度
            Cull[_Cull]     // 背面剔除
            
            HLSLPROGRAM
            #pragma target 4.5
            
            // 着色器入口
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            // 变体
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            
            // 包含URP的标准阴影Pass实现
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            
            ENDHLSLPROGRAM
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 3: DepthOnly Pass（深度预通道）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：提前渲染深度，用于SSAO、深度纹理等
        // 执行时机：深度预通道阶段（可选）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }  // ⭐ 关键标识
            
            ZWrite On
            ColorMask R  // 只写红色通道（深度值）
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma target 4.5
            
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            
            ENDHLSLPROGRAM
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 4: DepthNormals Pass（深度法线预通道）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：渲染深度和法线，用于SSAO等效果
        // 执行时机：深度法线预通道阶段（可选）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }  // ⭐ 关键标识
            
            ZWrite On
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma target 4.5
            
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma shader_feature_local _NORMALMAP
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            
            ENDHLSLPROGRAM
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 5: Meta Pass（光照贴图烘焙）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：烘焙光照贴图时使用
        // 执行时机：仅在编辑器烘焙时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }  // ⭐ 关键标识
            
            Cull Off
            
            HLSLPROGRAM
            #pragma target 4.5
            
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            
            ENDHLSLPROGRAM
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pass 6: Universal2D Pass（2D渲染，可选）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 用途：URP的2D渲染器使用
        // 如果项目不使用2D，可以删除此Pass
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }  // ⭐ 2D光照模式
            
            Blend One Zero
            ZWrite [_ZWrite]
            Cull [_Cull]
            
            HLSLPROGRAM
            #pragma target 4.5
            
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            #pragma shader_feature_local _NORMALMAP
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            
            ENDHLSLPROGRAM
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // FallBack（降级Shader）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 用途：
    //   当所有SubShader都不支持时，使用此Shader
    //   主要用于阴影投射Pass的继承
    //
    // 注意：
    //   URP通常不需要FallBack，因为已经包含所有Pass
    //   但为了兼容性，可以设置为隐藏的简单Shader
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // CustomEditor（自定义材质编辑器）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 用途：
    //   在Inspector中使用自定义的材质编辑器GUI
    //   可以添加额外的按钮、分类、帮助信息等
    //
    // URP标准编辑器：
    //   UnityEditor.Rendering.Universal.ShaderGUI.LitShader
    //   提供了标准的材质编辑界面
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
