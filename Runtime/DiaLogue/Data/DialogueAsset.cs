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
        public List<DialogueSentence> Sentences = new List<DialogueSentence>();
    }
}
