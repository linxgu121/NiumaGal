// DialogueConfig.cs
// 配置/策略类枚举，与数据配置一起变更
namespace NiumaGal.Enum
{
    /// <summary>
    /// 播放模式：控制台词行完成后的推进方式
    /// </summary>
    public enum PlaybackMode
    {
        /// <summary>
        /// 手动播放：玩家点击推进
        /// </summary>
        Manual,
        /// <summary>
        /// 自动播放：台词行完成后自动推进下一行
        /// </summary>
        Auto
    }
}