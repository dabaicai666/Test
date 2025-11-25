# Unity 2022 完整渲染Pass流程详解

## 目录
1. [Built-in 渲染管线完整流程](#builtin-渲染管线完整流程)
2. [URP 渲染管线完整流程](#urp-渲染管线完整流程)
3. [两种管线对比](#两种管线对比)
4. [实际场景示例](#实际场景示例)

---

# Built-in 渲染管线完整流程

## 📋 完整Pass列表（按执行顺序）

```
帧开始
  ↓
1. 剔除阶段（CPU）
  ↓
2. 阴影渲染Pass
  ↓
3. 深度/法线预通道（可选）
  ↓
4. 不透明物体渲染
  ↓
5. 图像效果（Image Effects）- 如果有
  ↓
6. 天空盒渲染
  ↓
7. 透明物体渲染
  ↓
8. 后处理Pass（Post-Processing Stack v2）
  ↓
9. UI渲染
  ↓
帧结束
```

---

## 详细Pass流程

### 🔵 阶段0：CPU端准备

```
[CPU] 视锥体剔除 (Frustum Culling)
  ├─ 输入：场景中所有Renderer
  ├─ 处理：计算AABB与视锥体相交
  └─ 输出：可见物体列表

[CPU] 遮挡剔除 (Occlusion Culling)（如果启用）
  ├─ 输入：可见物体列表
  ├─ 处理：检查是否被其他物体遮挡
  └─ 输出：真正可见的物体列表

[CPU] 排序
  ├─ 不透明物体：按材质、距离排序
  └─ 透明物体：按距离从后到前排序

[CPU] 构建渲染命令
  └─ 输出：CommandBuffer发送到GPU
```

---

### 🟡 阶段1：阴影Pass（Shadow Pass）

#### Pass 1.1: Directional Light Shadow Map

```
LightMode = "ShadowCaster"

目的：为方向光渲染阴影贴图

执行流程：
┌─────────────────────────────────────────┐
│ 对每个方向光的每个级联（Cascade）：      │
│                                         │
│ SetPass: ShadowCaster Pass              │
│   ├─ 设置光源空间的投影矩阵              │
│   ├─ 设置渲染目标为ShadowMap            │
│   └─ 只渲染深度，不渲染颜色              │
│                                         │
│ 对所有投射阴影的物体：                   │
│   ├─ DrawCall 1: 物体1                  │
│   ├─ DrawCall 2: 物体2                  │
│   └─ DrawCall N: 物体N                  │
│                                         │
│ 输出：ShadowMap纹理（深度）              │
└─────────────────────────────────────────┘

伪代码：
for each DirectionalLight:
    for each Cascade (0-3):
        SetRenderTarget(ShadowMapRT, cascade)
        SetViewProjectionMatrix(lightSpaceMatrix)
        
        for each ShadowCaster in scene:
            if InLightFrustum(shadowCaster):
                DrawMesh(shadowCaster, ShadowCasterPass)

输入：
  - 场景中所有Renderer（castShadows = true）
  - 光源的位置和方向

输出：
  - _MainLightShadowmapTexture
  - 分辨率：1024/2048/4096（Quality Settings）
  - 格式：Depth24/32
```

#### Pass 1.2: Spot Light Shadow Map

```
LightMode = "ShadowCaster"

目的：为聚光灯渲染阴影贴图

执行流程：
for each SpotLight (with shadows):
    SetRenderTarget(SpotLightShadowMap)
    SetViewProjectionMatrix(spotLightMatrix)
    
    for each ShadowCaster:
        if InSpotLightFrustum(shadowCaster):
            DrawMesh(shadowCaster, ShadowCasterPass)

输出：
  - 每个聚光灯一张ShadowMap
  - 通常打包到一张纹理图集中
```

#### Pass 1.3: Point Light Shadow Map（立方体贴图）

```
LightMode = "ShadowCaster"

目的：为点光源渲染6个方向的阴影

执行流程：
for each PointLight (with shadows):
    for each CubeFace (6 faces):
        SetRenderTarget(CubeShadowMap, face)
        SetViewProjectionMatrix(pointLightFaceMatrix)
        
        for each ShadowCaster:
            if InFaceFrustum(shadowCaster):
                DrawMesh(shadowCaster, ShadowCasterPass)

输出：
  - CubeMap格式的阴影贴图
  - 6个面，每个面独立渲染
```

---

### 🟢 阶段2：预通道（Pre-Pass）可选

#### Pass 2.1: Depth-Only Pass

```
LightMode = "DepthOnly" 或自定义

目的：提前渲染深度，优化后续渲染

何时执行：
  - 开启SSAO（屏幕空间环境光遮蔽）
  - 开启某些图像效果
  - 手动启用深度预通道

执行流程：
SetRenderTarget(DepthTexture)
ClearDepth()

for each OpaqueObject:
    DrawMesh(object, DepthOnlyPass)
    // 只写深度，ColorMask = 0

输出：
  - _CameraDepthTexture
  - 格式：R32_Float 或 Depth24/32
  
优势：
  - 后续主渲染可以利用Early-Z优化
  - 减少像素着色器的执行次数
```

#### Pass 2.2: Depth-Normals Pass

```
LightMode = "DepthNormals"

目的：渲染深度和法线信息

何时执行：
  - SSAO需要
  - Edge Detection（边缘检测）需要
  - 某些屏幕空间效果需要

执行流程：
SetRenderTarget(DepthNormalsTexture)

for each OpaqueObject:
    DrawMesh(object, DepthNormalsPass)
    // 输出：Depth + ViewSpaceNormal

输出：
  - _CameraDepthNormalsTexture
  - 格式：RG32 或 RGBA32
    - R/G: Normal.xy（压缩后）
    - B/A: Depth（编码后）
```

---

### 🔵 阶段3：不透明物体主渲染（Opaque Rendering）

#### Forward渲染路径

##### Pass 3.1: ForwardBase Pass（主光源）

```
LightMode = "ForwardBase"

目的：渲染不透明物体的主要颜色和光照

执行流程：
SetRenderTarget(CameraColorBuffer, CameraDepthBuffer)

// 按材质分组，减少状态切换
for each UniqueMaterial:
    SetPass(material, ForwardBasePass)
    
    // 渲染使用该材质的所有物体
    for each Object using this material:
        SetTransformMatrix(object.worldMatrix)
        DrawMesh(object.mesh)

着色器中处理的内容：
  ✅ 主方向光（1个）
  ✅ 环境光（Ambient）
  ✅ 光照贴图（Lightmap）
  ✅ 光照探针（Light Probes）
  ✅ 自发光（Emission）
  ✅ 阴影接收（Shadow Receiving）
  ✅ 反射探针（Reflection Probes）

Shader示例：
Pass
{
    Tags { "LightMode"="ForwardBase" }
    
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile_fwdbase  // ⭐ 关键
    
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"
    
    fixed4 frag(v2f i) : SV_Target
    {
        // 1. 采样基础纹理
        fixed4 albedo = tex2D(_MainTex, i.uv);
        
        // 2. 计算主光源
        fixed3 worldNormal = normalize(i.worldNormal);
        fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
        fixed NdotL = max(0, dot(worldNormal, lightDir));
        
        // 3. 主光源漫反射
        fixed3 diffuse = _LightColor0.rgb * albedo.rgb * NdotL;
        
        // 4. 环境光
        fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo.rgb;
        
        // 5. 阴影
        fixed shadow = SHADOW_ATTENUATION(i);
        
        // 6. 合成
        fixed3 color = ambient + diffuse * shadow;
        
        return fixed4(color, albedo.a);
    }
    ENDCG
}

输入：
  - 主摄像机的View/Projection矩阵
  - 主光源的颜色、方向
  - ShadowMap纹理
  - Lightmap纹理（如果有）

输出：
  - CameraColorBuffer（RGB：颜色，A：未使用）
  - CameraDepthBuffer（更新深度）
```

##### Pass 3.2: ForwardAdd Pass（附加光源）

```
LightMode = "ForwardAdd"

目的：为每个附加光源累加光照

重要特性：
  ⭐ 这个Pass会被执行多次（每个附加光源一次）
  ⭐ 使用加法混合（Blend One One）
  ⭐ 不写深度（ZWrite Off）

执行流程：
for each AdditionalLight (PointLight, SpotLight):
    SetPass(material, ForwardAddPass)
    SetLightParameters(light.position, light.color, light.range)
    
    for each Object affected by this light:
        if DistanceToLight(object, light) < light.range:
            DrawMesh(object, ForwardAddPass)

Shader示例：
Pass
{
    Tags { "LightMode"="ForwardAdd" }
    
    // ⭐ 关键设置
    Blend One One        // 加法混合
    ZWrite Off           // 不写深度
    ZTest LEqual         // 深度测试
    
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma multi_compile_fwdadd_fullshadows  // ⭐ 关键
    
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"
    
    fixed4 frag(v2f i) : SV_Target
    {
        fixed4 albedo = tex2D(_MainTex, i.uv);
        fixed3 worldNormal = normalize(i.worldNormal);
        
        // ⭐ 计算光源方向（点光源和聚光灯不同）
        #ifdef USING_DIRECTIONAL_LIGHT
            fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
            fixed atten = 1.0;
        #else
            // 点光源或聚光灯
            fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
            
            // ⭐ 计算衰减（距离 + 聚光灯的角度）
            UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
        #endif
        
        // 计算当前光源的贡献
        fixed NdotL = max(0, dot(worldNormal, lightDir));
        fixed3 diffuse = _LightColor0.rgb * albedo.rgb * NdotL * atten;
        
        return fixed4(diffuse, 0);  // ⭐ Alpha为0（加法混合）
    }
    ENDCG
}

执行次数：
  场景示例：1个立方体，3个点光源
  
  结果：
    ForwardBase Pass: 1次（主光源）
    ForwardAdd Pass: 3次（3个点光源）
    
  总DrawCall = 1 + 3 = 4

像素光源数量限制：
  - Quality Settings → Pixel Light Count
  - 默认值：4
  - 超过限制的光源降级为顶点光照或不渲染
```

---

#### Deferred渲染路径

##### Pass 3.1: Deferred Pass（GBuffer渲染）

```
LightMode = "Deferred"

目的：将物体的几何信息写入GBuffer

执行流程：
SetRenderTarget(GBuffer_RT0, GBuffer_RT1, GBuffer_RT2, GBuffer_RT3, DepthBuffer)
// ⭐ 同时写入多个RenderTarget（MRT）

for each OpaqueObject:
    SetPass(material, DeferredPass)
    DrawMesh(object)

GBuffer结构（Unity Built-in）：
┌─────────────────────────────────────────┐
│ RT0 (ARGB32): Diffuse Color + Occlusion │
│   - RGB: Albedo（基础颜色）              │
│   - A: Occlusion（AO）                   │
├─────────────────────────────────────────┤
│ RT1 (ARGB32): Specular + Smoothness     │
│   - RGB: Specular Color（高光颜色）      │
│   - A: Smoothness（光滑度）              │
├─────────────────────────────────────────┤
│ RT2 (ARGB2101010): World Normal         │
│   - RGB: World Space Normal（法线）      │
│   - A: unused                           │
├─────────────────────────────────────────┤
│ RT3 (ARGBHalf/ARGB2101010): Emission    │
│   - RGB: Emission + GI（自发光+GI）      │
│   - A: unused                           │
├─────────────────────────────────────────┤
│ Depth+Stencil: Camera Depth             │
│   - Depth: 场景深度                      │
│   - Stencil: 标记（光照/无光照）         │
└─────────────────────────────────────────┘

Shader示例：
Pass
{
    Tags { "LightMode"="Deferred" }
    
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma target 3.0  // ⭐ 至少需要Shader Model 3.0
    #pragma multi_compile_prepassfinal
    
    #include "UnityCG.cginc"
    
    // ⭐ 输出到多个RenderTarget
    struct GBufferOutput
    {
        half4 diffuse  : SV_Target0;   // RT0
        half4 specular : SV_Target1;   // RT1
        half4 normal   : SV_Target2;   // RT2
        half4 emission : SV_Target3;   // RT3
    };
    
    GBufferOutput frag(v2f i)
    {
        GBufferOutput o;
        
        // 1. 基础颜色
        half4 albedo = tex2D(_MainTex, i.uv) * _Color;
        o.diffuse = half4(albedo.rgb, 1);  // RT0
        
        // 2. 高光和光滑度
        o.specular = half4(_SpecColor.rgb, _Glossiness);  // RT1
        
        // 3. 世界空间法线（编码到0-1）
        half3 worldNormal = normalize(i.worldNormal);
        o.normal = half4(worldNormal * 0.5 + 0.5, 1);  // RT2
        
        // 4. 自发光
        o.emission = half4(_Emission.rgb, 1);  // RT3
        
        return o;
    }
    ENDCG
}

内存占用：
  1920x1080分辨率：
    RT0: 1920 × 1080 × 4 = 8.3 MB
    RT1: 1920 × 1080 × 4 = 8.3 MB
    RT2: 1920 × 1080 × 4 = 8.3 MB
    RT3: 1920 × 1080 × 8 = 16.6 MB (HDR)
    Depth: 1920 × 1080 × 4 = 8.3 MB
    
  总计：约50 MB
```

##### Pass 3.2: Deferred Lighting Pass（光照计算）

```
目的：从GBuffer计算光照，应用到屏幕

执行流程：
SetRenderTarget(CameraColorBuffer)

// ⭐ 1. 环境光和主光源
DrawFullScreenQuad(DeferredShadingPass)
{
    // 读取GBuffer
    float3 albedo = tex2D(GBuffer_RT0).rgb;
    float3 specular = tex2D(GBuffer_RT1).rgb;
    float3 normal = tex2D(GBuffer_RT2).rgb * 2 - 1;  // 解码
    float depth = tex2D(DepthBuffer).r;
    
    // 重建世界坐标
    float3 worldPos = ReconstructWorldPos(screenUV, depth);
    
    // 计算主光源
    float3 lighting = CalculateDirectionalLight(worldPos, normal, albedo, specular);
    
    // 环境光
    lighting += CalculateAmbient(albedo);
    
    return float4(lighting, 1);
}

// ⭐ 2. 每个附加光源（点光源、聚光灯）
for each AdditionalLight:
    // 只渲染受光源影响的区域（Light Volume）
    if (light.type == PointLight):
        DrawSphereMesh(light.position, light.range, DeferredPointLightPass)
    else if (light.type == SpotLight):
        DrawConeMesh(light.position, light.direction, light.range, DeferredSpotLightPass)

特点：
  ⭐ 光源数量不影响物体DrawCall
  ⭐ 使用Light Volume优化（只处理受影响的像素）
  ⭐ 所有光照在屏幕空间计算

Light Volume优化：
┌────────────────────────────────────────┐
│ 点光源 → 渲染球体Mesh                   │
│   - 半径 = 光源Range                    │
│   - 只处理球体内的像素                  │
│   - 背面剔除（Cull Front）              │
│                                        │
│ 聚光灯 → 渲染锥体Mesh                   │
│   - 底面半径 = tan(angle) × range      │
│   - 只处理锥体内的像素                  │
└────────────────────────────────────────┘

性能优势：
  场景：100个物体，20个点光源
  
  Forward:
    DrawCall = 100 × (1 + 20) = 2100
  
  Deferred:
    DrawCall = 100（GBuffer）+ 1（主光源）+ 20（附加光源）= 121
    
  节省：94% ✅
```

##### Pass 3.3: Deferred Reflections Pass

```
目的：应用反射探针

执行流程：
SetRenderTarget(CameraColorBuffer)
Blend One One  // 累加到已有光照

for each ReflectionProbe:
    DrawProbeVolume(probe, DeferredReflectionPass)
    {
        // 读取GBuffer
        float3 normal = tex2D(GBuffer_RT2).rgb * 2 - 1;
        float3 worldPos = ReconstructWorldPos(screenUV, depth);
        float3 viewDir = normalize(cameraPos - worldPos);
        
        // 计算反射
        float3 reflectDir = reflect(-viewDir, normal);
        float3 reflection = texCUBE(probe.cubemap, reflectDir).rgb;
        
        // 应用菲涅尔和粗糙度
        float fresnel = Fresnel(viewDir, normal);
        float roughness = 1 - smoothness;
        
        return reflection * fresnel * (1 - roughness);
    }
```

---

### 🟣 阶段4：图像效果（Image Effects）可选

```
目的：屏幕空间特效（旧版，已被Post-Processing Stack v2替代）

常见效果：
  - Bloom（辉光）
  - Color Grading（颜色分级）
  - Depth of Field（景深）
  - Motion Blur（运动模糊）

执行流程：
for each ImageEffect (in order):
    SetRenderTarget(TempRT)
    DrawFullScreenQuad(effectShader)
    Blit(TempRT, CameraColorBuffer)

⚠️ 注意：
  - 在Legacy Image Effects中使用
  - 每个效果都是一次全屏Pass
  - 建议使用Post-Processing Stack v2替代
```

---

### 🟠 阶段5：天空盒渲染（Skybox）

```
LightMode = (内置Pass，无LightMode标签)

目的：渲染天空盒背景

执行条件：
  - Camera.clearFlags = Skybox
  - 分配了Skybox材质

执行流程：
SetRenderTarget(CameraColorBuffer, CameraDepthBuffer)
SetDepthTest(LEqual)  // ⭐ 只在没有物体的地方渲染

// 渲染天空盒立方体
DrawMesh(SkyboxCube, SkyboxMaterial)

渲染时机：
  ┌──────────────────────────────────────┐
  │ 不透明物体已经渲染完毕                │
  │ 深度缓冲已经写入                      │
  │ ↓                                    │
  │ 天空盒只渲染在深度为远平面的像素上    │
  │ （即没有物体覆盖的地方）              │
  └──────────────────────────────────────┘

优化：
  - 天空盒通常最后渲染（除了透明物体）
  - 利用深度缓冲，只渲染可见区域
  - 可以用程序化天空盒减少纹理内存
```

---

### 🔴 阶段6：透明物体渲染（Transparent Rendering）

```
目的：渲染半透明物体（玻璃、水、粒子等）

重要特性：
  ⭐ 必须从后到前排序（保证正确的Alpha混合）
  ⭐ 不写入深度缓冲（ZWrite Off）
  ⭐ 使用Alpha混合

执行流程：
// ⭐ 按距离从后到前排序
SortTransparentObjects(camera.position, FarToNear)

for each TransparentObject (sorted):
    // Forward Base Pass
    SetPass(material, ForwardBasePass)
    SetBlending(SrcAlpha, OneMinusSrcAlpha)  // Alpha混合
    DrawMesh(object)
    
    // Forward Add Pass（如果有附加光源）
    for each AdditionalLight:
        SetPass(material, ForwardAddPass)
        DrawMesh(object)

Shader示例：
SubShader
{
    Tags 
    { 
        "Queue"="Transparent"           // ⭐ 渲染队列
        "RenderType"="Transparent" 
        "IgnoreProjector"="True"
    }
    
    Pass
    {
        Tags { "LightMode"="ForwardBase" }
        
        // ⭐ 关键设置
        ZWrite Off                       // 不写深度
        Blend SrcAlpha OneMinusSrcAlpha  // Alpha混合
        
        CGPROGRAM
        // ...
        ENDCG
    }
}

混合模式：
  常见混合：
    Blend SrcAlpha OneMinusSrcAlpha  // 标准Alpha混合
    Blend One One                    // 加法（发光效果）
    Blend OneMinusDstColor One       // 柔和加法
    Blend DstColor Zero              // 乘法（暗化）

⚠️ Deferred渲染路径注意：
  - 透明物体不能使用Deferred
  - 自动回退到Forward渲染
  - 可以接收阴影但不能投射阴影（默认）

性能考虑：
  - Overdraw严重（重叠区域多次绘制）
  - 排序有CPU开销
  - 不能使用Early-Z优化
```

---

### 🟢 阶段7：后处理（Post-Processing Stack v2）

```
目的：应用现代后处理效果栈

常见效果（按顺序）：
  1. Temporal Anti-Aliasing (TAA)
  2. Ambient Occlusion (SSAO)
  3. Screen Space Reflections (SSR)
  4. Depth of Field
  5. Motion Blur
  6. Bloom
  7. Color Grading (LUT)
  8. Chromatic Aberration
  9. Vignette
  10. Grain
  11. Dithering

执行流程：
// ⭐ 使用Uber Shader合并多个效果
SetRenderTarget(TempRT)
DrawFullScreenQuad(PostProcessUberShader)
{
    float4 color = tex2D(SourceRT, uv);
    
    // 按顺序应用效果
    color = ApplyTAA(color, uv);
    color = ApplySSAO(color, uv);
    color = ApplySSR(color, uv);
    color = ApplyDoF(color, uv);
    color = ApplyMotionBlur(color, uv);
    color = ApplyBloom(color, uv);
    color = ApplyColorGrading(color, lut);
    color = ApplyChromaticAberration(color, uv);
    color = ApplyVignette(color, uv);
    color = ApplyGrain(color, uv);
    color = ApplyDithering(color, uv);
    
    return color;
}

Blit(TempRT, FinalRT)

优化策略：
  ✅ 合并多个效果到一个Uber Shader
  ✅ 使用降采样（如Bloom的Mipmap链）
  ✅ 复用RenderTexture
  ✅ 跳过不启用的效果

内存使用：
  临时RT（1920x1080）：
    - 全分辨率RT: 8.3 MB × 2 = 16.6 MB
    - Half分辨率RT: 2.1 MB
    - Quarter分辨率RT: 0.5 MB
    
  总计：约20 MB
```

---

### 🔵 阶段8：UI渲染（Canvas）

```
目的：渲染UI元素

渲染模式：
  1. Screen Space - Overlay（屏幕空间-覆盖）
     - 最后渲染
     - 不受后处理影响
     - 直接绘制到屏幕
     
  2. Screen Space - Camera（屏幕空间-摄像机）
     - 在指定距离渲染
     - 可以被后处理影响
     - 作为透明物体渲染
     
  3. World Space（世界空间）
     - 作为普通3D物体渲染
     - 在透明Pass中渲染

执行流程（Overlay模式）：
SetRenderTarget(ScreenFrameBuffer)
DisableDepthTest()

// 按Canvas的Sort Order和Hierarchy排序
for each Canvas (sorted):
    for each CanvasRenderer:
        if (renderer.cull == false):
            DrawMesh(uiMesh, uiMaterial)

批处理：
  ✅ 相同材质和纹理的UI元素会批处理
  ❌ 不同材质会打断批处理
  ❌ Mask组件会打断批处理

优化建议：
  - 使用Atlas合并UI图片
  - 减少Canvas的Rebuild次数
  - 静态UI使用单独的Canvas
  - 避免频繁修改UI层级
```

---

## 🎯 Built-in管线完整流程图

```
╔════════════════════════════════════════════════════════════════╗
║                     帧开始 (Frame Start)                        ║
╚════════════════════════════════════════════════════════════════╝
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [CPU] 阶段0: 准备阶段                                           │
│   ├─ 视锥体剔除 (Frustum Culling)                              │
│   ├─ 遮挡剔除 (Occlusion Culling)                              │
│   ├─ 排序 (Sorting)                                            │
│   └─ 构建CommandBuffer                                         │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段1: 阴影渲染 (Shadow Pass)                            │
│   ├─ Pass 1.1: Directional Light Shadows                       │
│   │    └─ LightMode="ShadowCaster"                             │
│   │    └─ 输出: _MainLightShadowmapTexture                     │
│   ├─ Pass 1.2: Spot Light Shadows                              │
│   └─ Pass 1.3: Point Light Shadows (CubeMap)                   │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段2: 预通道 (Pre-Pass) 【可选】                        │
│   ├─ Pass 2.1: Depth-Only Pass                                 │
│   │    └─ 输出: _CameraDepthTexture                            │
│   └─ Pass 2.2: Depth-Normals Pass                              │
│        └─ 输出: _CameraDepthNormalsTexture                     │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段3: 不透明物体主渲染                                  │
│                                                                │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 如果是 Forward 渲染路径：                                 │  │
│ │   ├─ Pass 3.1: ForwardBase Pass                          │  │
│ │   │    └─ LightMode="ForwardBase"                        │  │
│ │   │    └─ 处理：主光源 + 环境光 + 光照贴图                │  │
│ │   │                                                       │  │
│ │   └─ Pass 3.2: ForwardAdd Pass (× N次)                   │  │
│ │        └─ LightMode="ForwardAdd"                         │  │
│ │        └─ 每个附加光源执行一次                             │  │
│ │        └─ Blend One One (累加)                           │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                                │
│ ┌──────────────────────────────────────────────────────────┐  │
│ │ 如果是 Deferred 渲染路径：                                │  │
│ │   ├─ Pass 3.1: Deferred Pass (GBuffer)                   │  │
│ │   │    └─ LightMode="Deferred"                           │  │
│ │   │    └─ 输出到GBuffer (RT0~RT3)                        │  │
│ │   │                                                       │  │
│ │   ├─ Pass 3.2: Deferred Lighting Pass                    │  │
│ │   │    └─ 读取GBuffer                                    │  │
│ │   │    └─ 全屏Quad应用所有光源                            │  │
│ │   │                                                       │  │
│ │   └─ Pass 3.3: Deferred Reflections Pass                 │  │
│ │        └─ 应用反射探针                                    │  │
│ └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段4: 图像效果 (Image Effects) 【可选，已过时】         │
│   └─ Legacy Image Effects                                      │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段5: 天空盒渲染 (Skybox)                               │
│   └─ 渲染到深度为远平面的像素                                  │
│   └─ 只在没有物体覆盖的地方绘制                                │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段6: 透明物体渲染 (Transparent)                        │
│   ├─ 按距离从后到前排序 ⭐                                      │
│   ├─ ForwardBase Pass                                          │
│   │    └─ LightMode="ForwardBase"                             │
│   │    └─ ZWrite Off, Alpha Blend                             │
│   │                                                            │
│   └─ ForwardAdd Pass (× N次，每个光源)                         │
│        └─ LightMode="ForwardAdd"                              │
│        └─ Blend One One                                        │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段7: 后处理 (Post-Processing Stack v2)                 │
│   ├─ TAA (Temporal Anti-Aliasing)                              │
│   ├─ SSAO (Ambient Occlusion)                                  │
│   ├─ SSR (Screen Space Reflections)                            │
│   ├─ Depth of Field                                            │
│   ├─ Motion Blur                                               │
│   ├─ Bloom                                                     │
│   ├─ Color Grading (LUT)                                       │
│   ├─ Chromatic Aberration                                      │
│   ├─ Vignette                                                  │
│   ├─ Grain                                                     │
│   └─ Dithering                                                 │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段8: UI渲染 (Canvas - Overlay)                         │
│   └─ 按Sort Order渲染所有Canvas                                │
│   └─ 直接绘制到屏幕，不受后处理影响                            │
└────────────────────────────────────────────────────────────────┘
                              ↓
╔════════════════════════════════════════════════════════════════╗
║                     帧结束 (Frame End)                          ║
║                   Present to Screen                             ║
╚════════════════════════════════════════════════════════════════╝
```

---

# URP 渲染管线完整流程

## 📋 完整Pass列表（按执行顺序）

```
帧开始
  ↓
1. 剔除阶段（CPU）
  ↓
2. Setup Pass（设置Pass）
  ↓
3. 阴影渲染Pass
  ↓
4. 深度预通道（可选）
  ↓
5. 深度法线预通道（可选）
  ↓
6. SSAO Pass（可选）
  ↓
7. 不透明物体渲染
  ↓
8. 天空盒渲染
  ↓
9. 拷贝颜色纹理（可选）
  ↓
10. 透明物体渲染
  ↓
11. 后处理Pass
  ↓
12. UI渲染
  ↓
帧结束
```

---

## 详细Pass流程

### 🔵 阶段0：CPU端准备

```
[CPU] ScriptableRenderContext.Cull()
  ├─ 输入：ScriptableCullingParameters
  ├─ 处理：
  │   ├─ 视锥体剔除
  │   ├─ 遮挡剔除
  │   └─ 光源剔除
  └─ 输出：CullingResults

[CPU] RenderingData构建
  ├─ CameraData（摄像机信息）
  ├─ LightData（光照信息）
  ├─ ShadowData（阴影信息）
  └─ PostProcessingData（后处理信息）

[CPU] 排序
  ├─ 不透明物体：
  │   └─ 主排序：RenderQueue
  │   └─ 次排序：Material ID（SRP Batcher优化）
  │   └─ 三排序：Distance（可选）
  └─ 透明物体：
      └─ 主排序：RenderQueue
      └─ 次排序：Distance（从后到前）
```

---

### 🟡 阶段1：Setup Pass

```
目的：设置全局渲染状态和纹理

UniversalRenderer.Setup()
  ├─ 配置CameraTarget
  ├─ 配置DepthTarget
  ├─ 设置全局Shader变量
  │   ├─ _ScaledScreenParams
  │   ├─ _ScreenParams
  │   ├─ _ZBufferParams
  │   ├─ unity_OrthoParams
  │   └─ ...
  └─ 设置ViewProjection矩阵

SetupLights()
  ├─ 筛选可见光源
  ├─ 排序光源（主光源优先）
  ├─ 限制光源数量（Per Object Limit）
  └─ 设置光照Shader变量
      ├─ _MainLightPosition
      ├─ _MainLightColor
      ├─ _AdditionalLightsCount
      └─ _AdditionalLightsBuffer
```

---

### 🟢 阶段2：阴影Pass（Shadow Pass）

#### Pass 2.1: Main Light Shadow Pass

```
目的：渲染主光源阴影（通常是方向光）

RenderPass: MainLightShadowCasterPass
LightMode = "ShadowCaster"

执行流程：
SetRenderTarget(_MainLightShadowmapTexture)

// ⭐ URP支持级联阴影（Cascaded Shadow Maps）
for each Cascade (0-3):
    SetViewport(cascade.viewport)
    SetViewProjectionMatrix(cascade.matrix)
    ClearDepth(1.0)
    
    for each ShadowCaster in cascade.cullingResults:
        DrawMesh(shadowCaster, ShadowCasterPass)

级联阴影配置（URP Asset）：
┌─────────────────────────────────────┐
│ Main Light → Shadow Cascades:       │
│   ├─ No Cascades (1个)              │
│   ├─ Two Cascades (2个) ⭐          │
│   └─ Four Cascades (4个)            │
│                                     │
│ Cascade Split:                      │
│   └─ [0.067, 0.2, 0.467]  (默认)   │
│                                     │
│ Shadow Distance: 150               │
│ Shadow Resolution: 2048 / 4096     │
└─────────────────────────────────────┘

输出：
  - _MainLightShadowmapTexture
  - 格式：Depth32 或 Depth16
  - 大小：根据Quality设置（1024/2048/4096）
  
优化：
  ✅ 使用SRP Batcher批处理ShadowCaster
  ✅ Shadow Distance限制阴影距离
  ✅ 级联数量越少性能越好
```

#### Pass 2.2: Additional Lights Shadow Pass

```
目的：渲染附加光源阴影（点光源、聚光灯）

RenderPass: AdditionalLightsShadowCasterPass
LightMode = "ShadowCaster"

执行流程：
SetRenderTarget(_AdditionalLightsShadowmapTexture)

// ⭐ URP将多个光源的阴影打包到一张图集
for each AdditionalLight (with shadows):
    if (light.type == SpotLight):
        SetViewport(light.shadowAtlasViewport)
        SetViewProjectionMatrix(light.shadowMatrix)
        
        for each ShadowCaster:
            DrawMesh(shadowCaster, ShadowCasterPass)
    
    else if (light.type == PointLight):
        // 渲染6个面（立方体贴图）
        for each CubeFace (6):
            SetViewport(light.shadowAtlasViewport[face])
            SetViewProjectionMatrix(light.shadowMatrix[face])
            
            for each ShadowCaster:
                DrawMesh(shadowCaster, ShadowCasterPass)

Shadow Atlas布局：
┌─────────────────────────────────────┐
│ _AdditionalLightsShadowmapTexture   │
│ (2048 x 2048，图集)                 │
│                                     │
│ ┌──────┬──────┬──────┬──────┐      │
│ │Light1│Light2│Light3│Light4│      │
│ │ 512² │ 512² │ 512² │ 512² │      │
│ ├──────┼──────┼──────┼──────┤      │
│ │Light5│Light6│Light7│Light8│      │
│ │ 512² │ 512² │ 512² │ 512² │      │
│ └──────┴──────┴──────┴──────┘      │
└─────────────────────────────────────┘

配置（URP Asset）：
  Additional Lights → Cast Shadows: ✓
  Shadow Resolution: 512 / 1024 / 2048
  Shadow Atlas Resolution: 2048 / 4096
```

---

### 🔵 阶段3：深度预通道（Depth Prepass）可选

```
RenderPass: DepthOnlyPass
LightMode = "DepthOnly"

目的：提前渲染深度，优化主渲染Pass

何时执行：
  ✅ SSAO开启
  ✅ Depth Texture开启（URP Asset → General → Depth Texture）
  ✅ 自定义RenderFeature需要深度

执行流程：
SetRenderTarget(_CameraDepthTexture)
ClearDepth(1.0)
ColorMask 0  // ⭐ 不写颜色

for each OpaqueObject:
    SetPass(material, DepthOnlyPass)
    DrawMesh(object)

Shader结构：
Pass
{
    Name "DepthOnly"
    Tags { "LightMode"="DepthOnly" }
    
    ZWrite On
    ColorMask 0  // ⭐ 只写深度
    
    HLSLPROGRAM
    #pragma vertex DepthOnlyVertex
    #pragma fragment DepthOnlyFragment
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    
    Varyings DepthOnlyVertex(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.position.xyz);
        return output;
    }
    
    half4 DepthOnlyFragment(Varyings input) : SV_TARGET
    {
        return 0;  // 不需要返回颜色
    }
    ENDHLSLPROGRAM
}

输出：
  - _CameraDepthTexture
  - 格式：Depth32_Float 或 Depth24Stencil8
  
性能影响：
  ✅ 优势：主渲染Pass可以利用Early-Z，跳过被遮挡的像素
  ❌ 劣势：增加了一次完整的几何Pass
  
  适用场景：
    - 场景复杂，Overdraw严重
    - 像素着色器计算昂贵
```

---

### 🟣 阶段4：深度法线预通道（Depth Normals Prepass）可选

```
RenderPass: DepthNormalsPass
LightMode = "DepthNormals"

目的：渲染深度和视空间法线

何时执行：
  ✅ SSAO开启
  ✅ Depth Normal Texture开启（URP Asset）
  ✅ 自定义RenderFeature需要（如描边）

执行流程：
SetRenderTarget(_CameraDepthNormalsTexture)
Clear(0, 0, 0, 1)

for each OpaqueObject:
    SetPass(material, DepthNormalsPass)
    DrawMesh(object)

Shader结构：
Pass
{
    Name "DepthNormals"
    Tags { "LightMode"="DepthNormals" }
    
    ZWrite On
    
    HLSLPROGRAM
    #pragma vertex DepthNormalsVertex
    #pragma fragment DepthNormalsFragment
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 normalWS : TEXCOORD0;
    };
    
    Varyings DepthNormalsVertex(Attributes input)
    {
        Varyings output;
        
        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
        
        output.positionCS = vertexInput.positionCS;
        output.normalWS = normalInput.normalWS;
        
        return output;
    }
    
    float4 DepthNormalsFragment(Varyings input) : SV_TARGET
    {
        // ⭐ 将世界空间法线转换为视空间
        float3 normalVS = TransformWorldToViewDir(input.normalWS, true);
        
        // 编码法线到0-1范围
        float2 encodedNormal = EncodeNormalOctQuadEncode(normalVS);
        
        // RG: 法线, BA: 未使用
        return float4(encodedNormal, 0, 0);
    }
    ENDHLSLPROGRAM
}

输出：
  - _CameraDepthNormalsTexture
  - 格式：RGBA32 或 RG32
    - RG: 编码的视空间法线
    - BA: 预留（可以存储其他信息）
```

---

### 🟠 阶段5：SSAO Pass（屏幕空间环境光遮蔽）可选

```
RenderPass: ScreenSpaceAmbientOcclusionPass

目的：计算屏幕空间的环境光遮蔽

前置条件：
  - 需要 Depth 纹理
  - 需要 Depth Normals 纹理
  - URP Asset → Renderer → Add Renderer Feature → SSAO

执行流程：
// 1. SSAO计算Pass
SetRenderTarget(_SSAOTexture)
DrawFullScreenQuad(SSAO_Shader)
{
    // 读取深度和法线
    float depth = tex2D(_CameraDepthTexture, uv);
    float3 normal = tex2D(_CameraDepthNormalsTexture, uv);
    
    // 重建世界坐标
    float3 worldPos = ReconstructWorldPosition(uv, depth);
    
    // 采样周围的深度，计算遮蔽
    float ao = 0;
    for (int i = 0; i < SampleCount; i++)
    {
        float3 samplePos = worldPos + RandomHemisphere[i] * Radius;
        float sampleDepth = SampleSceneDepth(samplePos);
        
        if (sampleDepth < samplePos.z)
            ao += 1.0;
    }
    ao = 1.0 - (ao / SampleCount);
    
    return ao;
}

// 2. SSAO模糊Pass
SetRenderTarget(_BlurredSSAOTexture)
DrawFullScreenQuad(SSAO_Blur_Shader)

// 3. 应用到光照
// 在主渲染Pass中读取_BlurredSSAOTexture
// finalColor *= ssaoValue;

输出：
  - _SSAOTexture（原始AO）
  - _BlurredSSAOTexture（模糊后的AO）

性能开销：
  - 2个全屏Pass（SSAO计算 + 模糊）
  - 依赖深度和法线纹理
  - 适合PC平台，移动端谨慎使用
```

---

### 🔴 阶段6：不透明物体主渲染（Opaque Rendering）

#### Forward渲染路径（URP默认）

##### Pass 6.1: UniversalForward Pass

```
RenderPass: DrawOpaqueObjectsPass
LightMode = "UniversalForward"

目的：渲染所有不透明物体（单Pass处理所有光源！）

执行流程：
SetRenderTarget(CameraColorTarget, CameraDepthTarget)

// ⭐ SRP Batcher自动批处理
for each Material:
    SetPass(material, UniversalForwardPass)
    
    for each Object using this material:
        SetPerObjectData(object.worldMatrix, object.properties)
        DrawMesh(object.mesh)

Shader结构（完整示例）：
Shader "Custom/URPLitShader"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }
        
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ URP的编译指令（关键！）
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            
            // ⭐ SRP Batcher要求：属性必须在CBUFFER中
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // ⭐ URP提供的辅助函数
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 基础颜色
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = baseMap * _BaseColor;
                
                // 2. 准备光照计算所需数据
                float3 positionWS = input.positionWS;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetCameraPositionWS() - positionWS);
                
                // ⭐ 3. 创建InputData（URP光照系统需要）
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
                
                // ⭐ 4. 创建SurfaceData（材质属性）
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = albedo.a;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 1.0;
                
                // ⭐ 5. 关键！URP的PBR光照计算
                // 在一个函数中处理：主光源 + 所有附加光源！
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // 6. 应用雾效
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
                return color;
            }
            ENDHLSLPROGRAM
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

UniversalFragmentPBR内部流程：
{
    // 1. 计算主光源
    Light mainLight = GetMainLight(inputData.shadowCoord);
    half3 mainLightColor = LightingPhysicallyBased(
        brdf, mainLight, 
        inputData.normalWS, inputData.viewDirectionWS
    );
    
    // ⭐ 2. 循环处理所有附加光源（关键！）
    #ifdef _ADDITIONAL_LIGHTS
        uint pixelLightCount = GetAdditionalLightsCount();
        
        for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
        {
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
            
            // 计算光照
            half3 additionalLightColor = LightingPhysicallyBased(
                brdf, light,
                inputData.normalWS, inputData.viewDirectionWS
            );
            
            // ⭐ 累加到最终颜色
            color.rgb += additionalLightColor;
        }
    #endif
    
    // 3. 环境光和GI
    color.rgb += GlobalIllumination(brdf, inputData.bakedGI, occlusion, 
                                    inputData.normalWS, inputData.viewDirectionWS);
    
    return color;
}

性能特点：
  ✅ 单Pass处理所有光源（不像Built-in需要ForwardAdd）
  ✅ SRP Batcher优化，大幅减少CPU开销
  ✅ 光源在Shader中循环处理，GPU并行计算
  
  限制：
    - 每个物体最多处理的附加光源数（Per Object Limit，默认8）
    - 超过的光源降级为Per-Vertex或不处理
```

---

#### Deferred渲染路径（URP，Unity 2021+）

##### Pass 6.1: GBuffer Pass

```
RenderPass: DrawGBufferPass
LightMode = "UniversalGBuffer"

目的：将物体的几何信息写入GBuffer

执行流程：
SetRenderTarget(GBuffer0, GBuffer1, GBuffer2, GBuffer3, GBuffer4, DepthBuffer)

for each OpaqueObject:
    SetPass(material, GBufferPass)
    DrawMesh(object)

URP GBuffer结构（优化版）：
┌──────────────────────────────────────────────┐
│ GBuffer0 (RGB10A2): Albedo + MaterialFlags  │
│   - RGB: Albedo (10-bit per channel)        │
│   - A: Material Flags (2-bit)               │
├──────────────────────────────────────────────┤
│ GBuffer1 (RGB10A2): Specular + Occlusion    │
│   - RGB: Specular Color                     │
│   - A: Occlusion                            │
├──────────────────────────────────────────────┤
│ GBuffer2 (RGBA8): Normal                     │
│   - RGB: World Normal (Octahedron编码)      │
│   - A: Smoothness                           │
├──────────────────────────────────────────────┤
│ GBuffer3 (RGBA8): Emission + Lighting       │
│   - RGB: Emission + Baked GI                │
│   - A: unused                               │
├──────────────────────────────────────────────┤
│ GBuffer4 (RGBA16): ShadowMask (可选)        │
│   - RGBA: Shadow Mask channels              │
├──────────────────────────────────────────────┤
│ Depth+Stencil: Camera Depth                 │
│   - Depth: 场景深度                          │
│   - Stencil: 材质标记                        │
└──────────────────────────────────────────────┘

相比Built-in Deferred的优化：
  ✅ 使用RGB10A2格式，减少内存占用
  ✅ 使用Octahedron编码法线，精度更高
  ✅ 支持更多材质变体
```

##### Pass 6.2: Deferred Lighting Pass

```
RenderPass: DeferredLightingPass

目的：从GBuffer计算光照

执行流程：
SetRenderTarget(CameraColorTarget)

// ⭐ 1. 主光源（全屏处理）
DrawFullScreenQuad(DeferredMainLightShader)
{
    // 读取GBuffer
    float3 albedo = tex2D(GBuffer0, uv).rgb;
    float3 specular = tex2D(GBuffer1, uv).rgb;
    float3 normal = DecodeNormal(tex2D(GBuffer2, uv).rgb);
    float smoothness = tex2D(GBuffer2, uv).a;
    float depth = tex2D(DepthBuffer, uv).r;
    
    // 重建世界坐标
    float3 worldPos = ReconstructWorldPosition(uv, depth);
    
    // 计算主光源光照
    Light mainLight = GetMainLight();
    float3 lighting = CalculatePBRLighting(
        albedo, specular, normal, smoothness,
        worldPos, mainLight
    );
    
    return float4(lighting, 1);
}

// ⭐ 2. 附加光源（使用Light Volumes）
Blend One One  // 累加模式

for each AdditionalLight:
    if (light.type == Directional):
        DrawFullScreenQuad(DeferredDirectionalLightShader)
    
    else if (light.type == Point):
        // 只渲染光源影响范围的球体
        DrawSphereMesh(light.position, light.range, DeferredPointLightShader)
    
    else if (light.type == Spot):
        // 只渲染光源影响范围的锥体
        DrawConeMesh(light.position, light.direction, light.range, DeferredSpotLightShader)

性能优势：
  ✅ 光源数量不影响几何DrawCall
  ✅ Light Volume优化，只处理受影响的像素
  ✅ 所有光照在屏幕空间统一计算
```

---

### 🟢 阶段7：天空盒渲染（Skybox）

```
RenderPass: DrawSkyboxPass

目的：渲染天空盒

执行条件：
  - Camera.clearFlags = Skybox
  - 分配了Skybox材质

执行流程：
SetRenderTarget(CameraColorTarget, CameraDepthTarget)
SetDepthTest(LEqual)  // 只渲染深度为远平面的像素

Context.DrawSkybox(camera)

优化：
  ✅ 利用深度缓冲，只渲染未被物体遮挡的部分
  ✅ 在不透明物体之后，透明物体之前渲染
  ✅ 减少Overdraw
```

---

### 🔵 阶段8：拷贝颜色纹理（Copy Color Pass）可选

```
RenderPass: CopyColorPass

目的：拷贝当前颜色缓冲，供透明物体使用

何时执行：
  ✅ 有透明物体需要读取背景色（如玻璃折射）
  ✅ URP Asset → Opaque Texture: Enabled

执行流程：
SetRenderTarget(_CameraOpaqueTexture)
Blit(CameraColorTarget, _CameraOpaqueTexture)

输出：
  - _CameraOpaqueTexture
  - 包含所有不透明物体和天空盒的渲染结果

用途：
  - 透明物体可以采样背景色
  - 实现折射、扭曲等效果

Shader示例：
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);

half4 frag(Varyings input) : SV_Target
{
    // 采样背景色
    half4 bgColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.screenUV);
    
    // 应用折射等效果
    float2 distortedUV = input.screenUV + distortion;
    half4 refractColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV);
    
    return refractColor;
}
```

---

### 🔴 阶段9：透明物体渲染（Transparent Rendering）

```
RenderPass: DrawTransparentObjectsPass
LightMode = "UniversalForward"

目的：渲染半透明物体

执行流程：
SetRenderTarget(CameraColorTarget, CameraDepthTarget)

// ⭐ 按距离从后到前排序（严格！）
SortTransparentObjects(camera.position, FarToNear)

for each TransparentObject (sorted):
    SetPass(material, UniversalForwardPass)
    SetBlending(SrcAlpha, OneMinusSrcAlpha)
    ZWrite Off  // ⭐ 不写深度
    
    DrawMesh(object)

Shader示例：
SubShader
{
    Tags 
    { 
        "Queue"="Transparent"              // ⭐ 渲染队列
        "RenderType"="Transparent"
        "RenderPipeline"="UniversalPipeline"
    }
    
    Pass
    {
        Name "UniversalForward"
        Tags { "LightMode"="UniversalForward" }
        
        // ⭐ 透明物体的关键设置
        Blend SrcAlpha OneMinusSrcAlpha    // Alpha混合
        ZWrite Off                         // 不写深度
        Cull Back                          // 背面剔除（可选）
        
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        
        // 与不透明物体相同的编译指令
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _ADDITIONAL_LIGHTS
        // ...
        
        // 透明物体也使用UniversalFragmentPBR
        // 同样在一个Pass中处理所有光源
        half4 frag(Varyings input) : SV_Target
        {
            // 准备InputData和SurfaceData
            // ...
            
            // PBR光照计算
            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            
            // ⭐ Alpha来自纹理或材质属性
            color.a = surfaceData.alpha;
            
            return color;
        }
        ENDHLSLPROGRAM
    }
}

特殊处理：
  ⭐ 透明物体不能使用Deferred渲染
  ⭐ 即使在Deferred模式下，透明物体也使用Forward Pass
  ⭐ 可以读取_CameraOpaqueTexture（如果启用）
```

---

### 🟣 阶段10：后处理Pass（Post-Processing）

```
RenderPass: PostProcessPass

目的：应用后处理效果栈

URP使用Volume系统，支持的效果：
  1. Tonemapping (LDR/HDR)
  2. Bloom
  3. Chromatic Aberration
  4. Color Adjustments
  5. Color Curves
  6. Color Lookup (LUT)
  7. Depth of Field
  8. Film Grain
  9. Lens Distortion
  10. Lift Gamma Gain
  11. Motion Blur
  12. Panini Projection
  13. Shadows Midtones Highlights
  14. Split Toning
  15. Vignette
  16. White Balance

执行流程（Uber Shader优化）：
// ⭐ URP将多个效果合并到一个Uber Pass
SetRenderTarget(DestinationRT)
DrawFullScreenQuad(PostProcessUberShader)
{
    float4 color = tex2D(SourceRT, uv);
    
    // 按顺序应用所有启用的效果
    #if BLOOM_ENABLED
        color = ApplyBloom(color, uv);
    #endif
    
    #if DEPTH_OF_FIELD_ENABLED
        color = ApplyDoF(color, uv);
    #endif
    
    #if MOTION_BLUR_ENABLED
        color = ApplyMotionBlur(color, uv);
    #endif
    
    #if COLOR_GRADING_ENABLED
        color = ApplyColorGrading(color, _InternalLut);
    #endif
    
    #if CHROMATIC_ABERRATION_ENABLED
        color = ApplyChromaticAberration(color, uv);
    #endif
    
    #if VIGNETTE_ENABLED
        color = ApplyVignette(color, uv);
    #endif
    
    #if FILM_GRAIN_ENABLED
        color = ApplyFilmGrain(color, uv);
    #endif
    
    #if DITHERING_ENABLED
        color = ApplyDithering(color, uv);
    #endif
    
    // ⭐ 最后应用Tonemapping（HDR → LDR）
    #if TONEMAPPING_ENABLED
        color.rgb = ApplyTonemapping(color.rgb);
    #endif
    
    return color;
}

Blit(DestinationRT, CameraTarget)

优化特点：
  ✅ Uber Shader：多个效果合并到一个Pass
  ✅ Volume Blending：多个Volume自动混合
  ✅ 动态Shader变体：只编译启用的效果
  ✅ 降采样：Bloom等效果使用Mipmap链

性能建议：
  - 移动端：只开启必要的效果（Tonemapping + Color Grading）
  - PC端：可以开启更多效果
  - 避免开启Motion Blur（性能开销大）
```

---

### 🔵 阶段11：UI渲染（Canvas）

```
目的：渲染UI元素

与Built-in相同，支持三种模式：
  1. Screen Space - Overlay
  2. Screen Space - Camera
  3. World Space

执行流程：
// Overlay模式
SetRenderTarget(FinalColorTarget)
DisableDepthTest()

for each Canvas (sorted by Sort Order):
    for each CanvasRenderer:
        DrawMesh(uiMesh, uiMaterial)

Canvas批处理：
  ✅ URP支持Canvas批处理
  ✅ 相同材质和纹理自动批处理
  ❌ Mask和RectMask2D会打断批处理
```

---

## 🎯 URP管线完整流程图

```
╔═══════════════════════════════════════════════════════════════════╗
║                     帧开始 (Frame Start)                           ║
╚═══════════════════════════════════════════════════════════════════╝
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [CPU] 阶段0: 准备阶段                                              │
│   ├─ ScriptableRenderContext.Cull()                               │
│   ├─ 构建RenderingData                                            │
│   ├─ 排序（SRP Batcher友好）                                       │
│   └─ 构建CommandBuffer                                            │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段1: Setup Pass                                            │
│   ├─ 设置全局Shader变量                                            │
│   ├─ 设置View/Projection矩阵                                       │
│   └─ 设置光照数据（主光源 + 附加光源）                              │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段2: 阴影Pass                                              │
│   ├─ Pass 2.1: Main Light Shadow Pass                             │
│   │    └─ LightMode="ShadowCaster"                                │
│   │    └─ 级联阴影（Cascaded Shadow Maps）                        │
│   │    └─ 输出: _MainLightShadowmapTexture                        │
│   │                                                                │
│   └─ Pass 2.2: Additional Lights Shadow Pass                      │
│        └─ LightMode="ShadowCaster"                                │
│        └─ 阴影图集（Shadow Atlas）                                │
│        └─ 输出: _AdditionalLightsShadowmapTexture                 │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段3: 深度预通道 【可选】                                   │
│   └─ Pass 3.1: Depth Only Pass                                    │
│        └─ LightMode="DepthOnly"                                   │
│        └─ 输出: _CameraDepthTexture                               │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段4: 深度法线预通道 【可选】                               │
│   └─ Pass 4.1: Depth Normals Pass                                 │
│        └─ LightMode="DepthNormals"                                │
│        └─ 输出: _CameraDepthNormalsTexture                        │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段5: SSAO Pass 【可选】                                    │
│   ├─ SSAO计算Pass                                                 │
│   ├─ SSAO模糊Pass                                                 │
│   └─ 输出: _ScreenSpaceOcclusionTexture                           │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段6: 不透明物体主渲染                                      │
│                                                                   │
│ ┌─────────────────────────────────────────────────────────────┐  │
│ │ Forward渲染路径（默认）:                                     │  │
│ │   └─ Pass 6.1: UniversalForward Pass ⭐                     │  │
│ │        └─ LightMode="UniversalForward"                      │  │
│ │        └─ 在一个Pass中处理所有光源！                         │  │
│ │        └─ 主光源 + 附加光源（0-8个）                         │  │
│ │        └─ SRP Batcher优化                                   │  │
│ └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│ ┌─────────────────────────────────────────────────────────────┐  │
│ │ Deferred渲染路径（Unity 2021+）:                            │  │
│ │   ├─ Pass 6.1: GBuffer Pass                                 │  │
│ │   │    └─ LightMode="UniversalGBuffer"                      │  │
│ │   │    └─ 输出到GBuffer（5个RT）                            │  │
│ │   │                                                          │  │
│ │   └─ Pass 6.2: Deferred Lighting Pass                       │  │
│ │        └─ 读取GBuffer                                       │  │
│ │        └─ 全屏/Light Volume应用光照                         │  │
│ └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段7: 天空盒渲染                                            │
│   └─ 只渲染深度为远平面的像素                                      │
│   └─ 利用深度缓冲优化                                              │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段8: 拷贝颜色纹理 【可选】                                 │
│   └─ CopyColorPass                                                │
│   └─ 输出: _CameraOpaqueTexture                                   │
│   └─ 供透明物体读取背景                                            │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段9: 透明物体渲染                                          │
│   ├─ 按距离从后到前排序 ⭐                                         │
│   ├─ UniversalForward Pass                                        │
│   │    └─ LightMode="UniversalForward"                           │
│   │    └─ ZWrite Off, Alpha Blend                                │
│   │    └─ 同样在一个Pass处理所有光源                              │
│   │                                                               │
│   └─ 即使在Deferred模式也使用Forward                              │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段10: 后处理 (Post-Processing)                             │
│   ├─ Uber Shader（多效果合并）                                     │
│   ├─ Bloom                                                        │
│   ├─ Depth of Field                                               │
│   ├─ Motion Blur                                                  │
│   ├─ Color Grading (LUT)                                          │
│   ├─ Chromatic Aberration                                         │
│   ├─ Vignette                                                     │
│   ├─ Film Grain                                                   │
│   ├─ Dithering                                                    │
│   └─ Tonemapping (HDR → LDR)                                      │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
┌───────────────────────────────────────────────────────────────────┐
│ [GPU] 阶段11: UI渲染 (Canvas - Overlay)                            │
│   └─ 直接渲染到最终framebuffer                                     │
│   └─ 不受后处理影响                                                │
└───────────────────────────────────────────────────────────────────┘
                                 ↓
╔═══════════════════════════════════════════════════════════════════╗
║                     帧结束 (Frame End)                             ║
║                   Present to Screen                                ║
╚═══════════════════════════════════════════════════════════════════╝
```

---

# 两种管线对比

## 📊 Pass数量对比

### 场景：10个不透明物体，5个光源

| Pass | Built-in Forward | Built-in Deferred | URP Forward | URP Deferred |
|------|-----------------|-------------------|-------------|--------------|
| **阴影** | 1 × 材质数 | 1 × 材质数 | 1 × 材质数 | 1 × 材质数 |
| **深度预通道** | 0 | 0 | 0-1 | 0-1 |
| **主渲染** | 10 × (1+5) = 60 | 10 + 1 = 11 | 10 | 10 + 1 = 11 |
| **天空盒** | 1 | 1 | 1 | 1 |
| **透明** | N × (1+光源数) | N × (1+光源数) | N | N |
| **后处理** | 多个Pass | 多个Pass | 1个Uber Pass | 1个Uber Pass |
| **总DrawCall** | ~70 | ~20 | ~15 ⭐ | ~20 |

---

## 🔑 关键区别

| 特性 | Built-in | URP |
|------|----------|-----|
| **Forward光源处理** | 每个光源一个Pass | 单Pass处理所有光源 ⭐ |
| **批处理优化** | Static/Dynamic Batching | SRP Batcher ⭐ |
| **Shader语言** | CG/HLSL | HLSL |
| **后处理** | 多Pass | Uber Shader |
| **移动端优化** | 一般 | 极好 ⭐ |
| **学习曲线** | 简单 | 中等 |
| **未来支持** | 维护模式 | 持续更新 ⭐ |

---

## 🎯 性能对比总结

```
移动平台：
  URP Forward >> Built-in Forward >> Built-in Deferred
  
PC平台（少量光源）：
  URP Forward > Built-in Forward > Built-in Deferred
  
PC平台（大量光源）：
  URP Deferred ≈ Built-in Deferred > URP Forward > Built-in Forward
```

---

# 实际场景示例

## 场景1：移动RPG游戏

```
配置：
  - 10个角色模型
  - 1个主方向光
  - 3个点光源
  - 一些透明粒子

Built-in Forward:
  阴影: 10个
  ForwardBase: 10个
  ForwardAdd: 10 × 3 = 30个
  透明: 5个
  总计: 55个DrawCall

URP Forward:
  阴影: 10个
  UniversalForward: 10个（包含所有光源！）
  透明: 5个
  总计: 25个DrawCall ⭐ 节省54%
```

## 场景2：PC FPS游戏（室内）

```
配置：
  - 50个物体
  - 1个主方向光
  - 20个点光源（灯泡、火把等）
  - 10个透明物体

Built-in Forward:
  阴影: 50个
  ForwardBase: 50个
  ForwardAdd: 50 × 20 = 1000个
  透明: 10 × (1+20) = 210个
  总计: 1310个DrawCall ❌ 太多！

Built-in Deferred:
  阴影: 50个
  Deferred: 50个
  Lighting: 1 + 20 = 21个
  透明: 10 × (1+20) = 210个
  总计: 331个DrawCall ✅ 好很多

URP Deferred:
  阴影: 50个
  GBuffer: 50个
  Lighting: 1 + 20 = 21个
  透明: 10个（单Pass处理所有光源）
  总计: 131个DrawCall ⭐ 最优！
```

---

## 🎓 学习建议

1. **理解Pass执行顺序** - 使用Frame Debugger实际查看
2. **掌握LightMode标签** - 每个Pass的作用
3. **优化Shader编写** - 支持SRP Batcher
4. **合理选择渲染路径** - 根据平台和场景
5. **使用RenderFeature扩展** - URP的自定义Pass

完整的Pass流程文档已经创建完成！🎉
