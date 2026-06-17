using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaGal.Editor
{
    public abstract class DialogueArrayCardBuilderBase
    {
        private readonly SerializedObject serializedObject;

        protected DialogueArrayCardBuilderBase(SerializedObject serializedObject, Action onChanged)
        {
            this.serializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            OnChanged = onChanged;
        }

        protected Action OnChanged { get; }

        protected void AddArrayCommandRow(VisualElement parent, SerializedProperty arrayProperty, string itemName, Action<SerializedProperty> initializer)
        {
            var row = new Toolbar
            {
                name = $"Dialogue{itemName}ArrayToolbar"
            };
            row.Add(new ToolbarButton(() => AddArrayElement(arrayProperty, initializer))
            {
                text = $"新增{LocalizeItemName(itemName)}"
            });
            parent.Add(row);
        }

        protected void AddCardDeleteButton(VisualElement parent, SerializedProperty arrayProperty, int index, string itemName)
        {
            var row = new VisualElement
            {
                name = $"Dialogue{itemName}CardCommandRow"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;

            var capturedIndex = index;
            row.Add(new Button(() => DeleteArrayElementAndRefresh(arrayProperty, capturedIndex))
            {
                text = $"删除{LocalizeItemName(itemName)}"
            });
            parent.Add(row);
        }

        protected static Foldout BuildArrayFoldout(string title, string timingDescription, int count)
        {
            var foldout = new Foldout
            {
                text = title,
                value = true
            };
            foldout.style.marginTop = 6f;
            foldout.style.marginBottom = 4f;

            if (!string.IsNullOrWhiteSpace(timingDescription))
            {
                foldout.Add(new HelpBox(timingDescription, HelpBoxMessageType.Info));
            }

            if (count == 0)
            {
                foldout.Add(new HelpBox("当前列表为空。点击下方“新增”按钮添加一项。", HelpBoxMessageType.Info));
            }

            return foldout;
        }

        protected static Foldout BuildCardFoldout(string title)
        {
            var card = new Foldout
            {
                text = title,
                value = false
            };
            card.style.marginLeft = 10f;
            card.style.marginTop = 4f;
            card.style.paddingLeft = 6f;
            card.style.paddingTop = 4f;
            card.style.paddingBottom = 4f;
            card.style.borderLeftWidth = 2f;
            card.style.borderLeftColor = new Color(0.3f, 0.55f, 0.85f, 1f);
            return card;
        }

        protected static string LocalizeItemName(string itemName)
        {
            return itemName switch
            {
                "Condition" => "条件",
                "Action" => "行为",
                "Choice" => "选项",
                _ => itemName ?? string.Empty
            };
        }

        private void AddArrayElement(SerializedProperty arrayProperty, Action<SerializedProperty> initializer)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = serializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray)
            {
                return;
            }

            var index = refreshedArray.arraySize;
            refreshedArray.InsertArrayElementAtIndex(index);
            initializer?.Invoke(refreshedArray.GetArrayElementAtIndex(index));
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            OnChanged?.Invoke();
        }

        private void DeleteArrayElementAndRefresh(SerializedProperty arrayProperty, int index)
        {
            var propertyPath = arrayProperty?.propertyPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            var refreshedArray = serializedObject.FindProperty(propertyPath);
            if (refreshedArray == null || !refreshedArray.isArray || index < 0 || index >= refreshedArray.arraySize)
            {
                return;
            }

            DialogueSerializedPropertyUtility.DeleteArrayElement(refreshedArray, index);
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            OnChanged?.Invoke();
        }
    }

    public sealed class DialogueConditionCardBuilder : DialogueArrayCardBuilderBase
    {
        public DialogueConditionCardBuilder(SerializedObject serializedObject, Action onChanged)
            : base(serializedObject, onChanged)
        {
        }

        public void AddCards(VisualElement parent, SerializedProperty conditionsProperty, string title, string timingDescription)
        {
            if (conditionsProperty == null || !conditionsProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"{title}（{conditionsProperty.arraySize}）", timingDescription, conditionsProperty.arraySize);
            AddArrayCommandRow(foldout, conditionsProperty, "Condition", InitializeElement);

            for (var i = 0; i < conditionsProperty.arraySize; i++)
            {
                var condition = conditionsProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(condition, i));
                AddCardDeleteButton(card, conditionsProperty, i, "Condition");
                AddBody(card, condition);
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private void AddBody(VisualElement parent, SerializedProperty condition)
        {
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "ConditionId", "条件 ID", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "Type", "条件类型", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "TargetId", "目标 ID", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "Operator", "运算符", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "StringValue", "字符串值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "IntValue", "整数值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "FloatValue", "小数值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "BoolValue", "布尔值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, condition, "CustomData", "自定义数据", OnChanged);
        }

        private static void InitializeElement(SerializedProperty condition)
        {
            DialogueSerializedPropertyUtility.SetString(condition, "ConditionId", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(condition, "Type", 0);
            DialogueSerializedPropertyUtility.SetString(condition, "TargetId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(condition, "Operator", string.Empty);
            DialogueSerializedPropertyUtility.SetString(condition, "StringValue", string.Empty);
            DialogueSerializedPropertyUtility.SetInt(condition, "IntValue", 0);
            DialogueSerializedPropertyUtility.SetFloat(condition, "FloatValue", 0f);
            DialogueSerializedPropertyUtility.SetBool(condition, "BoolValue", false);
            DialogueSerializedPropertyUtility.ResetCustomData(condition);
        }

        private static string BuildCardTitle(SerializedProperty condition, int index)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumDisplayName(condition, "Type");
            var id = DialogueSerializedPropertyUtility.GetString(condition, "ConditionId");
            var target = DialogueSerializedPropertyUtility.GetString(condition, "TargetId");
            return $"{index + 1}. {type} | {DialogueSerializedPropertyUtility.Fallback(id, "<无 ID>")} | 目标:{DialogueSerializedPropertyUtility.Fallback(target, "<空>")}";
        }
    }

    public sealed class DialogueActionCardBuilder : DialogueArrayCardBuilderBase
    {
        public DialogueActionCardBuilder(SerializedObject serializedObject, Action onChanged)
            : base(serializedObject, onChanged)
        {
        }

        public void AddCards(VisualElement parent, SerializedProperty actionsProperty, string title, string timingDescription)
        {
            if (actionsProperty == null || !actionsProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout($"{title}（{actionsProperty.arraySize}）", timingDescription, actionsProperty.arraySize);
            AddArrayCommandRow(foldout, actionsProperty, "Action", InitializeElement);

            for (var i = 0; i < actionsProperty.arraySize; i++)
            {
                var action = actionsProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(action, i));
                AddCardDeleteButton(card, actionsProperty, i, "Action");
                AddBody(card, action);
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private void AddBody(VisualElement parent, SerializedProperty action)
        {
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "ActionId", "行为 ID", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "Type", "行为类型", OnChanged);
            parent.Add(new HelpBox(GetTargetHint(action), HelpBoxMessageType.Info));
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "TargetId", "目标 ID", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "StringValue", "字符串值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "IntValue", "整数值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "FloatValue", "小数值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "BoolValue", "布尔值", OnChanged);
            DialogueSerializedPropertyUtility.AddRelativeProperty(parent, action, "CustomData", "自定义数据", OnChanged);
        }

        private static void InitializeElement(SerializedProperty action)
        {
            DialogueSerializedPropertyUtility.SetString(action, "ActionId", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(action, "Type", 0);
            DialogueSerializedPropertyUtility.SetString(action, "TargetId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(action, "StringValue", string.Empty);
            DialogueSerializedPropertyUtility.SetInt(action, "IntValue", 0);
            DialogueSerializedPropertyUtility.SetFloat(action, "FloatValue", 0f);
            DialogueSerializedPropertyUtility.SetBool(action, "BoolValue", false);
            DialogueSerializedPropertyUtility.ResetCustomData(action);
        }

        private static string BuildCardTitle(SerializedProperty action, int index)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumDisplayName(action, "Type");
            var id = DialogueSerializedPropertyUtility.GetString(action, "ActionId");
            var target = DialogueSerializedPropertyUtility.GetString(action, "TargetId");
            return $"{index + 1}. {type} | {DialogueSerializedPropertyUtility.Fallback(id, "<无 ID>")} | 目标:{DialogueSerializedPropertyUtility.Fallback(target, "<空>")}";
        }

        private static string GetTargetHint(SerializedProperty action)
        {
            var type = DialogueSerializedPropertyUtility.GetEnumName(action, "Type");
            switch (type)
            {
                case "StartDialogue":
                    return "TargetId：填写要启动的 DialogueId。";
                case "EndDialogue":
                    return "TargetId：通常留空。";
                case "OpenMiniGame":
                    return "TargetId：填写小游戏入口 ID 或模式 ID。";
                case "AcceptQuest":
                    return "TargetId：填写 QuestId。";
                case "PushQuestSignal":
                    return "TargetId：填写 SignalId。";
                case "StartStory":
                    return "TargetId：填写 StoryId。";
                case "SetStoryFlag":
                    return "TargetId：填写 FlagId。";
                case "LoadScene":
                    return "TargetId：填写场景名。";
                case "RequestCheckpointSave":
                    return "TargetId：通常留空。";
                case "PlayAudioCue":
                    return "TargetId：填写 AudioCueDefinition.CueId。";
                case "Custom":
                    return "TargetId / CustomData：由自定义 ActionHandler 解释。";
                default:
                    return "TargetId 的用途取决于行为类型。";
            }
        }
    }

    public sealed class DialogueChoiceCardBuilder : DialogueArrayCardBuilderBase
    {
        private readonly DialogueConditionCardBuilder conditionCardBuilder;
        private readonly DialogueActionCardBuilder actionCardBuilder;

        public DialogueChoiceCardBuilder(
            SerializedObject serializedObject,
            Action onChanged,
            DialogueConditionCardBuilder conditionCardBuilder,
            DialogueActionCardBuilder actionCardBuilder)
            : base(serializedObject, onChanged)
        {
            this.conditionCardBuilder = conditionCardBuilder ?? throw new ArgumentNullException(nameof(conditionCardBuilder));
            this.actionCardBuilder = actionCardBuilder ?? throw new ArgumentNullException(nameof(actionCardBuilder));
        }

        public void AddCards(VisualElement parent, SerializedProperty choicesProperty)
        {
            if (choicesProperty == null || !choicesProperty.isArray)
            {
                return;
            }

            var foldout = BuildArrayFoldout(
                $"选项（{choicesProperty.arraySize}）",
                "文字播放完成后显示给玩家的选项。ChoiceId 必填，运行时点击依赖它。",
                choicesProperty.arraySize);
            AddArrayCommandRow(foldout, choicesProperty, "Choice", InitializeElement);
            foldout.Add(new HelpBox("选项点击顺序：先执行 Choice Actions，再执行当前句子的 Exit Actions，最后应用 Behavior。", HelpBoxMessageType.Info));

            for (var i = 0; i < choicesProperty.arraySize; i++)
            {
                var choice = choicesProperty.GetArrayElementAtIndex(i);
                var card = BuildCardFoldout(BuildCardTitle(choice, i));
                AddCardDeleteButton(card, choicesProperty, i, "Choice");
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "ChoiceId", "选项 ID", OnChanged);
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "DisplayText", "显示文本", OnChanged);
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "Behavior", "跳转行为", OnChanged);
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "NextSentenceId", "目标句 ID", OnChanged);
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "HideWhenUnavailable", "条件不满足时隐藏", OnChanged);
                DialogueSerializedPropertyUtility.AddRelativeProperty(card, choice, "DisabledText", "不可用文本", OnChanged);
                conditionCardBuilder.AddCards(card, choice.FindPropertyRelative("Conditions"), "选项条件", "该选项显示或可点击前需要满足的条件。");
                actionCardBuilder.AddCards(card, choice.FindPropertyRelative("Actions"), "选项行为", "玩家点击该选项后立即执行，然后再执行当前句子的 Exit Actions。");
                foldout.Add(card);
            }

            parent.Add(foldout);
        }

        private static void InitializeElement(SerializedProperty choice)
        {
            DialogueSerializedPropertyUtility.SetString(choice, "ChoiceId", string.Empty);
            DialogueSerializedPropertyUtility.SetString(choice, "DisplayText", string.Empty);
            DialogueSerializedPropertyUtility.SetEnumIndex(choice, "Behavior", 0);
            DialogueSerializedPropertyUtility.SetString(choice, "NextSentenceId", string.Empty);
            DialogueSerializedPropertyUtility.SetBool(choice, "HideWhenUnavailable", false);
            DialogueSerializedPropertyUtility.SetString(choice, "DisabledText", string.Empty);
            DialogueSerializedPropertyUtility.ClearArray(choice, "Conditions");
            DialogueSerializedPropertyUtility.ClearArray(choice, "Actions");
        }

        private static string BuildCardTitle(SerializedProperty choice, int index)
        {
            var id = DialogueSerializedPropertyUtility.GetString(choice, "ChoiceId");
            var text = DialogueSerializedPropertyUtility.GetString(choice, "DisplayText");
            var behavior = DialogueSerializedPropertyUtility.GetEnumDisplayName(choice, "Behavior");
            return $"{index + 1}. {DialogueSerializedPropertyUtility.Fallback(id, "<空选项 ID>")} | {behavior} | {DialogueEditorTextUtility.BuildTextSummary(text, 24)}";
        }
    }
}
