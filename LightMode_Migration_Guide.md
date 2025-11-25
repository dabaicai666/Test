# Built-in → URP LightMode迁移指南

## 🔄 LightMode对照表

### 完整对照

```
Built-in管线 LightMode          →    URP管线 LightMode
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ForwardBase                     →    UniversalForward ⭐⭐⭐
ForwardAdd                      →    (已废弃，合并到UniversalForward中)
Deferred                        →    UniversalGBuffer
PrepassBase                     →    (URP无此Pass)
PrepassFinal                    →    UniversalDeferred
Vertex                          →    (URP无此Pass)
VertexLMRGBM                    →    (URP无此Pass)
VertexLM                        →    (URP无此Pass)

ShadowCaster                    →    ShadowCaster ✅ 相同

Always                          →    SRPDefaultUnlit
```

---

## 🎯 关键变化详解

### 变化1：ForwardBase + ForwardAdd → UniversalForward ⭐⭐⭐

**Built-in Forward渲染：**
```shader
// 旧方式：需要多个Pass
Pass
{
    Tags { "LightMode" = "ForwardBase" }  // Pass 1: 主光源
    
    CGPROGRAM
    #pragma multi_compile_fwdbase
    
    // 计算：
    //   - 主方向光
    //   - 环境光
    //   - 光照贴图
    ENDCG
}

Pass
{
    Tags { "LightMode" = "ForwardAdd" }  // Pass 2: 第1个附加光源
    Blend One One  // 叠加混合
    
    CGPROGRAM
    #pragma multi_compile_fwdadd
    
    // 计算：
    //   - 1个点光源或聚光灯
    ENDCG
}

Pass
{
    Tags { "LightMode" = "ForwardAdd" }  // Pass 3: 第2个附加光源
    Blend One One
    
    CGPROGRAM
    #pragma multi_compile_fwdadd
    // 计算第2个光源...
    ENDCG
}

// 结果：3个光源 = 3个Pass = 3倍DrawCall ❌
```

**URP Forward渲染：**
```shader
// 新方式：只需要1个Pass ⭐
Pass
{
    Tags { "LightMode" = "UniversalForward" }  // 唯一的主Pass
    
    HLSLPROGRAM
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
    #pragma multi_compile _ _ADDITIONAL_LIGHTS
    
    half4 frag(Varyings input) : SV_Target
    {
        half3 color = 0;
        
        // ⭐ 计算主光源
        Light mainLight = GetMainLight(shadowCoord);
        color += CalculateLighting(mainLight);
        
        // ⭐ 循环计算所有附加光源（在同一个Pass中！）
        uint lightCount = GetAdditionalLightsCount();  // = 2
        for (uint i = 0; i < lightCount; i++)
        {
            Light light = GetAdditionalLight(i, positionWS);
            color += CalculateLighting(light);
        }
        
        // ⭐ 环境光和GI
        color += GlobalIllumination();
        
        return half4(color, 1);
    }
    ENDHLSLPROGRAM
}

// 结果：3个光源 = 1个Pass = 1倍DrawCall ✅
```

**核心区别：**
```
Built-in:
  光源1 → ForwardBase Pass
  光源2 → ForwardAdd Pass  } 每个光源一个Pass
  光源3 → ForwardAdd Pass  }

URP:
  所有光源 → UniversalForward Pass（内部循环处理）⭐
```

---

### 变化2：Deferred → UniversalGBuffer + UniversalDeferred

**Built-in Deferred渲染：**
```shader
// Pass 1: GBuffer Pass
Pass
{
    Tags { "LightMode" = "Deferred" }
    
    // 输出到GBuffer：
    //   RT0: Albedo + Occlusion
    //   RT1: Specular + Smoothness
    //   RT2: Normal
    //   RT3: Emission + Lighting
}

// Pass 2: 光照Pass（Unity自动添加，不在Shader中）
// 读取GBuffer，计算所有光照
```

**URP Deferred渲染：**
```shader
// Pass 1: GBuffer Pass
Pass
{
    Tags { "LightMode" = "UniversalGBuffer" }  // ⭐ 新名称
    
    // 输出到GBuffer：
    //   RT0: Albedo + MaterialFlags
    //   RT1: Specular + Occlusion
    //   RT2: Normal + Smoothness
    //   RT3: Baked GI + Baked Shadow Mask
}

// Pass 2: Deferred Lighting Pass（Unity自动，系统级别）
// LightMode = "UniversalDeferred"
// 读取GBuffer，计算所有光照
```

**核心区别：**
```
Built-in: "Deferred"
URP:      "UniversalGBuffer"  ← Shader中写这个
          "UniversalDeferred" ← 系统自动调用，不需要写在Shader中
```

---

### 变化3：顶点光照Pass（已废弃）

**Built-in：** 有专门的顶点光照Pass
```shader
Pass
{
    Tags { "LightMode" = "Vertex" }
    // 在顶点着色器计算光照（低质量，高性能）
}
```

**URP：** 通过编译指令控制
```shader
Pass
{
    Tags { "LightMode" = "UniversalForward" }
    
    // 使用编译指令选择顶点光照或像素光照
    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    
    // _ADDITIONAL_LIGHTS_VERTEX → 顶点光照（快）
    // _ADDITIONAL_LIGHTS        → 像素光照（质量高）
}
```

---

## 📋 迁移步骤

### 步骤1：修改SubShader Tags

```shader
// Built-in ❌
SubShader
{
    Tags { "RenderType" = "Opaque" }
}

// URP ✅
SubShader
{
    Tags 
    { 
        "RenderType" = "Opaque"
        "RenderPipeline" = "UniversalPipeline"  // ← 添加这个
    }
}
```

---

### 步骤2：合并ForwardBase和ForwardAdd Pass

**Built-in（旧）：**
```shader
// Pass 1: ForwardBase
Pass
{
    Tags { "LightMode" = "ForwardBase" }
    
    CGPROGRAM
    #pragma multi_compile_fwdbase
    
    fixed4 frag(v2f i) : SV_Target
    {
        // 主光源 + 环境光
        fixed3 color = _LightColor0.rgb * diffuse;
        color += ShadeSH9(half4(i.normal, 1));
        return fixed4(color, 1);
    }
    ENDCG
}

// Pass 2: ForwardAdd
Pass
{
    Tags { "LightMode" = "ForwardAdd" }
    Blend One One
    
    CGPROGRAM
    #pragma multi_compile_fwdadd
    
    fixed4 frag(v2f i) : SV_Target
    {
        // 一个附加光源
        return fixed4(_LightColor0.rgb * diffuse, 1);
    }
    ENDCG
}
```

**URP（新）：**
```shader
// 唯一的Pass（合并！）
Pass
{
    Tags { "LightMode" = "UniversalForward" }  // ← 新名称
    
    HLSLPROGRAM
    // 变体声明
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
    #pragma multi_compile _ _ADDITIONAL_LIGHTS
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    
    half4 frag(Varyings input) : SV_Target
    {
        // ⭐ 准备数据
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalWS = input.normalWS;
        inputData.viewDirectionWS = input.viewDirectionWS;
        inputData.shadowCoord = input.shadowCoord;
        
        SurfaceData surfaceData = (SurfaceData)0;
        surfaceData.albedo = albedo;
        surfaceData.smoothness = _Smoothness;
        // ...
        
        // ⭐ 一个函数完成所有光照！
        return UniversalFragmentPBR(inputData, surfaceData);
        // 内部会：
        //   1. 计算主光源
        //   2. 循环计算所有附加光源
        //   3. 环境光和GI
    }
    ENDHLSLPROGRAM
}
```

---

### 步骤3：修改编译指令

**Built-in编译指令：**
```shader
#pragma multi_compile_fwdbase
#pragma multi_compile_fwdadd
#pragma multi_compile_fwdadd_fullshadows
```

**URP编译指令：**
```shader
// 主光源阴影
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

// 附加光源
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

// 附加光源阴影
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

// 光照贴图
#pragma multi_compile _ LIGHTMAP_ON

// 雾效
#pragma multi_compile_fog

// GPU Instancing
#pragma multi_compile_instancing
```

---

### 步骤4：替换Include文件

**Built-in Includes：**
```shader
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
```

**URP Includes：**
```shader
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
```

---

### 步骤5：替换内置函数

| Built-in函数 | URP函数 | 说明 |
|-------------|---------|------|
| `UnityObjectToClipPos()` | `TransformObjectToHClip()` | 物体空间→裁剪空间 |
| `UnityObjectToWorldDir()` | `TransformObjectToWorldDir()` | 物体空间方向→世界空间 |
| `UnityWorldToClipPos()` | `TransformWorldToHClip()` | 世界空间→裁剪空间 |
| `_WorldSpaceLightPos0` | `GetMainLight().direction` | 主光源方向 |
| `_LightColor0` | `GetMainLight().color` | 主光源颜色 |
| `UNITY_LIGHT_ATTENUATION()` | `mainLight.shadowAttenuation` | 阴影衰减 |
| `ShadeSH9()` | `SampleSH()` | 球谐光照 |
| `UNITY_SAMPLE_TEX2D()` | `SAMPLE_TEXTURE2D()` | 纹理采样 |

---

### 步骤6：SRP Batcher兼容

**Built-in（旧）：**
```shader
// 材质属性随意声明
sampler2D _MainTex;
float4 _MainTex_ST;
fixed4 _Color;
float _Metallic;
```

**URP（新）：**
```shader
// ⭐ 必须用CBUFFER包裹材质属性
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _Color;
    half _Metallic;
CBUFFER_END

// ⭐ 纹理单独声明
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
```

---

## 🔧 完整迁移示例

### Built-in Shader（旧）

```shader
Shader "Custom/OldBuiltinShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        // Pass 1: 主光源
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };
            
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // 主光源
                fixed3 lightDir = _WorldSpaceLightPos0.xyz;
                fixed ndotl = max(0, dot(i.worldNormal, lightDir));
                fixed3 diffuse = _LightColor0.rgb * ndotl;
                
                // 环境光
                fixed3 ambient = ShadeSH9(half4(i.worldNormal, 1));
                
                col.rgb *= (diffuse + ambient);
                return col;
            }
            ENDCG
        }
        
        // Pass 2: 附加光源
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            // ... 省略重复代码
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 计算一个附加光源
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                fixed ndotl = max(0, dot(i.worldNormal, lightDir));
                return fixed4(_LightColor0.rgb * ndotl, 1);
            }
            ENDCG
        }
        
        // Pass 3: 阴影
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ... 阴影代码
            ENDCG
        }
    }
}
```

---

### URP Shader（新）⭐

```shader
Shader "Custom/NewURPShader"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"  // ⭐ 添加这个
        }
        
        // 只需要1个Pass！⭐
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }  // ⭐ 新LightMode
            
            HLSLPROGRAM  // ⭐ HLSL而不是CG
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ 新的编译指令
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            // ⭐ 新的Include
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // ⭐ SRP Batcher兼容
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                // ⭐ 新的变换函数
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // 采样纹理
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                albedo *= _BaseColor;
                
                // ⭐ 准备光照数据
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SAMPLE_GI(0, input.normalWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                
                // ⭐ 一个函数计算所有光照！
                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                
                // 应用雾效
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
                return color;
            }
            ENDHLSLPROGRAM
        }
        
        // 阴影Pass（几乎不变）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSLPROGRAM
        }
    }
}
```

---

## ✅ 迁移检查清单

- [ ] SubShader添加`"RenderPipeline" = "UniversalPipeline"`
- [ ] 将`ForwardBase` + `ForwardAdd`合并为`UniversalForward`
- [ ] `CGPROGRAM` → `HLSLPROGRAM`
- [ ] `ENDCG` → `ENDHLSLPROGRAM`
- [ ] 替换Include文件
- [ ] 替换内置函数
- [ ] 材质属性用`CBUFFER_START(UnityPerMaterial)`包裹
- [ ] 纹理用`TEXTURE2D()`和`SAMPLER()`声明
- [ ] 更新编译指令
- [ ] 删除`Blend One One`（UniversalForward自动处理）
- [ ] 测试光照、阴影、雾效是否正常

---

## 🎯 总结

### 最关键的变化

```
Built-in → URP 最大的变化：

1️⃣ ForwardBase + N个ForwardAdd
   ↓
   1个UniversalForward（内部循环处理所有光源）⭐⭐⭐

2️⃣ CGPROGRAM → HLSLPROGRAM

3️⃣ 材质属性必须用CBUFFER包裹（SRP Batcher）

这三点理解了，迁移就成功90%！
```

---

**相关文档：**
- 📝 完整URP Shader示例：`/workspace/Assets/Shaders/StandardURPShader.shader`
- 📖 Tags详细说明：`/workspace/URP_Shader_Tags_Reference.md`
- ⚡ 快速速查卡：`/workspace/URP_Shader_Quick_Cheatsheet.md`
