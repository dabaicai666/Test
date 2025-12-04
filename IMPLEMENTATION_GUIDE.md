# Xreal 手势检测实现指南

## 已完成的工作

我已经为您创建了一个完整的手势检测系统，基于 XR Hands 1.4.3 API。以下是所有创建的文件：

### 核心代码文件

1. **HandGestureDetector.cs** - 主要的手势检测器类
   - 位置：`/workspace/CustomPackage/Runtime/HandGestureDetector.cs`
   - 功能：检测 6 种常见手势（Fist、Grab、Pinch、OpenHand、ThumbsUp、Point）
   - 提供事件系统和查询接口

2. **HandGestureExample.cs** - 使用示例脚本
   - 位置：`/workspace/CustomPackage/Runtime/HandGestureExample.cs`
   - 功能：展示如何使用手势检测器
   - 包含各种手势的处理示例

3. **com.migu.test.asmdef** - Assembly Definition
   - 已更新为包含 Unity.XR.Hands 依赖
   - 命名空间：XrealHandGesture

### 文档文件

1. **README.md** - 项目说明（英文版）
2. **使用说明.md** - 详细使用文档（中文版，包含完整示例）
3. **快速开始.md** - 5分钟快速入门指南
4. **项目说明.md** - 完整的项目技术文档

### 配置文件

1. **package.json** - Unity 包配置，已更新为手势检测包信息

## 如何使用

### 方法1：使用事件系统（推荐）

```csharp
using UnityEngine;
using UnityEngine.XR.Hands;
using XrealHandGesture;

public class MyGestureHandler : MonoBehaviour
{
    private HandGestureDetector gestureDetector;

    void Start()
    {
        // 获取或添加组件
        gestureDetector = GetComponent<HandGestureDetector>();
        
        // 订阅事件
        gestureDetector.OnGestureDetected += OnGestureDetected;
        gestureDetector.OnGestureEnded += OnGestureEnded;
    }

    void OnDestroy()
    {
        // 取消订阅
        if (gestureDetector != null)
        {
            gestureDetector.OnGestureDetected -= OnGestureDetected;
            gestureDetector.OnGestureEnded -= OnGestureEnded;
        }
    }

    private void OnGestureDetected(GestureType gesture, Handedness hand)
    {
        switch (gesture)
        {
            case GestureType.Fist:
                Debug.Log($"{hand} 手做了拳头手势");
                break;
            case GestureType.Grab:
                Debug.Log($"{hand} 手做了抓取手势");
                break;
            case GestureType.Pinch:
                Debug.Log($"{hand} 手做了捏合手势");
                break;
            // ... 其他手势
        }
    }

    private void OnGestureEnded(GestureType gesture, Handedness hand)
    {
        Debug.Log($"{hand} 手的 {gesture} 手势结束");
    }
}
```

### 方法2：主动查询

```csharp
using UnityEngine;
using XrealHandGesture;

public class GestureQuery : MonoBehaviour
{
    private HandGestureDetector gestureDetector;

    void Start()
    {
        gestureDetector = GetComponent<HandGestureDetector>();
    }

    void Update()
    {
        // 查询当前手势
        GestureType leftGesture = gestureDetector.GetCurrentLeftHandGesture();
        GestureType rightGesture = gestureDetector.GetCurrentRightHandGesture();

        // 检查特定手势是否激活
        if (gestureDetector.IsGestureActive(GestureType.Pinch, Handedness.Right))
        {
            // 右手正在捏合
        }
    }
}
```

## 支持的手势

| 手势 | 检测条件 | 常见用途 |
|-----|---------|---------|
| **Fist** | 所有手指弯曲 | 攻击、确认 |
| **Grab** | 四指弯曲 | 抓取物体 |
| **Pinch** | 拇指食指接近 | 精确选择、UI点击 |
| **OpenHand** | 所有手指伸直 | 释放物体、打开菜单 |
| **ThumbsUp** | 拇指伸直其他弯曲 | 点赞、确认 |
| **Point** | 食指伸直其他弯曲 | 指示、射击 |

## 实现原理

### 手势检测流程

1. **获取手部数据**
   ```csharp
   var leftHand = handSubsystem.leftHand;
   var rightHand = handSubsystem.rightHand;
   ```

2. **获取关节姿态**
   ```csharp
   hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose);
   ```

3. **计算手指弯曲度**
   - 使用关节位置计算向量
   - 通过点积判断弯曲角度
   - 归一化到 0-1 范围

4. **判断手势**
   - 按优先级检测各种手势
   - 返回匹配的手势类型

### 核心算法

#### 手指弯曲度计算

```csharp
private float CalculateFingerCurl(XRHand hand, XRHandFingerID fingerID)
{
    // 获取指尖、中间和基部关节
    var tip = GetFingerTip(fingerID);
    var middle = GetFingerMiddle(fingerID);
    var base = GetFingerBase(fingerID);
    
    // 计算向量并使用点积判断弯曲
    Vector3 baseToMiddle = (middle.position - base.position).normalized;
    Vector3 middleToTip = (tip.position - middle.position).normalized;
    float dotProduct = Vector3.Dot(baseToMiddle, middleToTip);
    
    // 转换为 0-1 的弯曲度
    return (1f - dotProduct) / 2f;
}
```

#### 捏合检测

```csharp
private bool IsPinch(XRHand hand)
{
    // 获取拇指和食指指尖位置
    hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose);
    hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose);
    
    // 计算距离
    float distance = Vector3.Distance(thumbPose.position, indexPose.position);
    
    // 小于阈值则判定为捏合
    return distance < pinchThreshold;
}
```

## API 完整列表

### HandGestureDetector 类

#### 公共属性（Inspector可调）
- `bool detectLeftHand` - 是否检测左手
- `bool detectRightHand` - 是否检测右手
- `float curlThreshold` - 弯曲阈值（默认0.7）
- `float pinchThreshold` - 捏合阈值（默认0.03）
- `float straightThreshold` - 伸直阈值（默认0.5）

#### 事件
- `event Action<GestureType, Handedness> OnGestureDetected` - 手势检测到时触发
- `event Action<GestureType, Handedness> OnGestureEnded` - 手势结束时触发

#### 公共方法
- `GestureType GetCurrentLeftHandGesture()` - 获取当前左手手势
- `GestureType GetCurrentRightHandGesture()` - 获取当前右手手势
- `bool IsGestureActive(GestureType, Handedness)` - 检查手势是否激活

### GestureType 枚举
- `None` - 无手势
- `Fist` - 拳头
- `Grab` - 抓取
- `Pinch` - 捏合
- `OpenHand` - 张开手掌
- `ThumbsUp` - 竖大拇指
- `Point` - 指向

## 集成步骤

### Unity 中使用

1. **导入包**
   - 将 CustomPackage 文件夹放入项目

2. **添加组件**
   - 在场景中创建空物体
   - 添加 `HandGestureDetector` 组件

3. **创建控制脚本**
   - 创建自己的脚本
   - 获取 `HandGestureDetector` 组件
   - 订阅事件或查询手势状态

4. **测试**
   - 在 Xreal 设备上运行
   - 做出不同手势进行测试

### 与 Xreal SDK 配合

```csharp
using UnityEngine;
using UnityEngine.XR.Hands;
using XrealHandGesture;

// 假设 Xreal SDK 提供了手部追踪功能
public class XrealGestureIntegration : MonoBehaviour
{
    private HandGestureDetector gestureDetector;

    void Start()
    {
        gestureDetector = GetComponent<HandGestureDetector>();
        gestureDetector.OnGestureDetected += OnGesture;
    }

    private void OnGesture(GestureType gesture, Handedness hand)
    {
        // 与 Xreal SDK 的其他功能配合
        // 例如：手势控制虚拟对象、UI交互等
    }
}
```

## 性能优化建议

1. **按需检测**
   - 如果只需要单手，关闭另一只手的检测
   - 在 Inspector 中取消勾选不需要的手

2. **使用事件而非轮询**
   - 优先使用 `OnGestureDetected` 事件
   - 避免在 Update 中频繁调用 `GetCurrentXXXGesture()`

3. **合理的阈值**
   - 过低的阈值会导致误触发
   - 过高的阈值会导致识别不灵敏
   - 建议在实际设备上测试并调整

## 扩展开发

### 添加新手势

1. 在 `GestureType` 枚举中添加新类型
2. 实现检测方法（参考现有手势）
3. 在 `DetectGesture()` 中添加检测逻辑

示例：添加"V"手势

```csharp
// 1. 添加到枚举
public enum GestureType
{
    // ... 现有手势
    Victory  // V手势
}

// 2. 实现检测方法
private bool IsVictory(XRHand hand)
{
    return IsFingerStraight(hand, XRHandFingerID.Index) &&
           IsFingerStraight(hand, XRHandFingerID.Middle) &&
           IsFingerCurled(hand, XRHandFingerID.Ring) &&
           IsFingerCurled(hand, XRHandFingerID.Little) &&
           IsFingerCurled(hand, XRHandFingerID.Thumb);
}

// 3. 添加到检测流程
private GestureType DetectGesture(XRHand hand)
{
    if (IsFist(hand)) return GestureType.Fist;
    if (IsVictory(hand)) return GestureType.Victory;  // 新手势
    // ... 其他手势
}
```

## 调试技巧

1. **启用日志**
   - 手势检测器会自动输出 Debug.Log
   - 查看 Console 了解实时状态

2. **可视化手部**
   - 使用 XR Hands 的 DebugUI 组件
   - 可以看到关节追踪情况

3. **参数调试**
   - 在 Inspector 中实时调整参数
   - 观察识别效果的变化

## 常见问题

### Q: 为什么手势检测不工作？
A: 检查：
1. XR Hands 包是否正确安装
2. 设备是否支持手部追踪
3. Console 是否有 "Hand Subsystem 已初始化" 日志

### Q: 如何提高识别准确率？
A: 
1. 调整 Inspector 中的阈值参数
2. 确保手部在追踪范围内
3. 确保光线条件良好
4. 手势动作要标准清晰

### Q: 可以同时检测多个手势吗？
A: 系统采用优先级检测，同一时刻每只手只会识别一个手势。这样设计是为了避免手势冲突。

## 下一步

1. 查看 `快速开始.md` 快速上手
2. 阅读 `使用说明.md` 了解详细用法
3. 参考 `HandGestureExample.cs` 学习示例代码
4. 根据您的项目需求进行定制

---

祝您开发顺利！如有问题，请参考详细文档或联系技术支持。
