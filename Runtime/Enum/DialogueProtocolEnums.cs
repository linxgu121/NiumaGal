using UnityEngine;

namespace NiumaGal.Enum
{
    /// <summary>
    /// 对话操作失败原因。用于 Service / Bridge 返回结构化错误，避免 UI 依赖字符串匹配。
    /// </summary>
    public enum DialogueOperationFailureReason
    {
        None = 0,
        ServiceNotReady = 1,
        InvalidRequest = 2,
        DialogueNotFound = 3,
        DialogueAlreadyPlaying = 4,
        DialogueNotPlaying = 5,
        SentenceNotFound = 6,
        ChoiceNotFound = 7,
        ChoiceUnavailable = 8,
        ConditionBlocked = 9,
        ActionFailed = 10,
        PresenterMissing = 11,
        Unknown = 99
    }

    /// <summary>
    /// 对话选项选择后的基础行为。
    /// </summary>
    public enum DialogueChoiceBehavior
    {
        [InspectorName("顺序继续：播放下一句")]
        Continue = 0,

        [InspectorName("跳转句子：跳到 NextSentenceId")]
        JumpToSentence = 1,

        [InspectorName("结束对话：点击后关闭")]
        EndDialogue = 2,

        [InspectorName("自定义行为：执行 Actions，可选再跳句子")]
        Custom = 99
    }

    /// <summary>
    /// 对话条件类型。第一版只冻结通用协议，具体判断由外部 Resolver 扩展。
    /// </summary>
    public enum DialogueConditionType
    {
        [InspectorName("无条件")]
        None = 0,

        [InspectorName("对话已读：TargetId=DialogueId")]
        DialogueRead = 1,

        [InspectorName("对话未读：TargetId=DialogueId")]
        DialogueUnread = 2,

        [InspectorName("任务状态：TargetId=QuestId")]
        QuestState = 3,

        [InspectorName("任务目标完成：TargetId=ObjectiveId")]
        QuestObjectiveCompleted = 4,

        [InspectorName("剧情标记：TargetId=FlagId")]
        StoryFlag = 5,

        [InspectorName("拥有物品：TargetId=ItemId，IntValue=数量")]
        HasItem = 6,

        [InspectorName("技艺等级：TargetId=SkillId，IntValue=等级")]
        GrowthLevel = 7,

        [InspectorName("小游戏已解锁：TargetId=MiniGameId")]
        MiniGameUnlocked = 8,

        [InspectorName("自定义条件：交给 Resolver")]
        Custom = 99
    }

    /// <summary>
    /// 对话行为类型。核心 Gal 不直接依赖 Quest、Story、MiniGame 等模块，只通过 ActionHandler 转发。
    /// </summary>
    public enum DialogueActionType
    {
        [InspectorName("无行为")]
        None = 0,

        [InspectorName("启动对话：TargetId=DialogueId")]
        StartDialogue = 1,

        [InspectorName("结束对话")]
        EndDialogue = 2,

        [InspectorName("打开小游戏：TargetId=入口或模式 ID")]
        OpenMiniGame = 3,

        [InspectorName("接取任务：TargetId=QuestId")]
        AcceptQuest = 4,

        [InspectorName("推送任务信号：TargetId=SignalId")]
        PushQuestSignal = 5,

        [InspectorName("开始剧情：TargetId=StoryId")]
        StartStory = 6,

        [InspectorName("设置剧情标记：TargetId=FlagId")]
        SetStoryFlag = 7,

        [InspectorName("加载场景：TargetId=SceneName")]
        LoadScene = 8,

        [InspectorName("请求检查点存档")]
        RequestCheckpointSave = 9,

        [InspectorName("播放音频：TargetId=CueId")]
        PlayAudioCue = 10,

        [InspectorName("自定义行为：交给 ActionHandler")]
        Custom = 99
    }

    /// <summary>
    /// 对话表现层阶段。它是给 UI / 调试面板看的协议状态，不强制替换旧 StateMachine 枚举。
    /// </summary>
    public enum DialoguePlaybackPhase
    {
        None = 0,
        Idle = 1,
        PlayingLine = 2,
        WaitingAdvance = 3,
        WaitingChoice = 4,
        Closing = 5
    }
}
