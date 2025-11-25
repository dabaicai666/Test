# Unity 渲染路径快速参考卡

## 🎯 一句话总结

**渲染路径 = Unity如何计算和组织光照的方式**

---

## 📋 渲染路径类型速查表

| 渲染路径 | 管线 | Pass数量 | 光源处理 | 适用平台 |
|---------|------|---------|---------|---------|
| **Forward** | Built-in | 1 Base + N Add | 每个光源一个Pass | 移动/PC |
| **Deferred** | Built-in | 1 Geom + 1 Light | 统一计算 | PC/主机 |
| **URP Forward** | URP | 1个 | 单Pass处理所有 | 移动/PC ⭐ |
| **URP Deferred** | URP | 1 GBuf + 1 Light | 统一计算 | PC |
| **HDRP Deferred** | HDRP | 复杂 | 高级算法 | 高端PC/主机 |

---

## ⚙️ 设置位置（3个层级）

### 1️⃣ 全局设置

```
Built-in:
Edit → Project Settings → Quality
  └─ Rendering Path: Forward / Deferred

URP:
选中 URP Asset → Inspector
  └─ Renderer List → 选择Renderer
     └─ Rendering Path: Forward / Deferred
```

### 2️⃣ 摄像机设置（Built-in可用）

```
选中Camera → Inspector → Rendering
  └─ Rendering Path: 
     • Use Graphics Settings（使用全局）
     • Forward（覆盖为Forward）
     • Deferred（覆盖为Deferred）
```

### 3️⃣ Shader Pass设置

```hlsl
// Built-in Forward
Pass { Tags { "LightMode"="ForwardBase" } }  // 主光源
Pass { Tags { "LightMode"="ForwardAdd" } }   // 附加光源

// Built-in Deferred
Pass { Tags { "LightMode"="Deferred" } }     // GBuffer

// URP Forward
Pass { Tags { "LightMode"="UniversalForward" } }  // 所有光源

// URP Deferred
Pass { Tags { "LightMode"="UniversalGBuffer" } }  // GBuffer
```

---

## 🔄 Pass执行流程对比

### Built-in Forward

```
物体1:
  └─ ForwardBase Pass（主光源）
  └─ ForwardAdd Pass（点光源1）
  └─ ForwardAdd Pass（点光源2）
  └─ ForwardAdd Pass（点光源3）

物体2:
  └─ ForwardBase Pass
  └─ ForwardAdd Pass × 3

DrawCall = 物体数 × (1 + 附加光源数)
```

### URP Forward ⭐

```
物体1:
  └─ UniversalForward Pass（所有光源一起）

物体2:
  └─ UniversalForward Pass（所有光源一起）

DrawCall = 物体数
```

### Deferred

```
第一阶段（几何）:
  物体1 → GBuffer
  物体2 → GBuffer

第二阶段（光照）:
  全屏Quad → 读取GBuffer → 应用所有光源 → 输出

DrawCall = 物体数 + 1
```

---

## 🎨 选择指南

### 移动平台 📱
```
✅ URP Forward
   - Per Object Light Limit: 2-4
   - Shadow Quality: Medium

原因：性能最优，功耗低
```

### PC（少量光源，1-8个）💻
```
✅ URP Forward
   - Per Object Light Limit: 8
   - Shadow Quality: High

原因：平衡性能和质量
```

### PC（大量光源，10+个）🖥️
```
✅ Deferred（Built-in 或 URP）
   - 不限制光源数量
   - 使用TAA抗锯齿

原因：光源数量对性能影响小
```

### 高端PC/主机 🎮
```
✅ HDRP Deferred
   - 最高质量设置
   - Ray Tracing

原因：最佳画质
```

### 特殊场景
```
大量透明物体 → Forward（任意管线）
VR/AR → URP Forward + Single Pass Instanced
```

---

## 📊 性能对比

### 场景：10个物体，5个光源

| 渲染路径 | SetPass | DrawCall | 内存 | 透明 | MSAA |
|---------|---------|----------|------|------|------|
| **Built-in Forward** | 10 | 60 | 低 | ✅ | ✅ |
| **Built-in Deferred** | 3 | 21 | 高 | ❌ | ❌ |
| **URP Forward** | 2 | 10 | 低 | ✅ | ✅ |
| **URP Deferred** | 3 | 21 | 高 | ❌ | ❌ |

**结论：URP Forward在大多数情况下最优！**

---

## 🔍 如何查看当前渲染路径

### 方法1：代码查看
```csharp
// Built-in
Camera camera = Camera.main;
Debug.Log(camera.actualRenderingPath);

// URP
var pipeline = GraphicsSettings.currentRenderPipeline;
Debug.Log(pipeline.GetType().Name);
```

### 方法2：Frame Debugger
```
Window → Analysis → Frame Debugger → Enable

查看Pass的LightMode标签：
- ForwardBase / ForwardAdd → Built-in Forward
- Deferred → Built-in Deferred
- UniversalForward → URP Forward
- UniversalGBuffer → URP Deferred
```

### 方法3：使用工具
```
Unity菜单 → Tools → 渲染路径设置工具
```

---

## ⚡ 优化技巧

### Forward渲染优化
```
1. 控制光源数量（移动端≤4，PC≤8）
2. 使用Baked Lighting（静态光照）
3. 降低不重要光源的Render Mode为Vertex
4. 使用Light Probes代替实时光源
```

### Deferred渲染优化
```
1. 减少透明物体（会回退到Forward）
2. 优化GBuffer格式
3. 使用Tile-Based Deferred（移动端）
4. 使用TAA代替MSAA抗锯齿
```

### URP通用优化
```
1. 启用SRP Batcher ✅
2. 使用GPU Instancing
3. 合并材质
4. 减少Shader变体
```

---

## 🐛 常见问题

### Q1: 为什么我的透明物体不显示阴影？
**A:** Deferred渲染不支持透明物体，透明物体会自动回退到Forward，需要单独处理阴影。

### Q2: 为什么切换到Deferred后MSAA不工作？
**A:** Deferred不支持MSAA，请使用TAA或FXAA抗锯齿。

### Q3: URP Forward为什么比Built-in Forward快？
**A:** URP在一个Pass中处理所有光源，而Built-in需要多个Pass，减少了状态切换。

### Q4: 光源数量超过Per Object Limit会怎样？
**A:** 超过的光源会降级为Per-Vertex光照或SH光照，质量降低但性能更好。

### Q5: 如何验证渲染路径设置成功？
**A:** 使用Frame Debugger查看Pass的LightMode，或运行诊断工具。

---

## 📚 相关文件

### 工具脚本
- `Assets/RenderingPathExplainer.cs` - 运行时查看器
- `Assets/Editor/RenderingPathSettingsTool.cs` - 设置工具

### Shader示例
- `Assets/Shaders/ForwardShaderExample.shader` - Forward示例
- `Assets/Shaders/URPForwardShaderExample.shader` - URP Forward示例

### 完整文档
- `README_RenderingPath.md` - 详细文档（40+页）

---

## 🎓 学习路径

```
1. 理解基本概念
   └─ 阅读 README_RenderingPath.md 前3章

2. 查看当前设置
   └─ Tools → 渲染路径设置工具

3. 实践操作
   └─ 创建测试场景，对比不同渲染路径

4. 学习Shader编写
   └─ 参考示例Shader，理解Pass结构

5. 性能优化
   └─ 使用Frame Debugger分析，针对性优化
```

---

## 💡 记忆口诀

```
Forward好理解，每灯一个Pass
Deferred先存储，统一算光照
URP Forward牛，单Pass全搞定
选择看平台，光源数和透明
```

---

## ✅ 快速检查清单

渲染路径配置检查：
- [ ] 确认使用的渲染管线（Built-in / URP / HDRP）
- [ ] 检查Quality Settings的渲染路径设置
- [ ] 检查摄像机的渲染路径设置（Built-in）
- [ ] 检查Renderer Asset的渲染路径（URP）
- [ ] 统计场景中的光源数量
- [ ] 统计透明物体数量
- [ ] 使用Frame Debugger验证Pass执行
- [ ] 测试目标平台性能

Shader编写检查：
- [ ] 正确设置LightMode标签
- [ ] 包含必要的编译指令
- [ ] Forward Add使用加法混合
- [ ] Deferred正确输出到GBuffer
- [ ] URP使用HLSL而不是CG

性能优化检查：
- [ ] 启用SRP Batcher（URP）
- [ ] 控制像素光源数量（Forward）
- [ ] 减少透明物体（Deferred）
- [ ] 使用合适的阴影质量
- [ ] 选择合适的抗锯齿方式

---

**提示：** 打开Unity后，运行 `Tools → 渲染路径设置工具` 获取详细的项目分析！
