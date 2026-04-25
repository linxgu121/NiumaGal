// DialogueRuntimeStates.cs
// 对话系统核心运行时状态，新增状态类型时会一起审视
namespace NiumaGal.Enum
{
    /// <summary>
    /// 交互状态:对话系统内部的顶层状态
    /// 由外部触发器（碰撞体/射线检测/剧情触发）驱动进入 Interacting
    /// </summary>
    public enum InteractionState
    {
        /// <summary>
        /// 未交互（对话系统待机）
        /// </summary>
        Idle,
        /// <summary>
        /// 正在交互中（对话流程已启动）
        /// </summary>
        Interacting
    }

    /// <summary>
    /// 对话剧本状态：控制句子/单元的推进流程
    /// </summary>
    public enum DialogueScriptState
    {
        /// <summary>
        /// 空闲（无对话内容）
        /// </summary>
        Idle,
        /// <summary>
        /// 当前句正在进行中，等待结束
        /// </summary>
        Running,
        /// <summary>
        /// 当前句已结束，等待推进
        /// </summary>
        BetweenSentences,
        /// <summary>
        /// 单元结束，关闭对话后回到 Idle
        /// </summary>
        UnitEnded
    }

    /// <summary>
    /// 台词表现状态：描述 Typewriter 文字输出阶段
    /// 直接写入黑板，仲裁器只读
    /// </summary>
    public enum LineState
    {
        /// <summary>
        /// 无台词表现
        /// </summary>
        Idle,
        /// <summary>
        /// 当前台词行正在逐字显示
        /// </summary>
        Playing,
        /// <summary>
        /// 当前台词行文字已完整显示（语音可能继续播放），等待玩家点击推进
        /// </summary>
        Completed
    }

    /// <summary>
    /// 对话语音状态：当前台词行的语音生命周期
    /// 由 写入黑板，仲裁器与  读取
    public enum VoiceState
    {
        /// <summary>
        /// 无语音 / 被强制终止（快进/Switch）
        /// </summary>
        Idle,
        /// <summary>
        /// 语音正在播放
        /// </summary>
        Playing,
        /// <summary>
        /// 语音已播放完成
        /// </summary>
        Completed
    }
}
