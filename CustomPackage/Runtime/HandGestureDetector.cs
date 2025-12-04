using System;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace XrealHandGesture
{
    /// <summary>
    /// 手势类型枚举
    /// </summary>
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

    /// <summary>
    /// 手势检测器 - 用于检测XR Hands的常见手势
    /// </summary>
    public class HandGestureDetector : MonoBehaviour
    {
        [Header("手势设置")]
        [SerializeField] private bool detectLeftHand = true;
        [SerializeField] private bool detectRightHand = true;
        
        [Header("手势阈值")]
        [SerializeField] private float curlThreshold = 0.7f;        // 弯曲阈值
        [SerializeField] private float pinchThreshold = 0.03f;      // 捏合距离阈值
        [SerializeField] private float straightThreshold = 0.5f;    // 伸直阈值
        
        // 手势事件
        public event Action<GestureType, Handedness> OnGestureDetected;
        public event Action<GestureType, Handedness> OnGestureEnded;
        
        private XRHandSubsystem handSubsystem;
        private GestureType currentLeftGesture = GestureType.None;
        private GestureType currentRightGesture = GestureType.None;

        private void Start()
        {
            // 获取XRHandSubsystem
            var handSubsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(handSubsystems);
            
            if (handSubsystems.Count > 0)
            {
                handSubsystem = handSubsystems[0];
                Debug.Log("Hand Subsystem 已初始化");
            }
            else
            {
                Debug.LogError("未找到 XRHandSubsystem!");
            }
        }

        private void Update()
        {
            if (handSubsystem == null) return;

            // 检测左手手势
            if (detectLeftHand)
            {
                var leftHand = handSubsystem.leftHand;
                if (leftHand.isTracked)
                {
                    GestureType detectedGesture = DetectGesture(leftHand);
                    UpdateGestureState(detectedGesture, Handedness.Left, ref currentLeftGesture);
                }
            }

            // 检测右手手势
            if (detectRightHand)
            {
                var rightHand = handSubsystem.rightHand;
                if (rightHand.isTracked)
                {
                    GestureType detectedGesture = DetectGesture(rightHand);
                    UpdateGestureState(detectedGesture, Handedness.Right, ref currentRightGesture);
                }
            }
        }

        /// <summary>
        /// 检测手势
        /// </summary>
        private GestureType DetectGesture(XRHand hand)
        {
            // 按优先级检测手势
            if (IsFist(hand)) return GestureType.Fist;
            if (IsGrab(hand)) return GestureType.Grab;
            if (IsPinch(hand)) return GestureType.Pinch;
            if (IsThumbsUp(hand)) return GestureType.ThumbsUp;
            if (IsPoint(hand)) return GestureType.Point;
            if (IsOpenHand(hand)) return GestureType.OpenHand;
            
            return GestureType.None;
        }

        /// <summary>
        /// 更新手势状态并触发事件
        /// </summary>
        private void UpdateGestureState(GestureType newGesture, Handedness handedness, ref GestureType currentGesture)
        {
            if (newGesture != currentGesture)
            {
                // 手势结束
                if (currentGesture != GestureType.None)
                {
                    OnGestureEnded?.Invoke(currentGesture, handedness);
                    Debug.Log($"[{handedness}] 手势结束: {currentGesture}");
                }
                
                // 新手势开始
                if (newGesture != GestureType.None)
                {
                    OnGestureDetected?.Invoke(newGesture, handedness);
                    Debug.Log($"[{handedness}] 检测到手势: {newGesture}");
                }
                
                currentGesture = newGesture;
            }
        }

        #region 手势检测方法

        /// <summary>
        /// 检测拳头手势 - 所有手指都弯曲
        /// </summary>
        private bool IsFist(XRHand hand)
        {
            return IsFingerCurled(hand, XRHandFingerID.Index) &&
                   IsFingerCurled(hand, XRHandFingerID.Middle) &&
                   IsFingerCurled(hand, XRHandFingerID.Ring) &&
                   IsFingerCurled(hand, XRHandFingerID.Little) &&
                   IsFingerCurled(hand, XRHandFingerID.Thumb);
        }

        /// <summary>
        /// 检测抓取手势 - 类似拳头但大拇指可以不完全弯曲
        /// </summary>
        private bool IsGrab(XRHand hand)
        {
            return IsFingerCurled(hand, XRHandFingerID.Index) &&
                   IsFingerCurled(hand, XRHandFingerID.Middle) &&
                   IsFingerCurled(hand, XRHandFingerID.Ring) &&
                   IsFingerCurled(hand, XRHandFingerID.Little);
        }

        /// <summary>
        /// 检测捏合手势 - 大拇指和食指尖接近
        /// </summary>
        private bool IsPinch(XRHand hand)
        {
            if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose) &&
                hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose))
            {
                float distance = Vector3.Distance(thumbPose.position, indexPose.position);
                return distance < pinchThreshold;
            }
            return false;
        }

        /// <summary>
        /// 检测张开手掌 - 所有手指都伸直
        /// </summary>
        private bool IsOpenHand(XRHand hand)
        {
            return IsFingerStraight(hand, XRHandFingerID.Index) &&
                   IsFingerStraight(hand, XRHandFingerID.Middle) &&
                   IsFingerStraight(hand, XRHandFingerID.Ring) &&
                   IsFingerStraight(hand, XRHandFingerID.Little) &&
                   IsFingerStraight(hand, XRHandFingerID.Thumb);
        }

        /// <summary>
        /// 检测竖大拇指 - 大拇指伸直，其他手指弯曲
        /// </summary>
        private bool IsThumbsUp(XRHand hand)
        {
            return IsFingerStraight(hand, XRHandFingerID.Thumb) &&
                   IsFingerCurled(hand, XRHandFingerID.Index) &&
                   IsFingerCurled(hand, XRHandFingerID.Middle) &&
                   IsFingerCurled(hand, XRHandFingerID.Ring) &&
                   IsFingerCurled(hand, XRHandFingerID.Little);
        }

        /// <summary>
        /// 检测指向手势 - 食指伸直，其他手指弯曲
        /// </summary>
        private bool IsPoint(XRHand hand)
        {
            return IsFingerStraight(hand, XRHandFingerID.Index) &&
                   IsFingerCurled(hand, XRHandFingerID.Middle) &&
                   IsFingerCurled(hand, XRHandFingerID.Ring) &&
                   IsFingerCurled(hand, XRHandFingerID.Little);
        }

        #endregion

        #region 手指状态检测辅助方法

        /// <summary>
        /// 检测手指是否弯曲
        /// </summary>
        private bool IsFingerCurled(XRHand hand, XRHandFingerID fingerID)
        {
            float curl = CalculateFingerCurl(hand, fingerID);
            return curl > curlThreshold;
        }

        /// <summary>
        /// 检测手指是否伸直
        /// </summary>
        private bool IsFingerStraight(XRHand hand, XRHandFingerID fingerID)
        {
            float curl = CalculateFingerCurl(hand, fingerID);
            return curl < straightThreshold;
        }

        /// <summary>
        /// 计算手指弯曲度 (0=伸直, 1=完全弯曲)
        /// </summary>
        private float CalculateFingerCurl(XRHand hand, XRHandFingerID fingerID)
        {
            // 获取手指关节
            XRHandJointID tipJoint = GetFingerTip(fingerID);
            XRHandJointID middleJoint = GetFingerMiddle(fingerID);
            XRHandJointID baseJoint = GetFingerBase(fingerID);

            if (hand.GetJoint(tipJoint).TryGetPose(out var tipPose) &&
                hand.GetJoint(middleJoint).TryGetPose(out var middlePose) &&
                hand.GetJoint(baseJoint).TryGetPose(out var basePose))
            {
                // 计算指尖到基部的距离
                float extendedDistance = Vector3.Distance(basePose.position, tipPose.position);
                
                // 计算中间关节的弯曲程度
                Vector3 baseToMiddle = (middlePose.position - basePose.position).normalized;
                Vector3 middleToTip = (tipPose.position - middlePose.position).normalized;
                float dotProduct = Vector3.Dot(baseToMiddle, middleToTip);
                
                // 转换为0-1的弯曲度值（1-dotProduct）
                // dotProduct接近1表示伸直，接近-1表示弯曲
                float curl = (1f - dotProduct) / 2f;
                
                return curl;
            }

            return 0f;
        }

        /// <summary>
        /// 获取手指指尖关节ID
        /// </summary>
        private XRHandJointID GetFingerTip(XRHandFingerID fingerID)
        {
            return fingerID switch
            {
                XRHandFingerID.Thumb => XRHandJointID.ThumbTip,
                XRHandFingerID.Index => XRHandJointID.IndexTip,
                XRHandFingerID.Middle => XRHandJointID.MiddleTip,
                XRHandFingerID.Ring => XRHandJointID.RingTip,
                XRHandFingerID.Little => XRHandJointID.LittleTip,
                _ => XRHandJointID.IndexTip
            };
        }

        /// <summary>
        /// 获取手指中间关节ID
        /// </summary>
        private XRHandJointID GetFingerMiddle(XRHandFingerID fingerID)
        {
            return fingerID switch
            {
                XRHandFingerID.Thumb => XRHandJointID.ThumbDistal,
                XRHandFingerID.Index => XRHandJointID.IndexIntermediate,
                XRHandFingerID.Middle => XRHandJointID.MiddleIntermediate,
                XRHandFingerID.Ring => XRHandJointID.RingIntermediate,
                XRHandFingerID.Little => XRHandJointID.LittleIntermediate,
                _ => XRHandJointID.IndexIntermediate
            };
        }

        /// <summary>
        /// 获取手指基部关节ID
        /// </summary>
        private XRHandJointID GetFingerBase(XRHandFingerID fingerID)
        {
            return fingerID switch
            {
                XRHandFingerID.Thumb => XRHandJointID.ThumbMetacarpal,
                XRHandFingerID.Index => XRHandJointID.IndexMetacarpal,
                XRHandFingerID.Middle => XRHandJointID.MiddleMetacarpal,
                XRHandFingerID.Ring => XRHandJointID.RingMetacarpal,
                XRHandFingerID.Little => XRHandJointID.LittleMetacarpal,
                _ => XRHandJointID.IndexMetacarpal
            };
        }

        #endregion

        #region 公共API

        /// <summary>
        /// 获取当前左手手势
        /// </summary>
        public GestureType GetCurrentLeftHandGesture()
        {
            return currentLeftGesture;
        }

        /// <summary>
        /// 获取当前右手手势
        /// </summary>
        public GestureType GetCurrentRightHandGesture()
        {
            return currentRightGesture;
        }

        /// <summary>
        /// 检查指定手势是否正在执行
        /// </summary>
        public bool IsGestureActive(GestureType gestureType, Handedness handedness)
        {
            if (handedness == Handedness.Left)
                return currentLeftGesture == gestureType;
            else
                return currentRightGesture == gestureType;
        }

        #endregion
    }
}
