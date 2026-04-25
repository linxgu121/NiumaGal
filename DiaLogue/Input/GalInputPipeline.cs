using NiumaGal.Dialogue.Input.Base;
using NiumaGal.Dialogue.Input.Data;
using UnityEngine;

namespace NiumaGal.Dialogue.Input
{
    /// <summary>
    /// 系统流转的第一道关卡 唯一的数据生产者
    /// 数据流向：从 InputSource 提取原始输入数据 - 栈上处理（长按检测/动作缓存） - 压入堆内存黑板（GalInputData）
    /// 拥有绝对的写入权限 对外部仅暴露只读引用 外部系统只能通过 Consume 接口进行受控的数据核销
    /// </summary>
    public class GalInputPipeline
    {
       private readonly InputSourceBase _inputSource;
       /// <summary>
       /// 堆内存容器
       /// </summary>
       private GalInputData _inputData;
       /// <summary>
       /// 栈上瞬时数据缓存
       /// </summary>
       private GalRawInputData _rawData;
       /// <summary>
       /// 长按计时器
       /// </summary>
       private float _advanceHoldTime;
       /// <summary>
       /// 全局物理帧计数器
       /// </summary>
       private ulong _frameIndex;

       /// <summary>
       /// 对外暴露的当前输入快照
       /// 其他系统只能读取此引用 绝对禁止直接修改内部字段
       /// </summary>
       public GalInputData Current => _inputData;

       /// <summary>
       /// 构造函数只接受 InputSourceBase 其他配置从 inputSource 注入
       /// 如果 inputSource 未配置则使用其字段的默认值
       /// </summary>
       public GalInputPipeline(InputSourceBase inputSource)
       {
           _inputSource = inputSource;

            // 管线作为数据的绝对源头 自行分配容器 避免GC
            _inputData = new GalInputData
            {
                currentFrameData = new GalFrameInputData { FrameIndex = 0 },
                lastFrameData = new GalFrameInputData { FrameIndex = 0 }
            };

            _rawData = default;
           _advanceHoldTime = 0f;
           _frameIndex = 0;
       }

       /// <summary>
        /// DialogueSystem Update 最优先调用的函数
        /// 负责历史快照更迭 并拉起一轮新的硬件数据采样
        /// </summary>
        public void Update()
        {
            // 推进历史帧
            _inputData.lastFrameData = _inputData.currentFrameData;

            if (_inputSource != null && _inputSource.IsBlocked)
            {
                _rawData = default;
            }
            else
            {
                // 采样硬件真实状态
                _inputSource.FetchRawInput(ref _rawData);
            }

            // 后处理数据并压入内存
            ProcessRawInput();

            _frameIndex++;
        }

        /// <summary>
        /// 输入数据的后处理方法
        /// </summary>
        private void ProcessRawInput()
        {
            var currentFrame = new GalFrameInputData
            {
                FrameIndex = _frameIndex,
                Raw = _rawData,
                Processed = default
            };

            float dt = Time.deltaTime;
            var lastProc = _inputData.lastFrameData.Processed;

            // === Advance 长按检测（FastForward）===
            if (_rawData.AdvancePressed)
            {
                _advanceHoldTime += dt;
                currentFrame.Processed.FastForwardActive = _advanceHoldTime >= _inputSource.FastForwardThreshold;
            }
            else
            {
                _advanceHoldTime = 0f;
                currentFrame.Processed.FastForwardActive = false;
            }

            //Advance 透传
            currentFrame.Processed.AdvanceHeld = _rawData.AdvancePressed;
            currentFrame.Processed.AdvanceJustPressed = _rawData.AdvanceJustPressed;

            //Advance 动作缓存池调度
            float UpdateBuffer(float lastTimer, bool justPressed)
            {
                float newTimer = Mathf.Max(0f, lastTimer - dt);
                if (justPressed) newTimer = _inputSource.ActionBufferTime;
                return newTimer;
            }

            currentFrame.Processed.AdvanceBufferTimer = UpdateBuffer(
                lastProc.AdvanceBufferTimer,
                _rawData.AdvanceJustPressed
            );

            // 其他命令直接透传（无缓存)
            currentFrame.Processed.SkipUnitJustPressed = _rawData.SkipUnitJustPressed;
            currentFrame.Processed.ToggleAutoJustPressed = _rawData.ToggleAutoJustPressed;
            currentFrame.Processed.MenuJustPressed = _rawData.MenuJustPressed;
            currentFrame.Processed.LogJustPressed = _rawData.LogJustPressed;
            currentFrame.Processed.HideUIJustPressed = _rawData.HideUIJustPressed;
            currentFrame.Processed.SaveJustPressed = _rawData.SaveJustPressed;
            currentFrame.Processed.LoadJustPressed = _rawData.LoadJustPressed;

            // 将局部计算完毕的纯净数据 一次性写回堆内存 供全局读取
            _inputData.currentFrameData = currentFrame;
        }

        /// <summary>
        /// 消费仲裁接口
        /// 仲裁器或 State 在动作确立时调用
        /// 调用后 Timer 瞬间归零 配合实现同帧内核
        /// </summary>

        public void ConsumeAdvancePressed()
        {
            var f = _inputData.currentFrameData;
            f.Processed.AdvanceBufferTimer = 0f;
            _inputData.currentFrameData = f;
        }

    }
}
