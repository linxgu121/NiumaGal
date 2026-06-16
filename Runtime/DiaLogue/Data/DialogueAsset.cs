using System;
using System.Collections.Generic;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Dialogue.Data
{
    [Serializable]
    public class DialogueSentence
    {
        [Tooltip("句子稳定 ID。用于选项跳转、存档、调试和后续剧情回溯；为空时运行层可退回到句子索引。")]
        public string SentenceId;

        public string Speaker;

        [TextArea(3, 5)]
        public string Text;

        public AudioClip VoiceClip;

        [Tooltip("叙事分类。用于编辑器 Graph 节点颜色和后续叙事筛选；None 表示未分类，不影响运行。")]
        public DialogueNarrativeCategory NarrativeCategory;

        [HideInInspector]
        public string EditorGuid;

        [Tooltip("进入该句前需要满足的条件。为空表示无条件。")]
        public DialogueConditionData[] Conditions = Array.Empty<DialogueConditionData>();

        [Tooltip("该句开始播放时执行的行为。第一版只冻结协议，具体执行由外部 ActionHandler 负责。")]
        public DialogueActionData[] EnterActions = Array.Empty<DialogueActionData>();

        [Tooltip("该句完整推进离开时执行的行为。第一版只冻结协议，具体执行由外部 ActionHandler 负责。")]
        public DialogueActionData[] ExitActions = Array.Empty<DialogueActionData>();

        [Tooltip("该句播放完成后显示的选项。为空时按线性对话继续推进。")]
        [Header("选项配置：文字播放完后显示；每个选项必须填写 ChoiceId")]
        public DialogueChoiceData[] Choices = Array.Empty<DialogueChoiceData>();
    }

    [CreateAssetMenu(fileName = "DialogueAsset", menuName = "NiumaGal/Data/DialogueAsset")]
    public class DialogueAsset : ScriptableObject
    {
        [Tooltip("对话唯一 ID。用于任务、存档和调试。所有用于任务目标的对话都必须填写，确定后不要随资源文件名一起改动。近距离独白建议使用 monologue_ 前缀，普通剧情对话建议使用 dialogue_ 前缀。")]
        public string DialogueId;

        [Tooltip("对话显示名。主要用于调试面板和编辑器检视，不作为稳定逻辑 ID。")]
        public string DisplayName;

        [Tooltip("起始句子 ID。为空时从 Sentences[0] 开始。")]
        public string StartSentenceId;

        [Tooltip("对话开始时执行的行为。适合切换镜头、锁输入、触发轻量剧情标记等。")]
        public DialogueActionData[] OnStartActions = Array.Empty<DialogueActionData>();

        [Tooltip("对话完整结束时执行的行为。适合推进任务、进入 MiniGame、触发剧情节点等。")]
        public DialogueActionData[] OnCompleteActions = Array.Empty<DialogueActionData>();

        [Tooltip("对话被强制关闭或中断时执行的行为。默认可以留空，避免误把未读完剧情写入进度。")]
        public DialogueActionData[] OnAbortActions = Array.Empty<DialogueActionData>();

        public List<DialogueSentence> Sentences = new List<DialogueSentence>();
    }
}
