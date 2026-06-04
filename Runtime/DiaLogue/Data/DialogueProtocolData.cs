using System;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Dialogue.Data
{
    /// <summary>
    /// 对话扩展键值数据。用于少量跨模块参数，不承载复杂对象。
    /// </summary>
    [Serializable]
    public sealed class DialogueCustomDataEntry
    {
        public string Key;
        public string Value;

        public DialogueCustomDataEntry Clone()
        {
            return new DialogueCustomDataEntry
            {
                Key = Key,
                Value = Value
            };
        }

        public static DialogueCustomDataEntry[] CloneArray(DialogueCustomDataEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DialogueCustomDataEntry>();
            }

            var result = new DialogueCustomDataEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }

    /// <summary>
    /// 对话条件配置。条件只描述“要判断什么”，不直接引用具体业务模块实现。
    /// </summary>
    [Serializable]
    public sealed class DialogueConditionData
    {
        [Tooltip("条件稳定 ID。用于调试和日志定位，可填 has_item_key / quest_done 等；为空不影响运行。")]
        public string ConditionId;

        [Tooltip("条件类型。None 表示不判断；Quest / Story / Inventory / MiniGame 等条件需要外部 ConditionResolver 处理。")]
        public DialogueConditionType Type = DialogueConditionType.None;

        [Tooltip("条件目标 ID。例：DialogueRead 填 DialogueId；QuestState 填 QuestId；HasItem 填 ItemId；StoryFlag 填 FlagId。")]
        public string TargetId;

        [Tooltip("比较符。常用：==、!=、>=、<=、>、<。第一版由外部 Resolver 解释，不同条件可约定不同写法。")]
        public string Operator;

        [Tooltip("字符串参数。例：QuestState 可填 Completed；StoryFlag 可填 Flag 名或状态值；Custom 条件可自定义。")]
        public string StringValue;

        [Tooltip("整数参数。例：HasItem 的数量、GrowthLevel 的等级、任务目标计数等。")]
        public int IntValue;

        [Tooltip("浮点参数。需要小数阈值时使用，例如好感度、距离、概率等。")]
        public float FloatValue;

        [Tooltip("布尔参数。适合开关类条件，例如某个 Flag 是否为 true。")]
        public bool BoolValue;

        [Tooltip("扩展参数。用于少量跨模块补充字段；Key 建议小写下划线，例如 required_stage。")]
        public DialogueCustomDataEntry[] CustomData = Array.Empty<DialogueCustomDataEntry>();

        public DialogueConditionData Clone()
        {
            return new DialogueConditionData
            {
                ConditionId = ConditionId,
                Type = Type,
                TargetId = TargetId,
                Operator = Operator,
                StringValue = StringValue,
                IntValue = IntValue,
                FloatValue = FloatValue,
                BoolValue = BoolValue,
                CustomData = DialogueCustomDataEntry.CloneArray(CustomData)
            };
        }

        public static DialogueConditionData[] CloneArray(DialogueConditionData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DialogueConditionData>();
            }

            var result = new DialogueConditionData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }

    /// <summary>
    /// 对话行为配置。行为只描述“要请求什么”，具体执行由外部 ActionHandler 负责。
    /// </summary>
    [Serializable]
    public sealed class DialogueActionData
    {
        [Tooltip("行为稳定 ID。用于日志定位和防重复执行，可填 open_minigame / accept_quest 等；为空不影响运行。")]
        public string ActionId;

        [Tooltip("行为类型。Gal 只负责发出请求，具体执行由 NiumaDialogueController.Action Handler Provider 绑定的处理器负责。")]
        public DialogueActionType Type = DialogueActionType.None;

        [Tooltip("行为目标 ID。例：OpenMiniGame 填小游戏入口 ID；AcceptQuest 填 QuestId；LoadScene 填场景名；PlayAudioCue 填音频 CueId。")]
        public string TargetId;

        [Tooltip("字符串参数。例：LoadScene 的 SpawnPointId、SetStoryFlag 的 FlagValue、Custom 行为的自定义文本。")]
        public string StringValue;

        [Tooltip("整数参数。例：奖励数量、任务信号计数、小游戏模式索引等。")]
        public int IntValue;

        [Tooltip("浮点参数。例：淡入时间、音量、延迟秒数等。")]
        public float FloatValue;

        [Tooltip("布尔参数。例：是否立即切场景、是否保存检查点、是否强制执行等。")]
        public bool BoolValue;

        [Tooltip("扩展参数。用于少量跨模块补充字段；复杂业务不要塞进 Gal，交给外部 Handler 自己解释。")]
        public DialogueCustomDataEntry[] CustomData = Array.Empty<DialogueCustomDataEntry>();

        public DialogueActionData Clone()
        {
            return new DialogueActionData
            {
                ActionId = ActionId,
                Type = Type,
                TargetId = TargetId,
                StringValue = StringValue,
                IntValue = IntValue,
                FloatValue = FloatValue,
                BoolValue = BoolValue,
                CustomData = DialogueCustomDataEntry.CloneArray(CustomData)
            };
        }

        public static DialogueActionData[] CloneArray(DialogueActionData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DialogueActionData>();
            }

            var result = new DialogueActionData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }

    /// <summary>
    /// 对话选项配置。用于 NPC 对话进入 MiniGame、任务分支、剧情分支等。
    /// </summary>
    [Serializable]
    public sealed class DialogueChoiceData
    {
        [Tooltip("选项稳定 ID，必填。UI 点击和 DialogueService 选择都依赖它。建议用英文小写下划线，例如 enter_minigame / refuse_help。")]
        public string ChoiceId;

        [Tooltip("按钮显示文本。例：进入你画我猜 / 下次再说。为空时按钮会显示空文本，不建议留空。")]
        public string DisplayText;

        [Tooltip("玩家点击该选项后的基础行为：Continue=顺序下一句；JumpToSentence=跳到指定句子；EndDialogue=结束对话；Custom=先执行 Actions，可选再跳句子。")]
        public DialogueChoiceBehavior Behavior = DialogueChoiceBehavior.Continue;

        [Tooltip("目标句子 ID。Behavior=JumpToSentence 时必填；Behavior=Custom 时可选，表示执行 Actions 后继续跳到该句；Continue/EndDialogue 通常留空。")]
        public string NextSentenceId;

        [Tooltip("条件不满足时是否直接隐藏该选项。开启后玩家看不到；关闭后按钮仍显示但不可点击，并可显示 DisabledText。")]
        public bool HideWhenUnavailable;

        [Tooltip("条件不满足且未隐藏时显示的按钮文本。例：需要完成前置任务 / 需要 3 个草药。为空时沿用 DisplayText。")]
        public string DisabledText;

        [Tooltip("显示或可点击该选项需要满足的条件。全部满足才可用；为空表示无条件。具体判断由 ConditionResolver 执行。")]
        public DialogueConditionData[] Conditions = Array.Empty<DialogueConditionData>();

        [Tooltip("点击该选项时要执行的行为。例：进入小游戏、接取任务、切场景、设置剧情 Flag。Behavior=Custom 时最常用。")]
        public DialogueActionData[] Actions = Array.Empty<DialogueActionData>();

        public DialogueChoiceData Clone()
        {
            return new DialogueChoiceData
            {
                ChoiceId = ChoiceId,
                DisplayText = DisplayText,
                Behavior = Behavior,
                NextSentenceId = NextSentenceId,
                HideWhenUnavailable = HideWhenUnavailable,
                DisabledText = DisabledText,
                Conditions = DialogueConditionData.CloneArray(Conditions),
                Actions = DialogueActionData.CloneArray(Actions)
            };
        }

        public static DialogueChoiceData[] CloneArray(DialogueChoiceData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DialogueChoiceData>();
            }

            var result = new DialogueChoiceData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }

    [Serializable]
    public sealed class DialogueStartRequest
    {
        public DialogueAsset DialogueAsset;
        public string DialogueId;
        public string StartSentenceId;
        public string ActorId;
        public string SourceModule;
        public bool RestartIfPlaying;
    }

    [Serializable]
    public sealed class DialogueAdvanceRequest
    {
        public string ActorId;
        public string SourceModule;
    }

    [Serializable]
    public sealed class DialogueChoiceSelectRequest
    {
        public string ChoiceId;
        public string ActorId;
        public string SourceModule;
    }

    [Serializable]
    public sealed class DialogueCloseRequest
    {
        public string ActorId;
        public string SourceModule;
        public bool MarkAsCompleted;
    }

    /// <summary>
    /// 对话操作结果。业务失败走结构化原因，调用方不需要匹配 Message 字符串。
    /// </summary>
    [Serializable]
    public sealed class DialogueOperationResult
    {
        public bool Succeeded;
        public DialogueOperationFailureReason FailureReason = DialogueOperationFailureReason.None;
        public string Message;
        public string DialogueId;
        public string SentenceId;
        public string ChoiceId;

        public static DialogueOperationResult Success(string dialogueId = null, string sentenceId = null, string choiceId = null)
        {
            return new DialogueOperationResult
            {
                Succeeded = true,
                FailureReason = DialogueOperationFailureReason.None,
                DialogueId = dialogueId,
                SentenceId = sentenceId,
                ChoiceId = choiceId
            };
        }

        public static DialogueOperationResult Fail(
            DialogueOperationFailureReason reason,
            string message,
            string dialogueId = null,
            string sentenceId = null,
            string choiceId = null)
        {
            return new DialogueOperationResult
            {
                Succeeded = false,
                FailureReason = reason,
                Message = message,
                DialogueId = dialogueId,
                SentenceId = sentenceId,
                ChoiceId = choiceId
            };
        }
    }

    [Serializable]
    public sealed class DialogueChoiceViewData
    {
        public string ChoiceId;
        public string DisplayText;
        public bool IsAvailable;
        public string DisabledText;

        public DialogueChoiceViewData Clone()
        {
            return new DialogueChoiceViewData
            {
                ChoiceId = ChoiceId,
                DisplayText = DisplayText,
                IsAvailable = IsAvailable,
                DisabledText = DisabledText
            };
        }

        public static DialogueChoiceViewData[] CloneArray(DialogueChoiceViewData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DialogueChoiceViewData>();
            }

            var result = new DialogueChoiceViewData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }

    [Serializable]
    public sealed class DialogueViewData
    {
        public long Revision;
        public string DialogueId;
        public string SentenceId;
        public int SentenceIndex;
        public string Speaker;
        public string FullText;
        public string DisplayText;
        public bool ShowContinueHint;
        public bool IsWaitingChoice;
        public DialogueChoiceViewData[] Choices = Array.Empty<DialogueChoiceViewData>();

        public DialogueViewData Clone()
        {
            return new DialogueViewData
            {
                Revision = Revision,
                DialogueId = DialogueId,
                SentenceId = SentenceId,
                SentenceIndex = SentenceIndex,
                Speaker = Speaker,
                FullText = FullText,
                DisplayText = DisplayText,
                ShowContinueHint = ShowContinueHint,
                IsWaitingChoice = IsWaitingChoice,
                Choices = DialogueChoiceViewData.CloneArray(Choices)
            };
        }
    }

    [Serializable]
    public sealed class DialoguePlaybackSnapshot
    {
        public long Revision;
        public string DialogueId;
        public string SentenceId;
        public int SentenceIndex;
        public bool IsPlaying;
        public DialoguePlaybackPhase Phase = DialoguePlaybackPhase.None;
        public InteractionState InteractionState;
        public DialogueScriptState ScriptState;
        public LineState LineState;
        public VoiceState VoiceState;
        public PlaybackMode PlaybackMode;
        public DialogueChoiceViewData[] CurrentChoices = Array.Empty<DialogueChoiceViewData>();

        public DialoguePlaybackSnapshot Clone()
        {
            return new DialoguePlaybackSnapshot
            {
                Revision = Revision,
                DialogueId = DialogueId,
                SentenceId = SentenceId,
                SentenceIndex = SentenceIndex,
                IsPlaying = IsPlaying,
                Phase = Phase,
                InteractionState = InteractionState,
                ScriptState = ScriptState,
                LineState = LineState,
                VoiceState = VoiceState,
                PlaybackMode = PlaybackMode,
                CurrentChoices = DialogueChoiceViewData.CloneArray(CurrentChoices)
            };
        }
    }

    [Serializable]
    public sealed class DialogueProgressSnapshot
    {
        public long Revision;
        public string[] ReadDialogueIds = Array.Empty<string>();
        public string[] TriggeredAmbientIds = Array.Empty<string>();

        public DialogueProgressSnapshot Clone()
        {
            return new DialogueProgressSnapshot
            {
                Revision = Revision,
                ReadDialogueIds = CloneStringArray(ReadDialogueIds),
                TriggeredAmbientIds = CloneStringArray(TriggeredAmbientIds)
            };
        }

        private static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }

    public readonly struct DialogueConditionContext
    {
        public readonly DialogueAsset DialogueAsset;
        public readonly DialogueSentence Sentence;
        public readonly DialogueChoiceData Choice;
        public readonly DialogueConditionData Condition;
        public readonly string ActorId;
        public readonly string SourceModule;

        public DialogueConditionContext(
            DialogueAsset dialogueAsset,
            DialogueSentence sentence,
            DialogueChoiceData choice,
            DialogueConditionData condition,
            string actorId,
            string sourceModule)
        {
            DialogueAsset = dialogueAsset;
            Sentence = sentence;
            Choice = choice;
            Condition = condition;
            ActorId = actorId;
            SourceModule = sourceModule;
        }
    }

    public readonly struct DialogueActionContext
    {
        public readonly DialogueAsset DialogueAsset;
        public readonly DialogueSentence Sentence;
        public readonly DialogueChoiceData Choice;
        public readonly DialogueActionData Action;
        public readonly string ActorId;
        public readonly string SourceModule;

        public DialogueActionContext(
            DialogueAsset dialogueAsset,
            DialogueSentence sentence,
            DialogueChoiceData choice,
            DialogueActionData action,
            string actorId,
            string sourceModule)
        {
            DialogueAsset = dialogueAsset;
            Sentence = sentence;
            Choice = choice;
            Action = action;
            ActorId = actorId;
            SourceModule = sourceModule;
        }
    }
}
