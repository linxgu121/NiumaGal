using System;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaGal.Dialogue.Data
{
    [Serializable]
    public class DialogueSentence
    {
        public string Speaker;
        [TextArea(3, 5)]
        public string Text;
        public AudioClip VoiceClip;
    }

    [CreateAssetMenu(fileName = "DialogueAsset", menuName = "NiumaGal/Data/DialogueAsset")]
    public class DialogueAsset : ScriptableObject
    {
        [Tooltip("对话唯一 ID。用于任务、存档和调试。所有用于任务目标的对话都必须填写，确定后不要随资源文件名一起改动。近距离独白建议使用 monologue_ 前缀，普通剧情对话建议使用 dialogue_ 前缀。")]
        public string DialogueId;

        public List<DialogueSentence> Sentences = new List<DialogueSentence>();
    }
}
