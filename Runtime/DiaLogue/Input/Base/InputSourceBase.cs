using NiumaGal.Dialogue.Config.Core;
using NiumaGal.Dialogue.Input.Data;
using UnityEngine;

namespace NiumaGal.Dialogue.Input.Base
{
    /// <summary>
    /// 输入源基类
    /// 提供统一的序列化接口 支持在 Unity 编辑器中拖拽赋值
    /// 所有具体输入源都应继承此类
    /// </summary>
    public abstract class InputSourceBase :  MonoBehaviour,IInputSource
    {
        [Header("输入配置引用（可选，未赋值则使用下方默认值）")]
        public DialogueInputSO InputConfig;

         // 运行时实际使用的值
        public float ActionBufferTime => InputConfig != null ? InputConfig.ActionBufferTime : _defaultActionBufferTime;
        public float FastForwardThreshold => InputConfig != null ? InputConfig.FastForwardThreshold : _defaultFastForwardThreshold;

        [Header("输入缓冲设置")]
        [SerializeField,Tooltip("推进键的防抖缓存时间(秒),用于抖动抑制")]
         private float _defaultActionBufferTime = 0.2f;
        [Header("快进设置")]
        [SerializeField,Tooltip("长按推进触发快进的时间阈值(秒)")]
        private float _defaultFastForwardThreshold = 0.5f;

        /// <summary>
        /// 是否被阻塞（由外部系统如菜单、暂停设置）
        /// </summary>
        public bool IsBlocked { get; set; }

        public abstract void FetchRawInput(ref GalRawInputData rawData);
    }
}