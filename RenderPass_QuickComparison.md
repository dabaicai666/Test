# Unity渲染Pass快速对比表

## 🎯 一图看懂Built-in vs URP

```
Built-in Forward         URP Forward              Built-in Deferred
═══════════════          ═══════════════          ═══════════════════

1. Shadow Pass           1. Setup Pass            1. Shadow Pass
   ↓                        ↓                        ↓
2. ForwardBase          2. Shadow Pass           2. Deferred Pass (GBuffer)
   (主光源)                 ↓                        ↓
   ↓                    3. UniversalForward ⭐    3. Lighting Pass
3. ForwardAdd              (所有光源一起！)           (全屏/Light Volume)
   (光源1) ×N次             ↓                        ↓
   ↓                    4. Skybox                4. Reflections Pass
4. ForwardAdd              ↓                        ↓
   (光源2) ×N次          5. Transparent           5. Skybox
   ↓                       ↓                        ↓
5. ForwardAdd           6. Post-Processing       6. Transparent (Forward)
   (光源3) ×N次             ↓                        ↓
   ↓                    7. UI                    7. Post-Processing
6. Skybox                                           ↓
   ↓                                             8. UI
7. Transparent
   ↓
8. Post-Processing
   ↓
9. UI
```

---

## 📋 完整Pass对照表

| 阶段 | Built-in Forward | Built-in Deferred | URP Forward | URP Deferred |
|-----|-----------------|-------------------|-------------|--------------|
| **CPU准备** | ✅ 剔除+排序 | ✅ 剔除+排序 | ✅ 剔除+排序 | ✅ 剔除+排序 |
| **Setup** | ❌ 无 | ❌ 无 | ✅ **Setup Pass** | ✅ Setup Pass |
| **阴影** | ✅ ShadowCaster | ✅ ShadowCaster | ✅ **ShadowCaster (级联+图集)** | ✅ ShadowCaster |
| **深度预通道** | ⚠️ DepthNormals | ⚠️ 无 | ⚠️ **DepthOnly** | ⚠️ DepthOnly |
| **深度法线** | ⚠️ DepthNormals | ⚠️ 无 | ⚠️ **DepthNormals** | ⚠️ DepthNormals |
| **SSAO** | ❌ 插件 | ❌ 插件 | ⚠️ **内置SSAO** | ⚠️ 内置SSAO |
| **不透明主渲染** | ✅ ForwardBase<br>✅ ForwardAdd ×N | ✅ Deferred (GBuffer)<br>✅ Lighting Pass | ✅ **UniversalForward<br>(单Pass所有光源！)** | ✅ UniversalGBuffer<br>✅ Deferred Lighting |
| **天空盒** | ✅ Skybox | ✅ Skybox | ✅ Skybox | ✅ Skybox |
| **颜色拷贝** | ❌ 无 | ❌ 无 | ⚠️ **Copy Color** | ⚠️ Copy Color |
| **透明渲染** | ✅ ForwardBase<br>✅ ForwardAdd ×N | ✅ ForwardBase<br>✅ ForwardAdd ×N | ✅ **UniversalForward** | ✅ UniversalForward |
| **后处理** | ✅ 多Pass | ✅ 多Pass | ✅ **Uber Shader** | ✅ Uber Shader |
| **UI** | ✅ Canvas | ✅ Canvas | ✅ Canvas | ✅ Canvas |

**图例：**
- ✅ 标准流程
- ⚠️ 可选流程
- ❌ 不支持
- **粗体** = URP的改进点

---

## 🔑 关键Pass详解

### 1. Shadow Pass（阴影Pass）

| 管线 | LightMode | 输出 | 特点 |
|------|-----------|------|------|
| **Built-in** | `"ShadowCaster"` | 每个光源独立ShadowMap | 简单直接 |
| **URP** | `"ShadowCaster"` | _MainLightShadowmapTexture<br>_AdditionalLightsShadowmapTexture | ✅ 级联阴影<br>✅ 阴影图集 |

---

### 2. 不透明物体主渲染Pass

#### Built-in Forward

```hlsl
// Pass 1: ForwardBase（主光源）
Pass
{
    Tags { "LightMode"="ForwardBase" }
    
    // 处理：
    // ✅ 主光源（1个）
    // ✅ 环境光
    // ✅ 光照贴图
    // ✅ 阴影接收
}

// Pass 2: ForwardAdd（附加光源1）
Pass
{
    Tags { "LightMode"="ForwardAdd" }
    Blend One One  // 累加
    ZWrite Off
    
    // 处理：
    // ✅ 点光源1
}

// Pass 3: ForwardAdd（附加光源2）
Pass
{
    Tags { "LightMode"="ForwardAdd" }
    Blend One One
    
    // 处理：
    // ✅ 点光源2
}

// ... 每个附加光源一个Pass
```

**问题：**
- ❌ 10个物体 × 5个光源 = 60个DrawCall
- ❌ 大量状态切换
- ❌ CPU开销大

---

#### URP Forward ⭐ 关键改进

```hlsl
// 只有一个Pass！
Pass
{
    Name "UniversalForward"
    Tags { "LightMode"="UniversalForward" }
    
    // 在Shader内部循环处理所有光源！
    HLSLPROGRAM
    #pragma multi_compile _ _ADDITIONAL_LIGHTS
    
    half4 frag(Varyings input) : SV_Target
    {
        // 1. 主光源
        Light mainLight = GetMainLight();
        half3 color = CalculateLighting(mainLight);
        
        // ⭐ 2. 在Shader中循环处理附加光源
        #ifdef _ADDITIONAL_LIGHTS
            uint lightCount = GetAdditionalLightsCount();
            for (uint i = 0; i < lightCount; ++i)
            {
                Light light = GetAdditionalLight(i);
                color += CalculateLighting(light);
            }
        #endif
        
        return half4(color, 1);
    }
    ENDHLSLPROGRAM
}
```

**优势：**
- ✅ 10个物体 × 所有光源 = 10个DrawCall（光源不增加Pass！）
- ✅ 减少状态切换
- ✅ CPU开销低
- ✅ GPU并行计算光照

---

#### Built-in Deferred

```hlsl
// Pass 1: Deferred（GBuffer）
Pass
{
    Tags { "LightMode"="Deferred" }
    
    // 输出到多个RT
    struct GBufferOutput
    {
        half4 diffuse  : SV_Target0;
        half4 specular : SV_Target1;
        half4 normal   : SV_Target2;
        half4 emission : SV_Target3;
    };
}

// Pass 2: Deferred Lighting（不在Shader中）
// Unity引擎内部的全屏Pass
// 读取GBuffer，应用所有光源
```

**优势：**
- ✅ 光源数量不影响物体DrawCall
- ✅ 适合大量光源

**劣势：**
- ❌ 不支持透明物体
- ❌ 不支持MSAA
- ❌ 显存占用高（GBuffer）

---

### 3. 透明物体渲染Pass

| 管线 | Pass | 特点 |
|------|------|------|
| **Built-in Forward** | ForwardBase + ForwardAdd ×N | ❌ 每个光源一个Pass |
| **Built-in Deferred** | ForwardBase + ForwardAdd ×N | ❌ 回退到Forward |
| **URP Forward** | UniversalForward | ✅ 单Pass处理所有光源 |
| **URP Deferred** | UniversalForward | ✅ 单Pass处理所有光源 |

---

### 4. 后处理Pass

| 管线 | 实现方式 | Pass数量 |
|------|---------|---------|
| **Built-in** | Image Effects | 每个效果一个Pass（5-10个） |
| **Built-in** | Post-Processing Stack v2 | 优化后3-5个Pass |
| **URP** | Volume System + Uber Shader | ✅ **1个Uber Pass** |

**URP优势：**
```hlsl
// URP将所有效果合并到一个Pass
half4 UberPostProcess(float2 uv)
{
    half4 color = tex2D(Source, uv);
    
    // 所有效果在一个Shader中
    color = ApplyBloom(color);        // 不需要额外Pass
    color = ApplyDoF(color);          // 不需要额外Pass
    color = ApplyColorGrading(color); // 不需要额外Pass
    color = ApplyVignette(color);     // 不需要额外Pass
    
    return color;
}
```

---

## 📊 性能对比

### 场景1：10个物体，1个方向光，4个点光源

| 管线 | 阴影 | 主渲染 | 透明 | 总计 |
|------|------|--------|------|------|
| **Built-in Forward** | 10 | 10×(1+4)=50 | 5×(1+4)=25 | **85** |
| **Built-in Deferred** | 10 | 10+5=15 | 5×(1+4)=25 | **50** |
| **URP Forward** | 10 | 10 | 5 | **25** ⭐ |
| **URP Deferred** | 10 | 10+5=15 | 5 | **30** |

**结论：URP Forward在此场景下最优（减少70% DrawCall）**

---

### 场景2：50个物体，1个方向光，20个点光源

| 管线 | 阴影 | 主渲染 | 透明 | 总计 |
|------|------|--------|------|------|
| **Built-in Forward** | 50 | 50×(1+20)=1050 | 10×(1+20)=210 | **1310** ❌ |
| **Built-in Deferred** | 50 | 50+21=71 | 10×(1+20)=210 | **331** |
| **URP Forward** | 50 | 50 | 10 | **110** ⭐ |
| **URP Deferred** | 50 | 50+21=71 | 10 | **131** |

**结论：大量光源时，URP Forward仍然表现最好**

---

## 🎯 选择指南

### 移动平台
```
推荐：URP Forward
原因：
  ✅ 单Pass光照处理
  ✅ SRP Batcher优化
  ✅ 低内存占用
  ✅ 优秀的性能
```

### PC平台（少量光源 ≤8个）
```
推荐：URP Forward
原因：
  ✅ 性能最优
  ✅ 支持所有特性
  ✅ 跨平台一致
```

### PC平台（大量光源 >10个）
```
推荐：URP Deferred 或 Built-in Deferred
原因：
  ✅ 光源数量对DrawCall影响小
  ✅ 适合室内复杂光照场景
```

### 高端PC/主机
```
推荐：HDRP Deferred
原因：
  ✅ 最高画质
  ✅ 最多特性
  ✅ AAA级渲染
```

---

## 🔍 如何查看实际Pass

### 方法1：Frame Debugger（最直观）

```
Window → Analysis → Frame Debugger → Enable

查看Pass的标识：
  Built-in Forward:
    ├─ Shadows.RenderJob
    ├─ RenderForward.RenderLoopJob
    │  ├─ Draw Mesh (LightMode = ForwardBase)
    │  └─ Draw Mesh (LightMode = ForwardAdd) ×N
    └─ ...

  URP:
    ├─ Main Light Shadow
    ├─ Draw Opaques
    │  └─ RenderLoop.Draw (LightMode = UniversalForward)
    └─ ...
```

### 方法2：Profiler

```
Window → Analysis → Profiler

查看 Rendering 部分：
  - SetPass Calls
  - Draw Calls
  - Render.OpaqueGeometry
  - Render.TransparentGeometry
```

### 方法3：使用脚本

```csharp
// 添加 RenderPassVisualizer.cs 到场景
// 按F2切换显示
// 查看实时Pass流程
```

---

## 📚 Shader LightMode 对照表

| LightMode | Built-in | URP | 作用 |
|-----------|----------|-----|------|
| `"ShadowCaster"` | ✅ | ✅ | 投射阴影 |
| `"ForwardBase"` | ✅ | ❌ | 主光源渲染 |
| `"ForwardAdd"` | ✅ | ❌ | 附加光源渲染 |
| `"Deferred"` | ✅ | ❌ | GBuffer渲染 |
| `"DepthNormals"` | ✅ | ✅ | 深度法线 |
| `"UniversalForward"` | ❌ | ✅ | **URP主渲染** |
| `"UniversalGBuffer"` | ❌ | ✅ | URP GBuffer |
| `"DepthOnly"` | ❌ | ✅ | 仅深度 |
| `"Meta"` | ✅ | ✅ | 光照烘焙 |

---

## 💡 快速记忆

```
Built-in Forward:
  每个光源 → 一个Pass → 很多DrawCall ❌

URP Forward:
  所有光源 → 一个Pass → 更少DrawCall ✅

Built-in Deferred:
  物体 → GBuffer → 光照Pass → 适合大量光源 ✅

URP = 更快 + 更简洁 + 更适合现代游戏 ⭐
```

---

## ✅ 学习检查清单

- [ ] 理解Built-in Forward的ForwardBase + ForwardAdd
- [ ] 理解URP Forward的单Pass处理所有光源
- [ ] 理解Deferred的GBuffer + Lighting Pass
- [ ] 知道如何使用Frame Debugger查看Pass
- [ ] 知道LightMode标签的作用
- [ ] 理解SRP Batcher的优化原理
- [ ] 能够根据场景选择合适的渲染路径

---

**完整详细文档：** `Unity_Complete_RenderPass_Flow.md`（100+页）

**立即查看：** 添加 `RenderPassVisualizer.cs` 到场景，按F2查看实时Pass流程！
