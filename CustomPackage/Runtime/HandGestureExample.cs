using UnityEngine;
using UnityEngine.XR.Hands;

namespace XrealHandGesture
{
    /// <summary>
    /// 手势检测使用示例
    /// </summary>
    public class HandGestureExample : MonoBehaviour
    {
        private HandGestureDetector gestureDetector;

        private void Start()
        {
            // 获取或添加手势检测器组件
            gestureDetector = GetComponent<HandGestureDetector>();
            if (gestureDetector == null)
            {
                gestureDetector = gameObject.AddComponent<HandGestureDetector>();
            }

            // 订阅手势事件
            gestureDetector.OnGestureDetected += OnGestureDetected;
            gestureDetector.OnGestureEnded += OnGestureEnded;
        }

        private void OnDestroy()
        {
            // 取消订阅
            if (gestureDetector != null)
            {
                gestureDetector.OnGestureDetected -= OnGestureDetected;
                gestureDetector.OnGestureEnded -= OnGestureEnded;
            }
        }

        /// <summary>
        /// 手势检测到时的回调
        /// </summary>
        private void OnGestureDetected(GestureType gesture, Handedness hand)
        {
            Debug.Log($"检测到手势: {gesture} - 手: {hand}");

            // 根据不同手势执行不同操作
            switch (gesture)
            {
                case GestureType.Fist:
                    OnFistDetected(hand);
                    break;
                case GestureType.Grab:
                    OnGrabDetected(hand);
                    break;
                case GestureType.Pinch:
                    OnPinchDetected(hand);
                    break;
                case GestureType.OpenHand:
                    OnOpenHandDetected(hand);
                    break;
                case GestureType.ThumbsUp:
                    OnThumbsUpDetected(hand);
                    break;
                case GestureType.Point:
                    OnPointDetected(hand);
                    break;
            }
        }

        /// <summary>
        /// 手势结束时的回调
        /// </summary>
        private void OnGestureEnded(GestureType gesture, Handedness hand)
        {
            Debug.Log($"手势结束: {gesture} - 手: {hand}");
        }

        #region 各种手势的处理方法

        private void OnFistDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 拳头手势 - 可以用于攻击、确认等操作");
            // 在这里添加你的逻辑
        }

        private void OnGrabDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 抓取手势 - 可以用于抓取物体");
            // 在这里添加你的逻辑
        }

        private void OnPinchDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 捏合手势 - 可以用于精确选择、UI交互");
            // 在这里添加你的逻辑
        }

        private void OnOpenHandDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 张开手掌 - 可以用于释放物体、展开菜单");
            // 在这里添加你的逻辑
        }

        private void OnThumbsUpDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 竖大拇指 - 可以用于点赞、确认");
            // 在这里添加你的逻辑
        }

        private void OnPointDetected(Handedness hand)
        {
            Debug.Log($"[{hand}] 指向手势 - 可以用于指示方向、射击");
            // 在这里添加你的逻辑
        }

        #endregion

        private void Update()
        {
            // 也可以在Update中主动查询当前手势
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GestureType leftGesture = gestureDetector.GetCurrentLeftHandGesture();
                GestureType rightGesture = gestureDetector.GetCurrentRightHandGesture();
                
                Debug.Log($"当前左手手势: {leftGesture}");
                Debug.Log($"当前右手手势: {rightGesture}");
            }

            // 检查特定手势是否激活
            if (gestureDetector.IsGestureActive(GestureType.Pinch, Handedness.Right))
            {
                // 右手正在执行捏合手势
                // 可以在这里持续执行某些操作
            }
        }
    }
}
