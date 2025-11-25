# URP Shader Tags 快速速查卡

## 🎯 最小可用Shader模板（复制即用）

```shader
Shader "Custom/MyURPShader"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"  // ⭐ 必须
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }  // ⭐ 必须
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                return color * _BaseColor;
            }
            ENDHLSLPROGRAM
        }
    }
}
```

---

## 📋 Tags对照表

### SubShader Tags（3个核心）

```
┌─────────────────────────────────────────────────────────────┐
│ Tag名称          │ 不透明物体           │ 透明物体           │
├─────────────────────────────────────────────────────────────┤
│ RenderPipeline   │ "UniversalPipeline" │ "UniversalPipeline"│ ⭐ URP必须
│ RenderType       │ "Opaque"            │ "Transparent"      │
│ Queue            │ "Geometry"          │ "Transparent"      │ ⭐ 决定顺序
└─────────────────────────────────────────────────────────────┘
```

### Pass Tags（1个核心）

```
┌──────────────────────────────────────────────────────────────┐
│ Pass用途         │ LightMode值             │ 是否必需         │
├──────────────────────────────────────────────────────────────┤
│ 主渲染+光照      │ "UniversalForward"      │ ✅ 必需          │ ⭐ 最重要
│ 投射阴影         │ "ShadowCaster"          │ ⭐ 强烈推荐      │
│ 深度预通道       │ "DepthOnly"             │ 可选             │
│ 烘焙光照贴图     │ "Meta"                  │ 静态物体需要     │
└──────────────────────────────────────────────────────────────┘
```

---

## 🎨 三种常见材质类型

### 1️⃣ 不透明物体（最常用）

```shader
SubShader
{
    Tags 
    { 
        "RenderPipeline" = "UniversalPipeline"
        "RenderType" = "Opaque"       // ← 不透明
        "Queue" = "Geometry"          // ← 不透明队列
    }
    
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }
        
        ZWrite On      // ← 写深度
        Cull Back      // ← 剔除背面
        // 不写Blend  // ← 不混合
    }
}
```

### 2️⃣ 透明物体

```shader
SubShader
{
    Tags 
    { 
        "RenderPipeline" = "UniversalPipeline"
        "RenderType" = "Transparent"  // ← 透明
        "Queue" = "Transparent"       // ← 透明队列 ⭐
    }
    
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }
        
        Blend SrcAlpha OneMinusSrcAlpha  // ← Alpha混合 ⭐
        ZWrite Off                        // ← 不写深度 ⭐
        Cull Back
    }
}
```

### 3️⃣ Alpha裁剪（树叶、栅栏）

```shader
SubShader
{
    Tags 
    { 
        "RenderPipeline" = "UniversalPipeline"
        "RenderType" = "TransparentCutout"  // ← Alpha裁剪
        "Queue" = "AlphaTest"               // ← AlphaTest队列
    }
    
    Pass
    {
        Tags { "LightMode" = "UniversalForward" }
        
        ZWrite On      // ← 写深度（和不透明一样）
        Cull Off       // ← 双面渲染 ⭐
        
        HLSLPROGRAM
        half4 frag(Varyings input) : SV_Target
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            clip(color.a - _Cutoff);  // ← Alpha Test ⭐
            return color;
        }
        ENDHLSLPROGRAM
    }
}
```

---

## 🔢 Queue（渲染队列）值

```
数值越小，越先渲染 ↓

┌─────────┬──────┬────────────────────────────┐
│ Queue名 │ 值   │ 用途                       │
├─────────┼──────┼────────────────────────────┤
│ Background │ 1000 │ 天空盒                  │
│ Geometry   │ 2000 │ 不透明物体（默认）⭐    │
│ AlphaTest  │ 2450 │ Alpha裁剪（树叶）       │
│ Transparent│ 3000 │ 透明物体 ⭐             │
│ Overlay    │ 4000 │ UI、叠加效果            │
└─────────┴──────┴────────────────────────────┘

可以加偏移：
  "Geometry+10"   = 2010
  "Transparent-50" = 2950
```

---

## ⚡ 必须记住的5件事

### 1. URP项目必须写的Tag ⭐⭐⭐

```shader
SubShader
{
    Tags { "RenderPipeline" = "UniversalPipeline" }
    //     ↑ 忘记这个 = 洋红色错误材质
}
```

### 2. 主渲染Pass必须的LightMode ⭐⭐⭐

```shader
Pass
{
    Tags { "LightMode" = "UniversalForward" }
    //     ↑ 这个Pass计算所有光照（主光源+附加光源）
}
```

### 3. 透明物体的3个关键设置 ⭐⭐

```shader
Tags { "Queue" = "Transparent" }   // 1. 透明队列
Blend SrcAlpha OneMinusSrcAlpha    // 2. Alpha混合
ZWrite Off                         // 3. 不写深度
```

### 4. 想要投射阴影 = 必须有此Pass ⭐

```shader
Pass
{
    Tags { "LightMode" = "ShadowCaster" }
    ZWrite On
    ColorMask 0  // 只写深度，不写颜色
}
```

### 5. SRP Batcher兼容 = 材质属性用CBUFFER ⭐

```shader
CBUFFER_START(UnityPerMaterial)  // ← 名字必须是这个
    float4 _BaseMap_ST;
    half4 _BaseColor;
    // 所有材质属性都放这里
CBUFFER_END

// 纹理不能放在CBUFFER中
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
```

---

## 🐛 常见错误速查

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 材质是洋红色 | 忘记`RenderPipeline`或`LightMode`错误 | 检查是否写了`"UniversalPipeline"`和`"UniversalForward"` |
| 不投射阴影 | 缺少ShadowCaster Pass | 添加`LightMode="ShadowCaster"`的Pass |
| 透明顺序混乱 | Queue不是Transparent | `Tags { "Queue" = "Transparent" }` |
| 烘焙后全黑 | 缺少Meta Pass | 添加`LightMode="Meta"`的Pass |
| SRP Batcher不工作 | 材质属性不在CBUFFER中 | 用`CBUFFER_START(UnityPerMaterial)`包裹 |

---

## 📖 Include文件速查

```hlsl
// ⭐ 必须包含（顺序很重要）
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// ↑ 包含：基础函数、空间变换、Unity内置变量

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// ↑ 包含：光照函数、GetMainLight()、UniversalFragmentPBR()

// 可选（标准Pass实现）
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
```

---

## 🎯 完整Pass组合推荐

### 最小配置（50KB，基础功能）
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }
```

### 标准配置（80KB，推荐）⭐
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }
Pass { Tags { "LightMode" = "ShadowCaster" } }
```

### 完整配置（150KB，所有功能）
```shader
Pass { Tags { "LightMode" = "UniversalForward" } }
Pass { Tags { "LightMode" = "ShadowCaster" } }
Pass { Tags { "LightMode" = "DepthOnly" } }
Pass { Tags { "LightMode" = "DepthNormals" } }
Pass { Tags { "LightMode" = "Meta" } }
```

---

## ⚙️ 渲染状态速查

```shader
// 深度
ZWrite On/Off           // 是否写入深度缓冲
ZTest Less/LEqual/...   // 深度测试方式

// 剔除
Cull Off/Front/Back     // Off=双面, Back=剔除背面（默认）

// 混合
Blend Off                               // 不混合（不透明）
Blend SrcAlpha OneMinusSrcAlpha        // 标准Alpha混合（透明）
Blend One One                          // 相加混合（发光）

// 颜色通道
ColorMask RGBA          // 写入所有通道（默认）
ColorMask RGB           // 不写Alpha
ColorMask 0             // 不写颜色（阴影Pass用）
```

---

## 🔗 相关资源

- 📝 **完整注释版Shader**：`/workspace/Assets/Shaders/StandardURPShader.shader`
  - 每一行都有详细中文注释
  - 包含所有标准Pass
  - 可直接复制使用

- 📖 **Tags完整文档**：`/workspace/URP_Shader_Tags_Reference.md`
  - 所有Tags的详细说明
  - 完整示例
  - 错误解决方案

- 🎬 **URP渲染流程**：`/workspace/URP_Detailed_Pass_Flow.md`
  - 理解Pass的执行顺序
  - UniversalForward的光照计算
  - 性能优化建议

---

## 💡 一句话总结

```
URP Shader 最核心的2个Tag：
  1. SubShader: "RenderPipeline" = "UniversalPipeline"
  2. Pass: "LightMode" = "UniversalForward"
  
这两个写对了，Shader就能工作！✅
```

---

**打印提示：** 建议打印本页面作为桌面速查卡 📄

**快捷键记忆：**
- `Ctrl+F` 搜索"不透明" → 看第1种材质类型
- `Ctrl+F` 搜索"透明" → 看第2种材质类型
- `Ctrl+F` 搜索"错误" → 看常见错误速查表
