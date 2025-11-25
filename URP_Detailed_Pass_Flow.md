# Unity 2022 URP 渲染Pass超详细流程

## 🎯 核心理解（先读这个！）

### 关键概念澄清

**您的疑惑：**
> "没看到渲染不透明pass呢？他和UniversalForward Pass有什么关联呢？"

**答案：**
```
UniversalForward Pass = 渲染不透明物体的主渲染Pass ⭐

换句话说：
  "渲染不透明物体" 这个阶段
  =
  执行 UniversalForward Pass

它们不是两个不同的东西，而是同一个东西！
```

**更详细的说明：**
```
渲染阶段名称：DrawOpaqueObjects（渲染不透明物体）
  ↓
具体如何渲染：使用 UniversalForward Pass
  ↓
Shader中的标识：LightMode="UniversalForward"
  ↓
光照计算：在这个Pass的片元着色器中完成 ✅
```

---

## 📋 完整的URP渲染流程（超详细版）

### 场景设置
```
场景：
  - 摄像机：1个
  - 不透明物体：3个（立方体1、立方体2、立方体3）
  - 透明物体：2个（玻璃窗、水面）
  - 光源：
    - 1个方向光（主光源，开启阴影）
    - 2个点光源（附加光源）
```

---

## 🔵 阶段0：CPU端准备（渲染线程）

```
[CPU] 执行：UniversalRenderPipeline.RenderSingleCamera()

┌─────────────────────────────────────────────────┐
│ 1. 剔除（Culling）                               │
│    ScriptableRenderContext.Cull()                │
│                                                  │
│    输入：                                         │
│      - ScriptableCullingParameters              │
│      - 摄像机的视锥体                            │
│                                                  │
│    处理：                                         │
│      ├─ 视锥体剔除（Frustum Culling）            │
│      │   5个物体 → 检测是否在视锥体内            │
│      │                                           │
│      ├─ 遮挡剔除（Occlusion Culling）            │
│      │   检查是否被其他物体遮挡                   │
│      │                                           │
│      └─ 光源剔除                                 │
│          哪些光源影响哪些物体                     │
│                                                  │
│    输出：CullingResults                          │
│      - visibleRenderers（5个可见物体）          │
│      - visibleLights（3个可见光源）             │
│      - visibleReflectionProbes                  │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ 2. 构建RenderingData                             │
│                                                  │
│    RenderingData包含：                           │
│      ├─ cameraData                              │
│      │   - 摄像机位置、旋转                      │
│      │   - View矩阵、Projection矩阵             │
│      │   - 渲染分辨率                            │
│      │                                           │
│      ├─ lightData                               │
│      │   - 主光源信息（方向、颜色、强度）         │
│      │   - 附加光源列表（2个点光源）             │
│      │   - 每个物体最多几个光源（默认8）          │
│      │                                           │
│      ├─ shadowData                              │
│      │   - 阴影距离                              │
│      │   - 阴影分辨率                            │
│      │   - 级联数量                              │
│      │                                           │
│      └─ cullResults                             │
│          - 剔除结果（5个可见物体）               │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ 3. 排序（Sorting）                               │
│                                                  │
│    不透明物体排序（3个）：                        │
│      主排序键：RenderQueue（渲染队列值）          │
│      次排序键：Material InstanceID ⭐            │
│      三排序键：Distance（可选）                   │
│                                                  │
│    结果：                                         │
│      [立方体1(材质A), 立方体2(材质A), 立方体3(材质B)]
│      ↑ 相同材质的物体会排在一起（SRP Batcher优化）│
│                                                  │
│    透明物体排序（2个）：                          │
│      主排序键：RenderQueue                       │
│      次排序键：Distance（从后到前）⭐            │
│                                                  │
│    结果：                                         │
│      [水面(远), 玻璃窗(近)]                      │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ 4. 构建渲染命令（CommandBuffer）                 │
│                                                  │
│    将渲染指令打包成CommandBuffer发送到GPU         │
└─────────────────────────────────────────────────┘

完成CPU准备，进入GPU渲染
```

---

## 🟡 阶段1：Setup Pass（设置全局变量）

```
[GPU] RenderPass: 无独立Pass，在C#代码中执行

目的：设置Shader全局变量，供后续Pass使用

代码位置：UniversalRenderer.Setup()

┌─────────────────────────────────────────────────┐
│ 执行内容：                                        │
│                                                  │
│ 1. 设置View/Projection矩阵                       │
│    cmd.SetViewProjectionMatrices(view, proj)    │
│                                                  │
│ 2. 设置全局Shader变量                            │
│    ├─ _WorldSpaceCameraPos                      │
│    │   └─ 摄像机世界坐标                          │
│    │                                             │
│    ├─ _ScreenParams                             │
│    │   └─ (width, height, 1+1/width, 1+1/height)│
│    │                                             │
│    ├─ _ScaledScreenParams                       │
│    │   └─ 考虑渲染缩放的屏幕参数                   │
│    │                                             │
│    ├─ _ZBufferParams                            │
│    │   └─ 深度缓冲重建参数                        │
│    │                                             │
│    ├─ unity_OrthoParams                         │
│    │   └─ 正交/透视投影参数                       │
│    │                                             │
│    └─ _ProjectionParams                         │
│        └─ 投影矩阵参数                            │
│                                                  │
│ 3. 设置光照全局变量（关键！）                     │
│    ├─ _MainLightPosition                        │
│    │   └─ float4(方向光方向, 0)                   │
│    │                                             │
│    ├─ _MainLightColor                           │
│    │   └─ float4(颜色RGB, 强度)                  │
│    │                                             │
│    ├─ _AdditionalLightsCount                    │
│    │   └─ int（附加光源数量 = 2）                 │
│    │                                             │
│    └─ _AdditionalLightsBuffer                   │
│        └─ StructuredBuffer（2个点光源的数据）    │
│           ├─ [0] 点光源1（位置、颜色、范围）      │
│           └─ [1] 点光源2（位置、颜色、范围）      │
│                                                  │
│ 4. 配置渲染目标                                  │
│    SetRenderTarget(cameraColorTarget,           │
│                    cameraDepthTarget)           │
└─────────────────────────────────────────────────┘

输出：
  ✅ GPU端全局变量已设置
  ✅ 后续Shader可以直接使用这些变量
  ✅ 渲染目标已配置

注意：
  这个"Pass"实际上不是一个真正的渲染Pass
  而是在C#代码中设置GPU状态
```

---

## 🟢 阶段2：阴影Pass（Shadow Rendering）

### Pass 2.1: 主光源阴影（Main Light Shadow）

```
[GPU] RenderPass: MainLightShadowCasterPass
      LightMode = "ShadowCaster"

目的：为主方向光渲染级联阴影贴图

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   - 投射阴影的物体（3个不透明 + 2个透明 = 5个）   │
│   - 主光源的方向和位置                            │
│   - 级联配置（假设4级联）                         │
│                                                  │
│ 渲染目标：                                        │
│   _MainLightShadowmapTexture                    │
│   格式：Depth32 或 Depth16                       │
│   大小：2048 × 2048（根据Quality设置）           │
│                                                  │
│ 布局（4级联）：                                   │
│   ┌────────┬────────┐                           │
│   │Cascade0│Cascade1│  每个级联覆盖不同距离范围   │
│   │ 1024²  │ 1024²  │                           │
│   ├────────┼────────┤  Cascade0: 0-10m（最近）  │
│   │Cascade2│Cascade3│  Cascade1: 10-30m         │
│   │ 1024²  │ 1024²  │  Cascade2: 30-80m         │
│   └────────┴────────┘  Cascade3: 80-150m（最远）│
└─────────────────────────────────────────────────┘

执行流程：

for (int cascadeIndex = 0; cascadeIndex < 4; cascadeIndex++)
{
    ┌─────────────────────────────────────────┐
    │ 步骤1：设置级联视口和矩阵                 │
    │   SetViewport(cascade[i].viewport)      │
    │   SetViewProjectionMatrix(cascade[i])   │
    └─────────────────────────────────────────┘
    
    ┌─────────────────────────────────────────┐
    │ 步骤2：清除深度                          │
    │   ClearDepth(1.0)                       │
    └─────────────────────────────────────────┘
    
    ┌─────────────────────────────────────────┐
    │ 步骤3：渲染该级联范围内的物体             │
    │                                         │
    │ for each Object in cascadeCullResults:  │
    │   {                                     │
    │     SetPass(material, "ShadowCaster")   │
    │     DrawMesh(object)                    │
    │   }                                     │
    │                                         │
    │ Cascade 0 示例：                         │
    │   SetPass 1: 材质A的ShadowCaster         │
    │     DrawCall 1: 立方体1                 │
    │     DrawCall 2: 立方体2                 │
    │   SetPass 2: 材质B的ShadowCaster         │
    │     DrawCall 3: 立方体3                 │
    │   SetPass 3: 透明材质1的ShadowCaster     │
    │     DrawCall 4: 玻璃窗                  │
    │   SetPass 4: 透明材质2的ShadowCaster     │
    │     DrawCall 5: 水面                    │
    └─────────────────────────────────────────┘
}

输出：
  ✅ _MainLightShadowmapTexture（4级联深度图）
  ✅ 每个级联的ViewProjection矩阵
  ✅ 供后续主渲染使用

ShadowCaster Shader示例：
Pass
{
    Name "ShadowCaster"
    Tags { "LightMode"="ShadowCaster" }
    
    ZWrite On
    ZTest LEqual
    ColorMask 0  // ⭐ 不写颜色，只写深度
    
    HLSLPROGRAM
    #pragma vertex ShadowPassVertex
    #pragma fragment ShadowPassFragment
    
    Varyings ShadowPassVertex(Attributes input)
    {
        Varyings output;
        
        // ⭐ 使用光源空间的ViewProjection矩阵
        float3 positionWS = TransformObjectToWorld(input.positionOS);
        output.positionCS = TransformWorldToHClip(positionWS);
        
        return output;
    }
    
    half4 ShadowPassFragment(Varyings input) : SV_TARGET
    {
        return 0;  // 只写深度，不需要返回颜色
    }
    ENDHLSLPROGRAM
}

性能统计（本场景）：
  SetPass Call: 4个（4种不同材质）
  DrawCall: 5个（5个物体）
  时间开销：约0.5-1ms（取决于物体复杂度）
```

### Pass 2.2: 附加光源阴影（Additional Lights Shadow）

```
[GPU] RenderPass: AdditionalLightsShadowCasterPass
      LightMode = "ShadowCaster"

目的：为点光源和聚光灯渲染阴影（如果开启）

假设：2个点光源都开启阴影

┌─────────────────────────────────────────────────┐
│ 渲染目标：                                        │
│   _AdditionalLightsShadowmapTexture             │
│   格式：Depth16                                  │
│   大小：2048 × 2048（图集）                      │
│                                                  │
│ 图集布局（2个点光源）：                           │
│   ┌────────────────┬────────────────┐           │
│   │  PointLight 0  │  PointLight 1  │           │
│   │   (1024×1024)  │   (1024×1024)  │           │
│   │                │                │           │
│   │  (6个面的      │  (6个面的      │           │
│   │   CubeMap)     │   CubeMap)     │           │
│   └────────────────┴────────────────┘           │
└─────────────────────────────────────────────────┘

执行流程：

for each PointLight with shadows:
{
    for each CubeFace (6 faces):
    {
        ┌─────────────────────────────────────┐
        │ 设置当前面的视口和矩阵               │
        │ SetViewport(light.atlasViewport)    │
        │ SetViewProjectionMatrix(face)       │
        └─────────────────────────────────────┘
        
        ┌─────────────────────────────────────┐
        │ 渲染该方向可见的物体                 │
        │ for each Object in faceCullResults: │
        │   DrawMesh(object, ShadowCasterPass)│
        └─────────────────────────────────────┘
    }
}

输出：
  ✅ _AdditionalLightsShadowmapTexture
  ✅ 包含所有附加光源的阴影

注意：
  点光源需要渲染6个面（CubeMap）
  性能开销较大，移动端谨慎使用
```

---

## 🔵 阶段3：深度预通道（Depth Prepass）可选

```
[GPU] RenderPass: DepthOnlyPass
      LightMode = "DepthOnly"

目的：提前渲染深度，优化主渲染Pass

何时执行：
  ✅ 开启SSAO
  ✅ 开启Depth Texture（URP Asset → General）
  ✅ 自定义RenderFeature需要

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   - 3个不透明物体                                 │
│                                                  │
│ 渲染目标：                                        │
│   _CameraDepthTexture                           │
│   格式：R32_Float 或 Depth24Stencil8            │
│   大小：1920 × 1080（与摄像机分辨率一致）         │
└─────────────────────────────────────────────────┘

执行流程：

SetRenderTarget(_CameraDepthTexture)
ClearDepth(1.0)

┌─────────────────────────────────────────────────┐
│ 渲染所有不透明物体的深度                          │
│                                                  │
│ SetPass 1: 材质A的DepthOnly Pass                 │
│   DrawCall 1: 立方体1 ⭐ 只写深度                │
│   DrawCall 2: 立方体2                            │
│                                                  │
│ SetPass 2: 材质B的DepthOnly Pass                 │
│   DrawCall 3: 立方体3                            │
└─────────────────────────────────────────────────┘

DepthOnly Shader示例：
Pass
{
    Name "DepthOnly"
    Tags { "LightMode"="DepthOnly" }
    
    ZWrite On
    ColorMask 0  // ⭐ 关键：不写颜色
    
    HLSLPROGRAM
    #pragma vertex DepthOnlyVertex
    #pragma fragment DepthOnlyFragment
    
    Varyings DepthOnlyVertex(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS);
        return output;
    }
    
    half4 DepthOnlyFragment(Varyings input) : SV_TARGET
    {
        return 0;  // 不需要返回颜色
    }
    ENDHLSLPROGRAM
}

输出：
  ✅ _CameraDepthTexture（深度纹理）
  ✅ 后续主渲染可以利用Early-Z优化

优势：
  当像素着色器很复杂时，提前写深度可以：
  - 跳过被遮挡的像素
  - 减少片元着色器执行次数
  - 适合：复杂材质、大量Overdraw的场景

劣势：
  - 增加了一次完整的几何Pass
  - 不适合：简单场景、移动端
```

---

## 🔴 阶段4：不透明物体主渲染（Opaque Rendering）⭐⭐⭐

```
[GPU] RenderPass: DrawOpaqueObjectsPass
      LightMode = "UniversalForward"

⭐⭐⭐ 关键理解 ⭐⭐⭐
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
这个阶段的名称：DrawOpaqueObjects（渲染不透明物体）
使用的Shader Pass：UniversalForward Pass
光照计算位置：就在这个Pass的片元着色器中！⭐
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

换句话说：
  "渲染不透明物体" = 执行 UniversalForward Pass
  光照计算 = 在 UniversalForward Pass 的 frag() 中完成
```

### 详细执行流程

```
┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   - 3个不透明物体（已排序）                       │
│   - 主光源数据（从Setup Pass设置的全局变量）      │
│   - 附加光源数据（2个点光源）                     │
│   - 阴影贴图（_MainLightShadowmapTexture等）     │
│   - 深度纹理（如果有DepthPrepass）               │
│                                                  │
│ 渲染目标：                                        │
│   ColorBuffer: _CameraColorTarget               │
│   DepthBuffer: _CameraDepthTarget               │
│   格式：RGB8 或 RGB10A2 (HDR)                    │
│   大小：1920 × 1080                              │
└─────────────────────────────────────────────────┘

SetRenderTarget(_CameraColorTarget, _CameraDepthTarget)

如果没有DepthPrepass：
  ClearColor(摄像机背景色)
  ClearDepth(1.0)

┌─────────────────────────────────────────────────┐
│ 渲染循环（SRP Batcher优化）                       │
│                                                  │
│ ⭐ SetPass 1: 材质A的UniversalForward Pass        │
│   │                                              │
│   ├─ 设置材质属性（通过CBUFFER）                  │
│   │   - _BaseColor                              │
│   │   - _Smoothness                             │
│   │   - _Metallic                               │
│   │   - 纹理绑定（_BaseMap等）                   │
│   │                                              │
│   ├─ DrawCall 1: 立方体1                        │
│   │   ├─ 顶点着色器：变换到裁剪空间               │
│   │   └─ 片元着色器：计算PBR光照 ⭐              │
│   │       ├─ 计算主光源                          │
│   │       ├─ 循环计算2个附加光源 ⭐               │
│   │       ├─ 采样阴影                            │
│   │       ├─ 环境光和GI                          │
│   │       └─ 输出最终颜色                        │
│   │                                              │
│   └─ DrawCall 2: 立方体2                        │
│       └─ 同样的片元着色器处理                     │
│                                                  │
│ ⭐ SetPass 2: 材质B的UniversalForward Pass        │
│   │                                              │
│   └─ DrawCall 3: 立方体3                        │
│       └─ 片元着色器计算光照                       │
└─────────────────────────────────────────────────┘

输出：
  ✅ _CameraColorTarget（包含所有不透明物体的颜色）
  ✅ _CameraDepthTarget（深度信息）

统计：
  SetPass Call: 2个（2种不同材质）
  DrawCall: 3个（3个物体）
  时间开销：主要渲染时间（2-5ms，取决于复杂度）
```

### UniversalForward Pass详细解析

```hlsl
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// UniversalForward Pass 完整代码示例
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Pass
{
    Name "UniversalForward"
    Tags { "LightMode"="UniversalForward" }  // ⭐ 关键标识
    
    HLSLPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    
    // ⭐ 编译指令（决定包含哪些功能）
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
    #pragma multi_compile_fog
    
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 材质属性（CBUFFER用于SRP Batcher）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Smoothness;
        half _Metallic;
    CBUFFER_END
    
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    
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
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 顶点着色器
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    Varyings vert(Attributes input)
    {
        Varyings output;
        
        // 变换到裁剪空间
        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = vertexInput.positionCS;
        output.positionWS = vertexInput.positionWS;
        
        // 变换法线
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
        output.normalWS = normalInput.normalWS;
        
        // UV变换
        output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
        
        // 雾效因子
        output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
        
        return output;
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 片元着色器（⭐⭐⭐ 光照计算在这里！⭐⭐⭐）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    half4 frag(Varyings input) : SV_Target
    {
        // ============================================
        // 步骤1：准备表面数据
        // ============================================
        half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
        half4 albedo = baseMap * _BaseColor;
        
        float3 positionWS = input.positionWS;
        float3 normalWS = normalize(input.normalWS);
        float3 viewDirectionWS = normalize(GetCameraPositionWS() - positionWS);
        
        // ============================================
        // 步骤2：创建InputData（光照系统需要）
        // ============================================
        InputData inputData = (InputData)0;
        inputData.positionWS = positionWS;
        inputData.normalWS = normalWS;
        inputData.viewDirectionWS = viewDirectionWS;
        inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
        inputData.fogCoord = input.fogFactor;
        inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);
        
        // ============================================
        // 步骤3：创建SurfaceData（材质属性）
        // ============================================
        SurfaceData surfaceData = (SurfaceData)0;
        surfaceData.albedo = albedo.rgb;
        surfaceData.metallic = _Metallic;
        surfaceData.smoothness = _Smoothness;
        surfaceData.alpha = albedo.a;
        // ... 其他属性
        
        // ============================================
        // ⭐⭐⭐ 步骤4：计算光照（关键！）⭐⭐⭐
        // ============================================
        half4 color = UniversalFragmentPBR(inputData, surfaceData);
        //            ↑ 这个函数内部做了什么？见下方详解！
        
        // ============================================
        // 步骤5：应用雾效
        // ============================================
        color.rgb = MixFog(color.rgb, inputData.fogCoord);
        
        return color;
    }
    
    ENDHLSLPROGRAM
}
```

### UniversalFragmentPBR 内部实现（核心！）

```hlsl
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// UniversalFragmentPBR 函数内部实现
// 位置：Lighting.hlsl
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    // ============================================
    // 准备BRDF数据
    // ============================================
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    
    half3 color = 0;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ⭐ 1. 计算主光源（方向光）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    Light mainLight = GetMainLight(inputData.shadowCoord);
    //                ↑ 从全局变量读取主光源数据
    //                  _MainLightPosition
    //                  _MainLightColor
    //                  采样阴影贴图
    
    // 计算主光源的PBR光照
    color += LightingPhysicallyBased(
        brdfData,                    // BRDF参数
        mainLight,                   // 光源数据
        inputData.normalWS,          // 法线
        inputData.viewDirectionWS    // 视线方向
    );
    // 结果：主光源的漫反射 + 高光
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ⭐ 2. 循环计算附加光源（点光源、聚光灯）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    #ifdef _ADDITIONAL_LIGHTS
    
    // 获取影响当前像素的附加光源数量
    uint pixelLightCount = GetAdditionalLightsCount();
    //                     ↑ 读取 _AdditionalLightsCount
    //                       在我们的例子中 = 2
    
    // ⭐ 关键：在片元着色器中循环处理每个光源！
    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
    {
        // 获取第i个附加光源的数据
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        //            ↑ 从 _AdditionalLightsBuffer 读取
        //              包含：位置、颜色、范围、衰减
        
        // 计算该光源的PBR光照
        half3 additionalColor = LightingPhysicallyBased(
            brdfData,
            light,
            inputData.normalWS,
            inputData.viewDirectionWS
        );
        
        // ⭐ 累加到最终颜色
        color += additionalColor;
    }
    
    // 实际执行：
    //   循环迭代0：计算点光源1的光照 → 累加
    //   循环迭代1：计算点光源2的光照 → 累加
    
    #endif
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 3. 添加环境光和GI（全局光照）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    color += GlobalIllumination(
        brdfData,
        inputData.bakedGI,           // 光照贴图
        surfaceData.occlusion,
        inputData.normalWS,
        inputData.viewDirectionWS
    );
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 4. 添加自发光
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    color += surfaceData.emission;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 返回最终颜色
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    return half4(color, surfaceData.alpha);
}
```

### 关键总结

```
┌─────────────────────────────────────────────────┐
│ ⭐⭐⭐ 核心理解 ⭐⭐⭐                              │
│                                                  │
│ 问题1："没看到渲染不透明pass呢？"                 │
│ 答案：UniversalForward Pass 就是不透明渲染Pass！  │
│                                                  │
│ 问题2："和UniversalForward Pass有什么关联？"      │
│ 答案：它们是同一个东西！                          │
│   - 阶段名称：DrawOpaqueObjects                 │
│   - Shader Pass：UniversalForward               │
│   - LightMode标签："UniversalForward"           │
│                                                  │
│ 问题3："光照直接在这个pass里面计算吗？"           │
│ 答案：是的！在UniversalForward的frag()中计算！   │
│   1. 主光源光照计算                              │
│   2. 循环计算所有附加光源                        │
│   3. 环境光和GI                                 │
│   4. 全部在一个片元着色器中完成！⭐              │
└─────────────────────────────────────────────────┘

对比Built-in Forward：
┌─────────────────────────────────────────────────┐
│ Built-in Forward:                                │
│   ForwardBase Pass  → 主光源（1个Pass）          │
│   ForwardAdd Pass   → 光源1（1个Pass）           │
│   ForwardAdd Pass   → 光源2（1个Pass）           │
│   总计：3个Pass                                  │
│                                                  │
│ URP Forward:                                     │
│   UniversalForward Pass → 所有光源（1个Pass）⭐  │
│     在片元着色器中循环：                          │
│       - 主光源                                   │
│       - 光源1（for循环）                         │
│       - 光源2（for循环）                         │
│   总计：1个Pass ✅                               │
└─────────────────────────────────────────────────┘

性能优势：
  ✅ Pass数量减少 → SetPass Call减少
  ✅ 状态切换减少 → CPU开销降低
  ✅ GPU并行计算 → 光照计算更高效
```

---

## 🟡 阶段5：天空盒渲染（Skybox）

```
[GPU] RenderPass: DrawSkyboxPass

目的：渲染天空盒背景

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   - 天空盒材质（6面体或程序化）                   │
│   - 当前深度缓冲                                 │
│                                                  │
│ 条件：                                            │
│   - Camera.clearFlags = Skybox                  │
│   - 分配了Skybox材质                             │
└─────────────────────────────────────────────────┘

SetRenderTarget(_CameraColorTarget, _CameraDepthTarget)
SetDepthTest(LEqual)  // ⭐ 只渲染深度为远平面的像素

Context.DrawSkybox(camera)
// Unity内部实现：
//   渲染一个立方体或球体
//   深度测试确保只在没有物体的地方绘制

输出：
  ✅ 天空盒颜色添加到ColorBuffer
  ✅ 只在深度=1.0的像素上绘制

统计：
  SetPass: 1个
  DrawCall: 1个
  时间：<0.5ms
```

---

## 🟢 阶段6：拷贝颜色纹理（Copy Color）可选

```
[GPU] RenderPass: CopyColorPass

目的：拷贝当前颜色缓冲，供透明物体使用

何时执行：
  ✅ URP Asset → Opaque Texture: Enabled
  ✅ 有透明物体需要读取背景（折射、扭曲等）

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   _CameraColorTarget（包含不透明物体+天空盒）     │
│                                                  │
│ 操作：                                            │
│   Blit(_CameraColorTarget, _CameraOpaqueTexture)│
│                                                  │
│ 输出：                                            │
│   _CameraOpaqueTexture                          │
│   └─ 包含所有不透明内容的快照                     │
└─────────────────────────────────────────────────┘

用途：
  透明物体的Shader可以采样_CameraOpaqueTexture
  实现：
    - 折射效果（玻璃）
    - 扭曲效果（热浪）
    - 模糊效果（毛玻璃）

Shader示例：
half4 frag(Varyings input) : SV_Target
{
    // 采样背景色
    half4 bgColor = SAMPLE_TEXTURE2D(
        _CameraOpaqueTexture, 
        sampler_CameraOpaqueTexture, 
        input.screenUV
    );
    
    // 应用折射
    float2 distortedUV = input.screenUV + refraction;
    half4 refractColor = SAMPLE_TEXTURE2D(..., distortedUV);
    
    return refractColor;
}

统计：
  SetPass: 1个
  DrawCall: 1个（全屏Blit）
  时间：0.5-1ms
```

---

## 🔴 阶段7：透明物体渲染（Transparent Rendering）

```
[GPU] RenderPass: DrawTransparentObjectsPass
      LightMode = "UniversalForward"  ⭐ 同样是UniversalForward！

目的：渲染半透明物体

⭐ 关键点：
  透明物体也使用 UniversalForward Pass
  和不透明物体用的是同一个Pass！
  只是渲染设置不同（Alpha混合、不写深度）

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   - 2个透明物体（已按距离从后到前排序）           │
│   - _CameraOpaqueTexture（如果有）               │
│   - 所有光源数据（和不透明物体一样）              │
│                                                  │
│ 渲染目标：                                        │
│   _CameraColorTarget（累加到已有颜色）           │
│   _CameraDepthTarget（只读，不写入）             │
└─────────────────────────────────────────────────┘

SetRenderTarget(_CameraColorTarget, _CameraDepthTarget)

┌─────────────────────────────────────────────────┐
│ 渲染循环（从后到前！）                            │
│                                                  │
│ SetPass 1: 透明材质1的UniversalForward Pass       │
│   设置：                                          │
│     Blend SrcAlpha OneMinusSrcAlpha  // Alpha混合 │
│     ZWrite Off                       // 不写深度  │
│     ZTest LEqual                     // 深度测试  │
│                                                  │
│   DrawCall 1: 水面（远处）                       │
│     ├─ 顶点着色器：同不透明物体                   │
│     └─ 片元着色器：                               │
│         ├─ 计算主光源 ⭐                          │
│         ├─ 循环计算2个附加光源 ⭐                 │
│         ├─ 可选：采样_CameraOpaqueTexture        │
│         └─ 输出：half4(color, alpha)             │
│                  ↑ Alpha用于混合                 │
│                                                  │
│ SetPass 2: 透明材质2的UniversalForward Pass       │
│   DrawCall 2: 玻璃窗（近处）                     │
│     └─ 同样的光照计算流程                         │
└─────────────────────────────────────────────────┘

透明物体Shader示例：
SubShader
{
    Tags 
    { 
        "Queue"="Transparent"           // ⭐ 渲染队列
        "RenderType"="Transparent"
        "RenderPipeline"="UniversalPipeline"
    }
    
    Pass
    {
        Name "UniversalForward"
        Tags { "LightMode"="UniversalForward" }  // ⭐ 同样的LightMode
        
        // ⭐ 透明物体的关键设置
        Blend SrcAlpha OneMinusSrcAlpha  // Alpha混合
        ZWrite Off                       // 不写深度
        Cull Back                        // 背面剔除
        
        HLSLPROGRAM
        // ... 和不透明物体完全一样的代码 ...
        // UniversalFragmentPBR() 计算光照
        // 唯一区别：返回值的Alpha用于混合
        ENDHLSLPROGRAM
    }
}

Alpha混合过程（GPU硬件执行）：
  FinalColor = SourceColor × SrcAlpha + DestColor × (1 - SrcAlpha)
  
  例如：
    SourceColor = (1, 0, 0, 0.5)  // 半透明红色
    DestColor = (0, 0, 1, 1)      // 背景蓝色
    
  结果：
    FinalColor = (1,0,0) × 0.5 + (0,0,1) × 0.5
               = (0.5, 0, 0.5)    // 紫色

输出：
  ✅ 透明物体混合到ColorBuffer
  ✅ 深度缓冲未改变

统计：
  SetPass: 2个（2种透明材质）
  DrawCall: 2个
  时间：1-2ms

注意：
  ⭐ 即使在Deferred模式下，透明物体也使用Forward！
  ⭐ 透明物体不能写入GBuffer（无法延迟渲染）
  ⭐ 必须严格从后到前排序（保证正确混合）
```

---

## 🟣 阶段8：后处理（Post-Processing）

```
[GPU] RenderPass: PostProcessPass

目的：应用后处理效果栈

URP使用Uber Shader技术：
  将多个效果合并到一个Pass中 ⭐

┌─────────────────────────────────────────────────┐
│ 输入：                                            │
│   _CameraColorTarget（完整渲染的场景）           │
│   _CameraDepthTexture（深度信息）               │
│                                                  │
│ 临时RT：                                         │
│   _AfterPostProcessTexture                      │
└─────────────────────────────────────────────────┘

SetRenderTarget(_AfterPostProcessTexture)

DrawFullScreenQuad(PostProcessUberShader)
{
    float4 color = tex2D(_CameraColorTarget, uv);
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ⭐ 在一个Shader中应用所有启用的效果
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    #if BLOOM_ENABLED
        color = ApplyBloom(color, uv);
    #endif
    
    #if DEPTH_OF_FIELD_ENABLED
        float depth = tex2D(_CameraDepthTexture, uv).r;
        color = ApplyDoF(color, uv, depth);
    #endif
    
    #if MOTION_BLUR_ENABLED
        color = ApplyMotionBlur(color, uv, velocityTexture);
    #endif
    
    #if COLOR_GRADING_ENABLED
        // 使用LUT（Look-Up Texture）
        color = ApplyColorGrading(color, _InternalLut);
    #endif
    
    #if CHROMATIC_ABERRATION_ENABLED
        color = ApplyChromaticAberration(color, uv);
    #endif
    
    #if VIGNETTE_ENABLED
        color = ApplyVignette(color, uv);
    #endif
    
    #if FILM_GRAIN_ENABLED
        color = ApplyFilmGrain(color, uv, _Time);
    #endif
    
    // ⭐ 最后应用Tonemapping（HDR → LDR）
    #if TONEMAPPING_ENABLED
        color.rgb = ApplyTonemapping(color.rgb);
    #endif
    
    #if DITHERING_ENABLED
        color = ApplyDithering(color, uv);
    #endif
    
    return color;
}

Blit(_AfterPostProcessTexture, _CameraColorTarget)

优势：
  ✅ Uber Shader：多个效果一个Pass完成
  ✅ 减少RT切换
  ✅ 降采样：Bloom等使用Mipmap链
  ✅ 动态编译：只包含启用的效果

输出：
  ✅ 后处理后的最终颜色

统计：
  SetPass: 1个（Uber Shader）
  DrawCall: 1个（全屏Quad）
  时间：2-5ms（取决于启用的效果）
```

---

## 🔵 阶段9：UI渲染（Canvas）

```
[GPU] RenderPass: DrawUIPass

目的：渲染UI元素

模式：Screen Space - Overlay（最常用）

SetRenderTarget(FinalFrameBuffer)
DisableDepthTest()  // ⭐ UI不需要深度测试

┌─────────────────────────────────────────────────┐
│ 按Canvas Sort Order和Hierarchy排序               │
│                                                  │
│ for each Canvas:                                 │
│   for each CanvasRenderer:                      │
│     if (renderer.cull == false):                │
│       DrawMesh(uiMesh, uiMaterial)              │
└─────────────────────────────────────────────────┘

批处理规则：
  ✅ 相同材质和纹理 → 批处理
  ❌ 不同材质 → 打断批处理
  ❌ Mask组件 → 打断批处理
  ❌ 改变层级 → 打断批处理

输出：
  ✅ UI直接渲染到最终framebuffer
  ✅ 不受后处理影响

统计：
  SetPass: N个（取决于UI材质数量）
  DrawCall: M个（取决于批处理情况）
  时间：0.5-2ms
```

---

## 📊 完整流程总结

### 场景统计

```
场景配置：
  - 3个不透明物体（2个用材质A，1个用材质B）
  - 2个透明物体（不同材质）
  - 1个方向光（主光源，阴影）
  - 2个点光源（附加光源）

完整Pass流程：
┌────┬──────────────────────┬─────────┬──────────┬────────┐
│阶段│ Pass名称              │ SetPass │ DrawCall │ 时间   │
├────┼──────────────────────┼─────────┼──────────┼────────┤
│ 0  │ CPU准备              │ -       │ -        │ 0.5ms  │
│ 1  │ Setup                │ -       │ -        │ <0.1ms │
│ 2  │ Shadow Pass          │ 4       │ 5        │ 0.8ms  │
│ 3  │ Depth Prepass        │ 2       │ 3        │ 0.5ms  │
│ 4  │ UniversalForward⭐   │ 2       │ 3        │ 3.0ms  │
│ 5  │ Skybox               │ 1       │ 1        │ 0.3ms  │
│ 6  │ Copy Color           │ 1       │ 1        │ 0.5ms  │
│ 7  │ Transparent          │ 2       │ 2        │ 1.5ms  │
│ 8  │ Post-Processing      │ 1       │ 1        │ 2.5ms  │
│ 9  │ UI                   │ 2       │ 3        │ 1.0ms  │
├────┼──────────────────────┼─────────┼──────────┼────────┤
│总计│                      │ 15      │ 19       │ 10.2ms │
└────┴──────────────────────┴─────────┴──────────┴────────┘

FPS：约98帧（1000ms / 10.2ms）
```

### 与Built-in Forward对比

```
相同场景，Built-in Forward：

┌────┬──────────────────────┬─────────┬──────────┬────────┐
│阶段│ Pass名称              │ SetPass │ DrawCall │ 时间   │
├────┼──────────────────────┼─────────┼──────────┼────────┤
│ 2  │ Shadow Pass          │ 4       │ 5        │ 0.8ms  │
│ 4  │ ForwardBase          │ 2       │ 3        │ 1.5ms  │
│ 4  │ ForwardAdd (光源1)   │ 2       │ 3        │ 1.5ms  │
│ 4  │ ForwardAdd (光源2)   │ 2       │ 3        │ 1.5ms  │
│ 5  │ Skybox               │ 1       │ 1        │ 0.3ms  │
│ 7  │ Transparent Base     │ 2       │ 2        │ 0.8ms  │
│ 7  │ Transparent Add×2    │ 4       │ 4        │ 1.6ms  │
│ 8  │ Post-Processing      │ 3       │ 3        │ 3.0ms  │
│ 9  │ UI                   │ 2       │ 3        │ 1.0ms  │
├────┼──────────────────────┼─────────┼──────────┼────────┤
│总计│                      │ 22      │ 27       │ 12.0ms │
└────┴──────────────────────┴─────────┴──────────┴────────┘

FPS：约83帧

性能提升：
  SetPass减少：31% ✅
  DrawCall减少：30% ✅
  时间减少：15% ✅
```

---

## 🎯 核心要点总结

### 1. 关于"渲染不透明物体"和"UniversalForward Pass"

```
它们是同一个东西！

概念层次：
  渲染阶段：DrawOpaqueObjects（渲染不透明物体）
    ↓
  实现方式：使用Shader的UniversalForward Pass
    ↓
  Shader标识：LightMode="UniversalForward"
    ↓
  光照计算：在Pass的片元着色器中完成
```

### 2. 光照计算位置

```
⭐ 光照计算在UniversalForward Pass的片元着色器中！

half4 frag(Varyings input) : SV_Target
{
    // 1. 计算主光源
    color += CalculateMainLight();
    
    // 2. 循环计算附加光源
    for (int i = 0; i < lightCount; i++)
    {
        color += CalculateAdditionalLight(i);
    }
    
    // 3. 环境光和GI
    color += GlobalIllumination();
    
    return color;
}

✅ 所有光照在一个Pass中完成
✅ 不需要像Built-in那样多个Pass
✅ GPU并行计算，效率更高
```

### 3. URP的核心优势

```
Built-in Forward：
  主光源：ForwardBase Pass
  光源1：ForwardAdd Pass
  光源2：ForwardAdd Pass
  → 3个Pass，9个DrawCall（3物体×3Pass）

URP Forward：
  所有光源：UniversalForward Pass（一个！）
    内部循环处理所有光源
  → 1个Pass，3个DrawCall（3物体×1Pass）

性能提升：
  ✅ Pass数量：1 vs 3
  ✅ SetPass Call：减少67%
  ✅ DrawCall：减少67%
  ✅ CPU开销：大幅降低
```

### 4. 透明物体

```
⭐ 透明物体也使用UniversalForward Pass！

和不透明物体的区别：
  - 渲染时机：在不透明物体之后
  - 排序：从后到前（保证正确混合）
  - ZWrite：Off（不写深度）
  - Blend：Alpha混合
  - 光照计算：完全一样！
```

---

希望这个超详细的文档能帮您彻底理解URP的Pass流程！✨
