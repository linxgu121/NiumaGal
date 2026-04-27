// DialogueInputCommands.cs
// 输入命令独立，与外部输入系统对接
namespace NiumaGal.Enum
{
    /// <summary>
    /// 对话系统专用输入命令
    /// 仅与对话推进相关，全局系统命令由 NiumaUI 处理
    /// </summary>
    public enum InputCommand
    {
        /// <summary>
        /// // 推进/确认/启动交互
        /// </summary>
        Advance,
        /// <summary>
        /// 快进（长按 Advance）        
        /// </summary>
        FastForward,
        /// <summary>
        /// 跳过当前对话单元    
        /// </summary>
        SkipUnit,
        /// <summary>
        /// 切换自动播放       
        /// </summary>
        ToggleAuto
    }

}