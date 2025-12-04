# Xreal 手势检测系统

基于 XR Hands 1.4.3 的手势检测解决方案，适用于 Xreal SDK 3.1.0。

## 功能特性

支持以下常见手势检测：

- **Fist (拳头)** - 所有手指弯曲
- **Grab (抓取)** - 四指弯曲，类似抓取动作
- **Pinch (捏合)** - 大拇指和食指尖接近
- **OpenHand (张开手掌)** - 所有手指伸直
- **ThumbsUp (竖大拇指)** - 大拇指伸直，其他手指弯曲
- **Point (指向)** - 食指伸直，其他手指弯曲

## 依赖要求

- Unity XR Hands: 1.4.3
- Unity XR Interaction Toolkit: 2.6.5
- Xreal XR Plugin: 3.0.0
- Xreal SDK: 3.1.0

## 快速开始

### 1. 基本使用

在场景中创建一个空物体，添加 `HandGestureDetector` 组件：

```csharp
using UnityEngine;
using XrealHandGesture;

public class MyGestureHandler : MonoBehaviour
{
    private HandGestureDetector gestureDetector;

    void Start()
    {
        gestureDetector = GetComponent<HandGestureDetector>();
        
        // 订阅手势事件
        gestureDetector.OnGestureDetected += OnGestureDetected;
        gestureDetector.OnGestureEnded += OnGestureEnded;
    }

    private void OnGestureDetected(GestureType gesture, Handedness hand)
    {
        Debug.Log($"检测到手势: {gesture} - {hand}");
        
        // 处理特定手势
        if (gesture == GestureType.Fist && hand == Handedness.Right)
        {
            // 右手拳头 - 执行攻击
            PerformAttack();
        }
    }

    private void OnGestureEnded(GestureType gesture, Handedness hand)
    {
        Debug.Log($"手势结束: {gesture}");
    }
}
```

### 2. 主动查询手势

```csharp
void Update()
{
    // 获取当前手势
    GestureType leftGesture = gestureDetector.GetCurrentLeftHandGesture();
    GestureType rightGesture = gestureDetector.GetCurrentRightHandGesture();
    
    // 检查特定手势是否激活
    if (gestureDetector.IsGestureActive(GestureType.Pinch, Handedness.Right))
    {
        // 右手正在执行捏合手势
        // 持续执行某些操作
    }
}
```

### 3. 参数调整

在 Inspector 中可以调整以下参数：

- **Detect Left Hand** - 是否检测左手
- **Detect Right Hand** - 是否检测右手
- **Curl Threshold** - 手指弯曲阈值（0-1，默认0.7）
- **Pinch Threshold** - 捏合距离阈值（米，默认0.03）
- **Straight Threshold** - 手指伸直阈值（0-1，默认0.5）

## API 参考

### HandGestureDetector 类

#### 事件

```csharp
// 手势检测到时触发
public event Action<GestureType, Handedness> OnGestureDetected;

// 手势结束时触发
public event Action<GestureType, Handedness> OnGestureEnded;
```

#### 方法

```csharp
// 获取当前左手手势
public GestureType GetCurrentLeftHandGesture()

// 获取当前右手手势
public GestureType GetCurrentRightHandGesture()

// 检查指定手势是否正在执行
public bool IsGestureActive(GestureType gestureType, Handedness handedness)
```

### GestureType 枚举

```csharp
public enum GestureType
{
    None,           // 无手势
    Fist,           // 拳头
    Grab,           // 抓取
    Pinch,          // 捏合
    OpenHand,       // 张开手掌
    ThumbsUp,       // 竖大拇指
    Point           // 指向
}
```

## 使用场景示例

### 1. 物体抓取

```csharp
void Update()
{
    if (gestureDetector.IsGestureActive(GestureType.Grab, Handedness.Right))
    {
        // 抓取附近的物体
        TryGrabNearbyObject();
    }
}
```

### 2. UI交互

```csharp
private void OnGestureDetected(GestureType gesture, Handedness hand)
{
    if (gesture == GestureType.Pinch)
    {
        // 使用射线检测选择UI元素
        RaycastAndSelectUI();
    }
}
```

### 3. 菜单控制

```csharp
private void OnGestureDetected(GestureType gesture, Handedness hand)
{
    switch (gesture)
    {
        case GestureType.OpenHand:
            // 打开菜单
            OpenMenu();
            break;
        case GestureType.Fist:
            // 关闭菜单
            CloseMenu();
            break;
        case GestureType.ThumbsUp:
            // 确认选择
            ConfirmSelection();
            break;
    }
}
```

## 注意事项

1. 确保场景中已正确配置 XR Hand Subsystem
2. 手势检测需要设备支持手部追踪功能
3. 在设备上测试时，确保手部在追踪范围内
4. 可以根据实际需求调整阈值参数以获得最佳检测效果

## 技术支持

如有问题或建议，请联系技术支持团队。
