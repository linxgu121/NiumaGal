using NiumaGal.DiaLogue.Input.Data;

namespace NiumaGal.DiaLogue.Input.Base
{
    /// <summary>
    /// 输入源接口 所有输入来源都要实现接口
    /// 用于解耦输入采样逻辑 支持玩家输入、AI、菜单等多种源
    /// </summary>
    public interface IInputSource
    {
        /// <summary>
        /// 从输入源采样原始输入数据并写入结构体
        /// </summary>
        /// <param name="rawData">原始数据结构体</param>
        void FetchRawInput(ref GalRawInputData rawData);
    }
}
