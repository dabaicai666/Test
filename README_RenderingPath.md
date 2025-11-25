# Unity 渲染路径（Rendering Path）完全指南

## 📚 目录
1. [什么是渲染路径](#什么是渲染路径)
2. [渲染路径的类型](#渲染路径的类型)
3. [各种渲染路径的区别](#各种渲染路径的区别)
4. [在哪里设置](#在哪里设置)
5. [Pass环节详解](#pass环节详解)
6. [实际应用场景](#实际应用场景)

---

## 什么是渲染路径？

**渲染路径（Rendering Path）** 决定了Unity如何计算和渲染光照、阴影和材质。

简单理解：
```
渲染路径 = Unity处理光照的方式
```

**核心问题：**
- 光照在哪个阶段计算？（顶点 / 像素）
- 光照如何组织？（每个光源一个Pass / 所有光源一个Pass）
- 几何和光照如何分离？（一起计算 / 分开计算）

---

## 渲染路径的类型

### 📍 Built-in 渲染管线（传统管线）

#### 1. Forward Rendering（前向渲染）⭐

```
工作流程：
物体 → Base Pass（主光源） → Additional Pass（附加光源1）
                          → Additional Pass（附加光源2）
                          → Additional Pass（附加光源3）
                          → ...
```

**特点：**
- 对每个物体，按光源数量渲染多个Pass
- Base Pass：处理第一个（最亮的）光源 + 环境光 + 自发光
- Additional Pass：每个附加光源一个Pass（逐个累加）

**适用场景：**
- ✅ 移动平台
- ✅ 光源数量少（1-4个）
- ✅ 透明物体多
- ✅ 需要MSAA抗锯齿

**性能：**
```
DrawCall数量 ≈ 物体数 × (1 + 影响该物体的附加光源数)
```

---

#### 2. Deferred Shading（延迟渲染）⭐⭐

```
工作流程：
第一阶段（几何Pass）：
  物体1 → GBuffer（存储：颜色、法线、深度等）
  物体2 → GBuffer
  物体3 → GBuffer
  
第二阶段（光照Pass）：
  GBuffer + 光源1 → 屏幕
  GBuffer + 光源2 → 屏幕（累加）
  GBuffer + 光源3 → 屏幕（累加）
```

**特点：**
- 先渲染所有物体的几何信息到GBuffer
- 再对GBuffer应用所有光照
- 光照计算只在屏幕空间进行

**GBuffer内容：**
```
RT0: Diffuse Color (RGB) + Occlusion (A)
RT1: Specular Color (RGB) + Roughness (A)
RT2: World Normal (RGB) + unused (A)
RT3: Emission + Lighting + Lightmaps (RGB) + unused (A)
Depth+Stencil buffer
```

**适用场景：**
- ✅ PC、主机平台
- ✅ 大量光源（10+）
- ✅ 不透明物体为主
- ⚠️ 不支持透明物体（需要回退到Forward）
- ⚠️ 不支持MSAA（只能用后处理抗锯齿）

**性能：**
```
DrawCall数量 ≈ 物体数 + 光源数（与物体数×光源数无关！）
```

---

#### 3. Vertex Lit（顶点光照）❌ 已废弃

**特点：**
- 在顶点着色器计算光照
- 性能最好，质量最差
- 已不推荐使用

---

### 📍 URP（Universal Render Pipeline）

#### 4. Forward（前向渲染）⭐⭐⭐ 推荐

```
工作流程（改进版）：
物体 → Universal Forward Pass（一个Pass处理所有光源）
       ├─ 主光源（实时光照）
       ├─ 附加光源1-8（实时光照）
       └─ 更多附加光源（Per-Vertex或SH光照）
```

**特点：**
- ✅ **单Pass处理多个光源**（与Built-in Forward不同！）
- 在一个Pass中：主光源 + 最多8个附加光源（可配置）
- 性能优于Built-in Forward

**关键改进：**
```hlsl
// Built-in Forward：多个Pass
Pass "ForwardBase" { }     // 主光源
Pass "ForwardAdd" { }      // 光源1
Pass "ForwardAdd" { }      // 光源2
Pass "ForwardAdd" { }      // 光源3

// URP Forward：单个Pass
Pass "UniversalForward"
{
    // 在一个Pass中处理：
    // - 主光源（Directional Light）
    // - 附加光源0-7（实时计算）
    // - 更多光源（Per-Vertex简化计算）
}
```

**适用场景：**
- ✅ 移动平台（首选）
- ✅ PC平台（通用方案）
- ✅ 光源数量中等（1-8个实时光源）
- ✅ 需要透明物体
- ✅ 跨平台项目

**性能：**
```
DrawCall数量 ≈ 物体数（光源不增加Pass！）
```

---

#### 5. Deferred（延迟渲染）⭐⭐ Unity 2021+

**特点：**
- URP版本的延迟渲染
- 支持大量光源
- 不支持透明物体（自动回退到Forward）

**适用场景：**
- ✅ PC平台
- ✅ 大量光源（10+）
- ⚠️ 移动端不推荐

---

### 📍 HDRP（High Definition Render Pipeline）

#### 6. Forward（前向渲染）

**特点：**
- 高质量前向渲染
- 支持透明、体积雾等高级特性

**适用场景：**
- 高端PC/主机
- 透明物体为主的场景

---

#### 7. Deferred（延迟渲染，默认）⭐⭐⭐

**特点：**
- HDRP的默认渲染路径
- 极高的渲染质量
- 支持大量高级特性

**适用场景：**
- 高端PC/主机
- AAA游戏级别的画质

---

## 各种渲染路径的区别

### 📊 对比表格

| 特性 | Forward (Built-in) | Deferred (Built-in) | Forward (URP) | Deferred (URP) | Deferred (HDRP) |
|------|-------------------|---------------------|---------------|----------------|-----------------|
| **光源数量** | ⚠️ 少（1-4） | ✅ 多（10+） | ✅ 中（8+） | ✅ 多（10+） | ✅ 极多 |
| **透明物体** | ✅ 支持 | ❌ 不支持 | ✅ 支持 | ⚠️ 回退Forward | ⚠️ 回退Forward |
| **MSAA** | ✅ 支持 | ❌ 不支持 | ✅ 支持 | ❌ 不支持 | ❌ 不支持 |
| **移动端** | ✅ 适合 | ❌ 不适合 | ✅ 最适合 | ⚠️ 谨慎 | ❌ 不支持 |
| **PC/主机** | ✅ 适合 | ✅ 适合 | ✅ 适合 | ✅ 适合 | ✅ 最适合 |
| **显存占用** | 低 | 高（GBuffer） | 低 | 高（GBuffer） | 极高 |
| **Pass数量** | 多（每光源） | 中 | 少（单Pass） | 中 | 少 |
| **实现复杂度** | 简单 | 复杂 | 中等 | 复杂 | 极复杂 |

---

### 🔍 详细区别

#### 光照计算时机

```
Forward（前向）：
  顶点着色器 → 片元着色器（计算光照）→ 输出颜色
  
Deferred（延迟）：
  顶点着色器 → 片元着色器（输出到GBuffer）→ 输出几何信息
  ↓
  屏幕空间 → 光照着色器（从GBuffer计算光照）→ 输出颜色
```

#### Pass数量

```
场景：10个物体，5个光源

Forward (Built-in):
  10个物体 × (1 Base Pass + 4 Additional Pass) = 50个DrawCall

Deferred (Built-in):
  10个物体（几何Pass）+ 5个光源（光照Pass）= 15个DrawCall
  
Forward (URP):
  10个物体（单Pass处理所有光源）= 10个DrawCall ✅ 最优！
```

---

## 在哪里设置？

### 🎯 设置位置总结

```
渲染路径的设置有三个层级：
1. 全局设置（Quality Settings）← 默认值
2. 摄像机设置（Camera）← 可覆盖全局
3. Shader Pass设置 ← 指定Pass类型
```

---

### 📍 位置1：Quality Settings（全局设置）

#### Built-in 渲染管线

```
路径：Edit → Project Settings → Quality

在每个质量级别中：
┌────────────────────────────────┐
│ Quality                        │
├────────────────────────────────┤
│ ├─ Very Low                    │
│ ├─ Low                         │
│ ├─ Medium                      │
│ ├─ High      ← 选中这个        │
│ └─ Very High                   │
│                                │
│ Rendering ▼                    │
│   ├─ Rendering Path:           │
│   │   └─ Forward  ⬇️           │  ← 在这里设置
│   │      • Forward             │
│   │      • Deferred            │
│   │      • Vertex Lit           │
│   │                            │
│   ├─ Pixel Light Count: 4     │  ← Forward时有效
│   └─ ...                       │
└────────────────────────────────┘
```

**代码设置：**
```csharp
// 获取当前渲染路径
RenderingPath path = QualitySettings.renderingPath;

// 设置渲染路径
QualitySettings.renderingPath = RenderingPath.Forward;
// 或
QualitySettings.renderingPath = RenderingPath.DeferredShading;

// 设置像素光源数量（Forward时）
QualitySettings.pixelLightCount = 4;
```

---

#### URP 渲染管线

```
路径：选中 URP Asset（通常在Assets/Settings/）

Inspector面板：
┌────────────────────────────────┐
│ Universal Render Pipeline      │
├────────────────────────────────┤
│ General ▼                      │
│   └─ Renderer List:            │
│       ├─ UniversalRenderer     │  ← 点击查看Renderer设置
│       └─ ...                   │
│                                │
│ Quality ▼                      │
│   └─ ...                       │
│                                │
│ Lighting ▼                     │
│   ├─ Main Light:               │
│   │   ├─ Cast Shadows: ✓      │
│   │   └─ Shadow Resolution     │
│   │                            │
│   ├─ Additional Lights:        │
│   │   ├─ Per Object Limit: 8  │  ← 每个物体最大光源数
│   │   ├─ Cast Shadows: ✓      │
│   │   └─ Shadow Resolution     │
│   └─ ...                       │
└────────────────────────────────┘

点击 UniversalRenderer Asset：
┌────────────────────────────────┐
│ Forward Renderer (Universal)   │
├────────────────────────────────┤
│ Rendering Path:                │
│   └─ Forward  ⬇️                │  ← 在这里设置！
│      • Forward                 │
│      • Deferred (需要Unity 2021+)│
└────────────────────────────────┘
```

**代码设置（URP）：**
```csharp
using UnityEngine.Rendering.Universal;

// 获取URP Asset
var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
if (urpAsset != null)
{
    // 注意：URP的渲染路径设置在Renderer中，不能直接通过代码修改
    // 需要在Inspector中手动设置，或使用SerializedObject
}
```

---

### 📍 位置2：Camera（摄像机设置）

#### Built-in 渲染管线

```
选中Scene中的Camera：

Inspector面板：
┌────────────────────────────────┐
│ Camera                         │
├────────────────────────────────┤
│ Rendering ▼                    │
│   ├─ Rendering Path:           │
│   │   └─ Use Graphics Settings ⬇️│  ← 在这里设置
│   │      • Use Graphics Settings │  （使用Quality Settings）
│   │      • Forward              │  （覆盖为Forward）
│   │      • Deferred             │  （覆盖为Deferred）
│   │      • Vertex Lit            │
│   │                            │
│   ├─ Culling Mask              │
│   └─ ...                       │
└────────────────────────────────┘
```

**代码设置：**
```csharp
Camera camera = Camera.main;

// 获取当前实际使用的渲染路径
RenderingPath actualPath = camera.actualRenderingPath;
Debug.Log($"实际渲染路径：{actualPath}");

// 设置摄像机的渲染路径
camera.renderingPath = RenderingPath.Forward;
// 或
camera.renderingPath = RenderingPath.DeferredShading;
// 或
camera.renderingPath = RenderingPath.UsePlayerSettings;  // 使用全局设置
```

#### URP 渲染管线

**注意：URP中摄像机不能单独设置渲染路径！**

```
URP中的渲染路径由 Renderer Asset 统一控制
所有使用该Renderer的摄像机都使用相同的渲染路径
```

如果需要不同的渲染路径：
```
1. 创建多个Renderer Asset（一个Forward，一个Deferred）
2. 在URP Asset中添加这些Renderer
3. 在Camera组件的Renderer设置中选择使用哪个Renderer
```

---

### 📍 位置3：Shader Pass（着色器Pass）

在Shader中，通过**LightMode标签**指定Pass的类型：

#### Built-in 渲染管线

```hlsl
Shader "Custom/ForwardShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        // ⭐ Forward Base Pass（主光源）
        Pass
        {
            Tags { "LightMode"="ForwardBase" }  ← 这里指定
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase  ← 编译Forward变体
            
            // 处理主光源 + 环境光 + 光照贴图
            // ...
            ENDCG
        }
        
        // ⭐ Forward Add Pass（附加光源）
        Pass
        {
            Tags { "LightMode"="ForwardAdd" }  ← 这里指定
            Blend One One  ← 累加混合
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows  ← 编译Forward Add变体
            
            // 处理每个附加光源
            // ...
            ENDCG
        }
    }
}
```

```hlsl
Shader "Custom/DeferredShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        // ⭐ Deferred Pass（输出到GBuffer）
        Pass
        {
            Tags { "LightMode"="Deferred" }  ← 这里指定
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_prepassfinal
            #pragma target 3.0  ← Deferred需要Shader Model 3.0+
            
            // 输出到多个渲染目标（GBuffer）
            struct GBufferOutput
            {
                half4 diffuse  : SV_Target0;  // RT0
                half4 specular : SV_Target1;  // RT1
                half4 normal   : SV_Target2;  // RT2
                half4 emission : SV_Target3;  // RT3
            };
            
            GBufferOutput frag(...)
            {
                // 输出到GBuffer
                // ...
            }
            ENDCG
        }
    }
}
```

#### URP 渲染管线

```hlsl
Shader "Custom/URPShader"
{
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"  ← 指定URP
        }
        
        // ⭐ URP Forward Pass
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }  ← URP的Forward
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ URP的关键编译指令
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // 在一个Pass中处理所有光源
            half4 frag(...) : SV_Target
            {
                // 主光源
                Light mainLight = GetMainLight(...);
                
                // 附加光源（循环处理）
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < pixelLightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, ...);
                    // 累加光照
                }
                
                return finalColor;
            }
            ENDHLSLPROGRAM
        }
        
        // ⭐ URP Deferred Pass（如果使用Deferred）
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }  ← URP的Deferred
            
            HLSLPROGRAM
            // 输出到GBuffer
            // ...
            ENDHLSLPROGRAM
        }
    }
}
```

---

## Pass环节详解

### 🔄 Forward Rendering的Pass流程

#### Built-in Forward

```
完整渲染流程：

1. DepthNormalsTexture Pass（可选，SSAO等需要）
   └─ LightMode = "DepthNormals"

2. ShadowCaster Pass（投射阴影）
   └─ LightMode = "ShadowCaster"

3. ForwardBase Pass（主渲染-主光源）⭐
   └─ LightMode = "ForwardBase"
      ├─ 处理主方向光
      ├─ 处理环境光
      ├─ 处理自发光
      ├─ 处理光照贴图
      └─ 接收阴影

4. ForwardAdd Pass（主渲染-附加光源）⭐ × N次
   └─ LightMode = "ForwardAdd"  ← 每个附加光源执行一次
      ├─ 处理点光源 / 聚光灯
      ├─ Blend One One（累加混合）
      └─ 接收阴影（如果开启）
```

**示例场景：**
```
场景：1个立方体，1个方向光（主光源），3个点光源

渲染Pass顺序：
SetPass 1: ShadowCaster Pass - 渲染阴影
  DrawCall 1: 立方体的阴影

SetPass 2: ForwardBase Pass - 主光源
  DrawCall 2: 立方体（主光源 + 环境光）

SetPass 3: ForwardAdd Pass - 点光源1
  DrawCall 3: 立方体（+点光源1）

SetPass 4: ForwardAdd Pass - 点光源2
  DrawCall 4: 立方体（+点光源2）

SetPass 5: ForwardAdd Pass - 点光源3
  DrawCall 5: 立方体（+点光源3）

总计：5个SetPass，5个DrawCall
```

---

#### URP Forward

```
完整渲染流程：

1. DepthOnly Pass（可选）
   └─ LightMode = "DepthOnly"

2. ShadowCaster Pass（投射阴影）
   └─ LightMode = "ShadowCaster"

3. DepthNormals Pass（可选）
   └─ LightMode = "DepthNormals"

4. UniversalForward Pass（主渲染）⭐ 单Pass！
   └─ LightMode = "UniversalForward"
      ├─ 处理主光源
      ├─ 处理环境光
      ├─ 处理自发光
      ├─ 处理光照贴图
      ├─ 在循环中处理附加光源（0-8个）← 关键！
      └─ 接收阴影
```

**同样的场景：**
```
场景：1个立方体，1个方向光（主光源），3个点光源

渲染Pass顺序：
SetPass 1: ShadowCaster Pass - 渲染阴影
  DrawCall 1: 立方体的阴影

SetPass 2: UniversalForward Pass - 所有光源
  DrawCall 2: 立方体（主光源 + 点光源1 + 点光源2 + 点光源3）
             ↑ 在一个DrawCall中处理所有光源！

总计：2个SetPass，2个DrawCall ✅ 比Built-in效率高！
```

---

### 🔄 Deferred Rendering的Pass流程

#### Built-in Deferred

```
完整渲染流程：

1. ShadowCaster Pass（投射阴影）
   └─ LightMode = "ShadowCaster"

2. Deferred Pass（渲染到GBuffer）⭐
   └─ LightMode = "Deferred"
      ├─ 输出Diffuse到RT0
      ├─ 输出Specular到RT1
      ├─ 输出Normal到RT2
      ├─ 输出Emission到RT3
      └─ 输出Depth到Depth Buffer

3. Deferred Lighting Pass（从GBuffer计算光照）⭐
   └─ 屏幕空间全屏渲染
      ├─ 读取GBuffer
      ├─ 应用主光源
      ├─ 应用所有附加光源（一次性）
      └─ 输出最终颜色

4. Deferred Reflection Pass（反射探针）
   └─ 应用反射探针

5. Forward Pass（透明物体回退到Forward）
   └─ LightMode = "ForwardBase" / "ForwardAdd"
```

**示例场景：**
```
场景：10个不透明物体，1个方向光，5个点光源

渲染Pass顺序：
SetPass 1: ShadowCaster Pass
  DrawCall 1-10: 10个物体的阴影

SetPass 2: Deferred Pass
  DrawCall 11-20: 10个物体渲染到GBuffer

SetPass 3: Deferred Lighting Pass（全屏）
  DrawCall 21: 全屏Quad（应用所有6个光源）
             ↑ 一次DrawCall处理所有光源！

总计：3个SetPass，21个DrawCall

对比Forward：
  阴影：10个DrawCall
  ForwardBase：10个DrawCall
  ForwardAdd：10×5 = 50个DrawCall
  总计：70个DrawCall ❌ 

Deferred优势明显！
```

---

#### URP Deferred

```
完整渲染流程：

1. ShadowCaster Pass
   └─ LightMode = "ShadowCaster"

2. GBuffer Pass（渲染到GBuffer）⭐
   └─ LightMode = "UniversalGBuffer"
      ├─ 输出到多个RT
      └─ 优化的GBuffer格式

3. Deferred Lighting Pass（计算光照）⭐
   └─ 屏幕空间处理
      ├─ 读取GBuffer
      ├─ 应用所有光源
      └─ 输出最终颜色

4. UniversalForward Pass（透明物体）
   └─ LightMode = "UniversalForward"
```

---

## 实际应用场景

### 📱 移动平台

**推荐：URP Forward**

```
配置：
- URP Asset: Forward Rendering
- Main Light: Enabled, Cast Shadows
- Additional Lights: Per Object Limit = 2-4
- Quality: Medium-Low
```

**原因：**
- ✅ 单Pass处理光源，性能最优
- ✅ 显存占用低
- ✅ 支持MSAA抗锯齿
- ✅ 电池续航友好

---

### 💻 PC平台（中等配置）

**推荐：URP Forward 或 Built-in Deferred**

```
配置方案A：URP Forward
- Additional Lights: Per Object Limit = 8
- Shadow Quality: High
- Post Processing: 开启

配置方案B：Built-in Deferred（如果光源多）
- Deferred Shading
- Pixel Light Count: 不限制
```

**选择依据：**
- 光源少（≤8个）→ URP Forward
- 光源多（>8个）→ Deferred

---

### 🎮 PC/主机（高端）

**推荐：HDRP Deferred 或 URP Deferred**

```
配置：HDRP Deferred
- 最高画质设置
- 大量实时光源
- 高级后处理
- Ray Tracing（如果支持）
```

---

### 🎨 特殊场景

#### 大量透明物体（玻璃、水、粒子）

**推荐：Forward（任意管线）**

**原因：** Deferred不支持透明物体

---

#### 大量动态光源（>10个）

**推荐：Deferred**

**原因：** 光源数量对性能影响小

---

#### VR/AR

**推荐：URP Forward + Single Pass Instanced**

**原因：**
- 性能要求极高
- 需要高帧率（90fps+）
- Forward渲染更快

---

## 📋 检查清单

### 确认当前渲染路径

- [ ] 打开 Edit → Project Settings → Quality
- [ ] 查看 Rendering Path 设置
- [ ] 打开 Window → Analysis → Frame Debugger
- [ ] 查看 Pass 的 LightMode 标签
- [ ] 运行游戏，使用 `RenderingPathExplainer` 脚本查看

### 优化渲染路径

- [ ] 根据目标平台选择合适的渲染路径
- [ ] 控制光源数量（Forward: ≤8, Deferred: 不限）
- [ ] 合理使用透明物体（Deferred不友好）
- [ ] 使用合适的抗锯齿方式（Forward: MSAA, Deferred: TAA/FXAA）
- [ ] 测试不同质量级别的性能

---

## 🎓 总结

### 核心要点

1. **渲染路径决定了光照的计算方式**
   - Forward：对每个物体计算光照
   - Deferred：先存储几何信息，再统一计算光照

2. **设置位置有三个层级**
   - Quality Settings（全局）
   - Camera（摄像机）
   - Shader Pass（着色器）

3. **选择依据**
   - 移动平台 → URP Forward
   - PC+少量光源 → URP Forward
   - PC+大量光源 → Deferred
   - 高端平台 → HDRP Deferred

4. **Pass环节**
   - Forward：ForwardBase + ForwardAdd（Built-in）或 UniversalForward（URP）
   - Deferred：Deferred/UniversalGBuffer + Lighting Pass

现在您应该完全理解Unity的渲染路径了！🎉
