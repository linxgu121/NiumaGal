using System;
using NiumaGal.Enum;

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
        public string ConditionId;
        public DialogueConditionType Type = DialogueConditionType.None;
        public string TargetId;
        public string Operator;
        public string StringValue;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
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
        public string ActionId;
        public DialogueActionType Type = DialogueActionType.None;
        public string TargetId;
        public string StringValue;
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
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
        public string ChoiceId;
        public string DisplayText;
        public DialogueChoiceBehavior Behavior = DialogueChoiceBehavior.Continue;
        public string NextSentenceId;
        public bool HideWhenUnavailable;
        public string DisabledText;
        public DialogueConditionData[] Conditions = Array.Empty<DialogueConditionData>();
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
