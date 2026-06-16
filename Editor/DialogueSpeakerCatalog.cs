using System;
using UnityEngine;

namespace NiumaGal.Editor
{
    [CreateAssetMenu(fileName = "DialogueSpeakerCatalog", menuName = "NiumaGal/Editor/Dialogue Speaker Catalog")]
    public sealed class DialogueSpeakerCatalog : ScriptableObject
    {
        [Tooltip("编辑器可选说话人列表。SpeakerKey 必须与 DialogueSentence.Speaker 字符串完全一致。")]
        public DialogueSpeakerEditorData[] Speakers = Array.Empty<DialogueSpeakerEditorData>();
    }

    [Serializable]
    public sealed class DialogueSpeakerEditorData
    {
        [Tooltip("说话人 ID。会写入 DialogueSentence.Speaker，必须稳定，不要随显示名一起改。")]
        public string SpeakerKey;

        [Tooltip("编辑器显示名。仅用于编辑器辅助识别，不会写入运行时对话数据。")]
        public string DisplayName;

        [Tooltip("编辑器预览头像。仅用于配置辅助，不进入 DialogueService 运行时逻辑。")]
        public Sprite Portrait;

        [Tooltip("编辑器预览主题色。仅用于配置辅助，不进入 DialogueService 运行时逻辑。")]
        public Color ThemeColor = Color.white;

        [Tooltip("编辑器试听用语音。仅用于编辑器 Preview，不会被运行时 DialogueService 读取。")]
        public AudioClip PreviewVoice;
    }
}