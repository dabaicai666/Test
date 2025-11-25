# Unity 2022 URP Shader Tags完全参考

## 📋 目录

1. [SubShader级别Tags](#subshader级别tags)
2. [Pass级别Tags](#pass级别tags)
3. [完整Tags组合示例](#完整tags组合示例)
4. [常见错误和解决方案](#常见错误和解决方案)
5. [快速参考表](#快速参考表)

---

## 🎯 SubShader级别Tags

SubShader的Tags定义在`SubShader { }`块的开头，控制整个Shader的行为。

```shader
SubShader
{
    Tags
    {
        "RenderType" = "Opaque"
        "RenderPipeline" = "UniversalPipeline"
        "Queue" = "Geometry"
        "IgnoreProjector" = "True"
    }
}
```

### 1. RenderType ⭐

**作用：** 标识Shader的渲染类型

**用途：**
- Shader替换（Camera.RenderWithShader）
- 渲染管线识别物体类型
- Frame Debugger分类

**可选值：**

| 值 | 含义 | 用途 |
|---|------|------|
| `"Opaque"` | 不透明 | 标准不透明物体（默认）⭐ |
| `"Transparent"` | 透明 | 使用Alpha混合的透明物体 |
| `"TransparentCutout"` | 透明裁剪 | 使用Alpha Test的物体（树叶、栅栏） |
| `"Background"` | 背景 | 天空盒等背景元素 |
| `"Overlay"` | 叠加 | UI或叠加效果 |
| `"TreeOpaque"` | 树木不透明 | Unity地形树木（不透明部分） |
| `"TreeTransparentCutout"` | 树木透明裁剪 | Unity地形树木（透明部分） |
| `"TreeBillboard"` | 树木公告板 | 远处树木的LOD |
| `"Grass"` | 草地 | 地形草地 |
| `"GrassBillboard"` | 草地公告板 | 远处草地LOD |

**示例：**
```shader
// 不透明物体
Tags { "RenderType" = "Opaque" }

// 透明物体
Tags { "RenderType" = "Transparent" }

// Alpha裁剪（树叶）
Tags { "RenderType" = "TransparentCutout" }
```

---

### 2. RenderPipeline ⭐⭐⭐ 最重要

**作用：** 指定Shader所属的渲染管线

**重要性：** 如果此值不正确，Shader会显示为洋红色错误材质！

**可选值：**

| 值 | 含义 | 使用场景 |
|---|------|---------|
| `"UniversalPipeline"` | URP管线 | **URP项目必须设置**⭐ |
| `"HDRenderPipeline"` | HDRP管线 | HDRP项目 |
| `""` (留空或不写) | Built-in管线 | 传统Built-in项目 |

**示例：**
```shader
// URP Shader（必须！）
Tags { "RenderPipeline" = "UniversalPipeline" }

// HDRP Shader
Tags { "RenderPipeline" = "HDRenderPipeline" }

// Built-in Shader（不写或留空）
Tags { }
```

**错误示例：**
```shader
// ❌ 错误：在URP项目中忘记写RenderPipeline
Tags { "RenderType" = "Opaque" }
// 结果：材质显示为洋红色

// ✅ 正确：
Tags 
{ 
    "RenderType" = "Opaque"
    "RenderPipeline" = "UniversalPipeline"
}
```

---

### 3. Queue ⭐

**作用：** 控制渲染顺序

**原理：** Queue值越小，越先渲染

**预定义队列：**

| 队列名 | 数值 | 用途 | 深度写入 | Alpha混合 |
|-------|------|------|---------|----------|
| `"Background"` | 1000 | 天空盒、背景 | 否 | 否 |
| `"Geometry"` | 2000 | **不透明物体（默认）**⭐ | 是 | 否 |
| `"AlphaTest"` | 2450 | Alpha裁剪（树叶） | 是 | 否 |
| `"Transparent"` | 3000 | **透明物体**⭐ | 否 | 是 |
| `"Overlay"` | 4000 | UI、粒子效果 | 否 | 是 |

**队列偏移：**
```shader
// 在Geometry队列之后+10
"Queue" = "Geometry+10"  // = 2010

// 在Transparent队列之前-50
"Queue" = "Transparent-50"  // = 2950

// 也可以直接写数字
"Queue" = "2500"
```

**示例：**
```shader
// 不透明物体
Tags { "Queue" = "Geometry" }

// 透明物体
Tags { "Queue" = "Transparent" }

// 确保在所有不透明物体之后渲染
Tags { "Queue" = "Geometry+100" }

// 确保最后渲染（UI）
Tags { "Queue" = "Overlay" }
```

**重要规则：**
```
不透明物体：
  ✅ Queue = "Geometry"
  ✅ ZWrite On（写深度）
  ✅ 从前到后排序（Early-Z优化）
  ❌ 不使用Alpha混合

透明物体：
  ✅ Queue = "Transparent"
  ❌ ZWrite Off（不写深度）
  ✅ 从后到前排序（正确混合）
  ✅ 使用Alpha混合
```

---

### 4. IgnoreProjector

**作用：** 控制是否接受Projector组件的投影

**现状：** URP已移除Projector功能，但保留此Tag用于兼容性

**可选值：**
- `"True"` - 忽略投影器
- `"False"` - 接受投影器（默认）

**示例：**
```shader
// 通常设置为True（URP推荐）
Tags { "IgnoreProjector" = "True" }
```

---

### 5. UniversalMaterialType（可选）

**作用：** URP特有，用于材质分类

**可选值：**

| 值 | 含义 |
|---|------|
| `"Lit"` | 标准光照（默认） |
| `"SimpleLit"` | 简化光照 |
| `"Unlit"` | 无光照 |

**示例：**
```shader
// 明确标记为标准光照材质
Tags { "UniversalMaterialType" = "Lit" }
```

---

### 6. PreviewType（可选）

**作用：** 控制材质预览的形状

**可选值：**
- `"Sphere"` - 球体（默认）
- `"Plane"` - 平面
- `"Skybox"` - 天空盒

**示例：**
```shader
// 在Inspector中使用平面预览
Tags { "PreviewType" = "Plane" }
```

---

### 7. CanUseSpriteAtlas（可选）

**作用：** 是否可以用于Sprite图集

**可选值：**
- `"True"` - 可以
- `"False"` - 不可以（默认）

**示例：**
```shader
// 用于UI Sprite
Tags { "CanUseSpriteAtlas" = "True" }
```

---

## 🎯 Pass级别Tags

Pass的Tags定义在每个`Pass { }`块中，控制Pass的执行时机和用途。

```shader
Pass
{
    Name "UniversalForward"
    Tags { "LightMode" = "UniversalForward" }
    
    // Pass内容...
}
```

### LightMode ⭐⭐⭐ 最关键！

**作用：** 告诉渲染管线此Pass在哪个阶段执行

**重要性：** 这是Pass级别最重要的Tag，决定Pass是否被执行！

**URP所有LightMode值：**

| LightMode值 | 执行阶段 | 用途 | 是否必需 |
|------------|---------|------|---------|
| `"UniversalForward"` | 主渲染Pass | **渲染不透明/透明物体，计算所有光照**⭐ | ✅ 必需 |
| `"UniversalForwardOnly"` | 主渲染Pass | 强制使用Forward渲染（即使URP设置为Deferred） | 可选 |
| `"UniversalGBuffer"` | GBuffer Pass | Deferred模式：写入GBuffer | Deferred时必需 |
| `"UniversalDeferred"` | Deferred光照 | Deferred模式：计算光照 | 系统自动 |
| `"ShadowCaster"` | 阴影Pass | **渲染到阴影贴图**⭐ | ✅ 强烈推荐 |
| `"DepthOnly"` | 深度预通道 | 渲染深度纹理（用于SSAO等） | 可选 |
| `"DepthNormals"` | 深度法线预通道 | 渲染深度+法线（用于SSAO等） | 可选 |
| `"Meta"` | 烘焙Pass | 光照贴图烘焙 | 静态物体需要 |
| `"Universal2D"` | 2D渲染 | URP 2D渲染器使用 | 2D项目需要 |
| `"SRPDefaultUnlit"` | 不受光照 | 无光照渲染（如天空盒） | 特殊用途 |

---

### 详细说明：UniversalForward ⭐⭐⭐

**最重要的Pass！**

```shader
Pass
{
    Name "UniversalForward"
    Tags { "LightMode" = "UniversalForward" }
    
    ZWrite On        // 不透明物体：写深度
    Cull Back        // 剔除背面
    
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    
    // 在frag()中计算：
    //   1. 主光源光照
    //   2. 循环计算所有附加光源
    //   3. 环境光和GI
    //   4. 自发光
    
    half4 frag(Varyings input) : SV_Target
    {
        // ⭐ 关键：所有光照在一个Pass中完成
        return UniversalFragmentPBR(inputData, surfaceData);
    }
    ENDHLSLPROGRAM
}
```

**适用于：**
- ✅ 不透明物体（Queue="Geometry"）
- ✅ 透明物体（Queue="Transparent"）
- ✅ Alpha裁剪物体（Queue="AlphaTest"）

**对比Built-in Forward：**
```
Built-in Forward（旧）：
  Pass 1: ForwardBase（主光源）
  Pass 2: ForwardAdd（光源1）
  Pass 3: ForwardAdd（光源2）
  → 3个Pass，多次DrawCall

URP Forward（新）：
  Pass 1: UniversalForward（所有光源）⭐
  → 1个Pass，光源在片元着色器循环处理
```

---

### 详细说明：ShadowCaster ⭐

**用于投射阴影**

```shader
Pass
{
    Name "ShadowCaster"
    Tags { "LightMode" = "ShadowCaster" }
    
    ZWrite On        // 必须写深度
    ZTest LEqual
    ColorMask 0      // ⭐ 不写颜色，只写深度
    
    HLSLPROGRAM
    #pragma vertex ShadowPassVertex
    #pragma fragment ShadowPassFragment
    
    // 只渲染深度到阴影贴图
    // 不需要计算光照
    
    #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
    ENDHLSLPROGRAM
}
```

**重要性：**
- ✅ 如果没有此Pass，物体不会投射阴影
- ✅ 但仍然可以接收阴影（在UniversalForward Pass中）

---

### 详细说明：DepthOnly

**用于深度预通道**

```shader
Pass
{
    Name "DepthOnly"
    Tags { "LightMode" = "DepthOnly" }
    
    ZWrite On
    ColorMask R      // 只写红色通道（深度值）
    
    HLSLPROGRAM
    #pragma vertex DepthOnlyVertex
    #pragma fragment DepthOnlyFragment
    
    // 只渲染深度，不计算光照
    
    #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
    ENDHLSLPROGRAM
}
```

**何时执行：**
- URP Asset → General → Depth Texture = Enabled
- 或：开启SSAO
- 或：自定义RenderFeature需要深度纹理

**优势：**
- 提前写入深度，主渲染时利用Early-Z优化
- 跳过被遮挡像素的片元着色器

---

### 详细说明：Meta

**用于光照贴图烘焙**

```shader
Pass
{
    Name "Meta"
    Tags { "LightMode" = "Meta" }
    
    Cull Off
    
    HLSLPROGRAM
    #pragma vertex UniversalVertexMeta
    #pragma fragment UniversalFragmentMetaLit
    
    // 烘焙系统使用
    // 输出：反照率、自发光等信息
    
    #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
    ENDHLSLPROGRAM
}
```

**重要性：**
- ✅ 静态物体需要此Pass才能正确烘焙
- ❌ 没有此Pass，烘焙的光照贴图会是黑色

---

## 📋 完整Tags组合示例

### 示例1：标准不透明物体 ⭐

```shader
Shader "Custom/StandardOpaque"
{
    SubShader
    {
        // SubShader Tags
        Tags
        {
            "RenderType" = "Opaque"                    // 不透明
            "RenderPipeline" = "UniversalPipeline"     // URP管线
            "Queue" = "Geometry"                       // 不透明队列
            "IgnoreProjector" = "True"                 // 忽略投影器
        }
        
        LOD 300
        
        // Pass 1: 主渲染
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }  // 主渲染Pass
            
            ZWrite On        // 写深度
            Cull Back        // 剔除背面
            
            HLSLPROGRAM
            // ... Shader代码
            ENDHLSLPROGRAM
        }
        
        // Pass 2: 阴影投射
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            // ... 阴影代码
            ENDHLSLPROGRAM
        }
        
        // Pass 3: 深度预通道（可选）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask R
            
            HLSLPROGRAM
            // ... 深度代码
            ENDHLSLPROGRAM
        }
    }
}
```

---

### 示例2：标准透明物体 ⭐

```shader
Shader "Custom/StandardTransparent"
{
    SubShader
    {
        // SubShader Tags
        Tags
        {
            "RenderType" = "Transparent"               // 透明
            "RenderPipeline" = "UniversalPipeline"     // URP管线
            "Queue" = "Transparent"                    // 透明队列⭐
            "IgnoreProjector" = "True"
        }
        
        LOD 300
        
        // Pass 1: 主渲染
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha  // ⭐ Alpha混合
            ZWrite Off                        // ⭐ 不写深度
            Cull Back
            
            HLSLPROGRAM
            // ... Shader代码（和不透明一样）
            ENDHLSLPROGRAM
        }
        
        // Pass 2: 阴影投射（透明物体也可以投影）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            // ... 阴影代码
            ENDHLSLPROGRAM
        }
    }
}
```

---

### 示例3：Alpha裁剪（树叶、栅栏）

```shader
Shader "Custom/AlphaCutout"
{
    Properties
    {
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"         // Alpha裁剪
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"                      // AlphaTest队列
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite On        // ⭐ Alpha裁剪写深度
            Cull Off         // ⭐ 双面渲染（树叶常用）
            
            HLSLPROGRAM
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                // ⭐ Alpha Test：丢弃透明像素
                clip(color.a - _Cutoff);
                
                // ... 光照计算
                return color;
            }
            ENDHLSLPROGRAM
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            Cull Off         // 双面阴影
            ColorMask 0
            
            HLSLPROGRAM
            // 阴影Pass也需要Alpha Test
            ENDHLSLPROGRAM
        }
    }
}
```

---

### 示例4：无光照Shader（Unlit）

```shader
Shader "Custom/Unlit"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }   // ⭐ 无光照模式
            
            HLSLPROGRAM
            half4 frag(Varyings input) : SV_Target
            {
                // 只采样纹理，不计算光照
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                return color * _BaseColor;
            }
            ENDHLSLPROGRAM
        }
        
        // 仍然需要阴影Pass（如果要投影）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            // ...
        }
    }
}
```

---

## ❌ 常见错误和解决方案

### 错误1：材质显示为洋红色

**症状：** 材质在场景中显示为洋红色（错误颜色）

**原因1：** 忘记设置`RenderPipeline`

```shader
// ❌ 错误
SubShader
{
    Tags { "RenderType" = "Opaque" }
}

// ✅ 正确
SubShader
{
    Tags 
    { 
        "RenderType" = "Opaque"
        "RenderPipeline" = "UniversalPipeline"  // ⭐ 必须添加
    }
}
```

**原因2：** `LightMode`写错

```shader
// ❌ 错误
Pass
{
    Tags { "LightMode" = "ForwardBase" }  // Built-in的LightMode
}

// ✅ 正确
Pass
{
    Tags { "LightMode" = "UniversalForward" }  // URP的LightMode
}
```

---

### 错误2：物体不投射阴影

**症状：** 物体不投射阴影，但可以接收阴影

**原因：** 缺少`ShadowCaster` Pass

```shader
// ❌ 错误：只有主渲染Pass
SubShader
{
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }
        // ...
    }
}

// ✅ 正确：添加ShadowCaster Pass
SubShader
{
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }
        // ...
    }
    
    Pass
    {
        Tags { "LightMode" = "ShadowCaster" }  // ⭐ 添加此Pass
        ZWrite On
        ColorMask 0
        // ...
    }
}
```

---

### 错误3：透明物体渲染错误

**症状：** 透明物体显示顺序混乱，或看不到后面的物体

**原因：** Queue或ZWrite设置错误

```shader
// ❌ 错误：Queue是Geometry，但使用Alpha混合
SubShader
{
    Tags 
    { 
        "Queue" = "Geometry"  // ❌ 不透明队列
    }
    
    Pass
    {
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On  // ❌ 透明物体不应该写深度
    }
}

// ✅ 正确：使用Transparent队列和正确设置
SubShader
{
    Tags 
    { 
        "Queue" = "Transparent"  // ✅ 透明队列
    }
    
    Pass
    {
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off  // ✅ 不写深度
    }
}
```

---

### 错误4：烘焙的光照贴图是黑色

**症状：** 静态物体的光照贴图烘焙后全黑

**原因：** 缺少`Meta` Pass

```shader
// ❌ 错误：没有Meta Pass
SubShader
{
    Pass { Tags { "LightMode" = "UniversalForward" } }
    Pass { Tags { "LightMode" = "ShadowCaster" } }
}

// ✅ 正确：添加Meta Pass
SubShader
{
    Pass { Tags { "LightMode" = "UniversalForward" } }
    Pass { Tags { "LightMode" = "ShadowCaster" } }
    
    Pass
    {
        Tags { "LightMode" = "Meta" }  // ⭐ 烘焙需要
        Cull Off
        // ... Meta Pass代码
    }
}
```

---

## 📊 快速参考表

### SubShader Tags速查

| Tag | 不透明物体 | 透明物体 | Alpha裁剪 | 备注 |
|-----|----------|---------|----------|------|
| `RenderType` | `"Opaque"` | `"Transparent"` | `"TransparentCutout"` | 物体类型 |
| `RenderPipeline` | `"UniversalPipeline"` | `"UniversalPipeline"` | `"UniversalPipeline"` | **URP必须⭐** |
| `Queue` | `"Geometry"` | `"Transparent"` | `"AlphaTest"` | 渲染顺序 |
| `IgnoreProjector` | `"True"` | `"True"` | `"True"` | URP推荐 |

---

### Pass LightMode速查

| LightMode | 用途 | 必需性 | 执行时机 |
|-----------|------|--------|---------|
| `"UniversalForward"` | 主渲染+所有光照 | ✅ 必需 | 主渲染Pass |
| `"ShadowCaster"` | 投射阴影 | ⭐ 强烈推荐 | 阴影Pass |
| `"DepthOnly"` | 深度预通道 | 可选 | 开启深度纹理时 |
| `"DepthNormals"` | 深度+法线预通道 | 可选 | SSAO等效果 |
| `"Meta"` | 光照贴图烘焙 | 静态物体需要 | 烘焙时 |
| `"Universal2D"` | 2D渲染 | 2D项目需要 | 2D渲染器 |

---

### 渲染状态速查

| 设置 | 不透明 | 透明 | Alpha裁剪 | 说明 |
|-----|--------|------|----------|------|
| `ZWrite` | `On` | `Off` | `On` | 深度写入 |
| `ZTest` | `LEqual` | `LEqual` | `LEqual` | 深度测试 |
| `Cull` | `Back` | `Back` | `Off` | 背面剔除 |
| `Blend` | 不写 | `SrcAlpha OneMinusSrcAlpha` | 不写 | 混合模式 |
| `ColorMask` | `RGBA` | `RGBA` | `RGBA` | 颜色通道 |

---

## 🎯 最佳实践总结

### 1. 必须设置的Tags ✅

```shader
SubShader
{
    Tags
    {
        "RenderPipeline" = "UniversalPipeline"  // ⭐ 最重要
        "RenderType" = "Opaque"                  // ⭐ 物体类型
        "Queue" = "Geometry"                     // ⭐ 渲染顺序
    }
    
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }  // ⭐ 最重要
    }
}
```

### 2. 推荐的Pass组合 ⭐

**最小配置（基础功能）：**
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }  // 主渲染
```

**标准配置（推荐）：**
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }   // 主渲染
Pass { Tags { "LightMode" = "ShadowCaster" } }       // 阴影
```

**完整配置（所有功能）：**
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }   // 主渲染
Pass { Tags { "LightMode" = "ShadowCaster" } }       // 阴影
Pass { Tags { "LightMode" = "DepthOnly" } }          // 深度
Pass { Tags { "LightMode" = "DepthNormals" } }       // 深度法线
Pass { Tags { "LightMode" = "Meta" } }               // 烘焙
```

### 3. 检查清单 ✓

创建新Shader时，检查：
- [ ] SubShader有`RenderPipeline = "UniversalPipeline"`
- [ ] SubShader有`RenderType`和`Queue`
- [ ] 至少有一个`UniversalForward` Pass
- [ ] 如果需要投影，有`ShadowCaster` Pass
- [ ] 如果是静态物体，有`Meta` Pass
- [ ] 透明物体：`Queue = "Transparent"`, `ZWrite Off`, `Blend`设置正确
- [ ] 使用`CBUFFER_START(UnityPerMaterial)`（SRP Batcher兼容）

---

希望这个完整的Tags参考能帮助您完全理解URP Shader的标签系统！🎉

**相关文档：**
- 📝 完整代码示例：`/workspace/Assets/Shaders/StandardURPShader.shader`
- 📖 URP渲染流程：`/workspace/URP_Detailed_Pass_Flow.md`
- 📚 总索引：`/workspace/README_INDEX.md`
