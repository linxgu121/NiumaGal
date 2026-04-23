// DialogueInputCommands.cs
// 输入命令独立，与外部输入系统对接
namespace NiumaGal.Enum
{
    /// <summary>
    /// 对话系统输入命令
    /// 由DialogueInputController 生成，提交仲裁器
    /// </summary>
    public enum InputCommand
    {
        /// <summary>
        /// 推进/确认/启动交互
        /// </summary>
        Advance,
        /// <summary>
        /// 快进
        /// </summary>
        FastForward,
        /// <summary>
        /// 跳过当前对话单元(遇到分支/选择时停止跳过)
        /// </summary>
        SkipUnit,
        /// <summary>
        /// 切换自动播放
        /// </summary>
        ToggleAuto,
        /// <summary>
        /// 打开/关闭菜单（后续可扩展为多个子状态，如选择分支/查看角色信息等）
        /// </summary>
        Menu,
        /// <summary>
        /// 对话日志（显示已播放过的台词历史，后续可扩展为查看角色信息/CG等功能）
        /// </summary>
        Log,
        /// <summary>
        /// 存档
        /// </summary>
        Save,
        /// <summary>
        /// 读档
        /// </summary>
        Load,

    }
}