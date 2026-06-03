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
        Continue = 0,
        JumpToSentence = 1,
        EndDialogue = 2,
        Custom = 99
    }

    /// <summary>
    /// 对话条件类型。第一版只冻结通用协议，具体判断由外部 Resolver 扩展。
    /// </summary>
    public enum DialogueConditionType
    {
        None = 0,
        DialogueRead = 1,
        DialogueUnread = 2,
        QuestState = 3,
        QuestObjectiveCompleted = 4,
        StoryFlag = 5,
        HasItem = 6,
        GrowthLevel = 7,
        MiniGameUnlocked = 8,
        Custom = 99
    }

    /// <summary>
    /// 对话行为类型。核心 Gal 不直接依赖 Quest、Story、MiniGame 等模块，只通过 ActionHandler 转发。
    /// </summary>
    public enum DialogueActionType
    {
        None = 0,
        StartDialogue = 1,
        EndDialogue = 2,
        OpenMiniGame = 3,
        AcceptQuest = 4,
        PushQuestSignal = 5,
        StartStory = 6,
        SetStoryFlag = 7,
        LoadScene = 8,
        RequestCheckpointSave = 9,
        PlayAudioCue = 10,
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
