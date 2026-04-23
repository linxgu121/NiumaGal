using NiumaGal.DiaLogue.Input.Data;
using UnityEngine;

namespace NiumaGal.DiaLogue.Input.Base
{
    /// <summary>
    /// 输入源基类
    /// 提供统一的序列化接口 支持在 Unity 编辑器中拖拽赋值
    /// 所有具体输入源都应继承此类
    /// </summary>
    public abstract class InputSourceBase :  MonoBehaviour,IInputSource
    {
        [Header("输入缓冲设置")]
        [Tooltip("推进键的防抖缓存时间(秒),用于抖动抑制")]
        public float ActionBufferTime = 0.2f;
        [Header("快进设置")]
        [Tooltip("长按推进触发快进的时间阈值(秒)")]
        public float FastForwardThreshold = 0.5f;

        /// <summary>
        /// 是否被阻塞（由外部系统如菜单、暂停设置）
        /// </summary>
        public bool IsBlocked { get; set; }

        public abstract void FetchRawInput(ref GalRawInputData rawData);
    }
}