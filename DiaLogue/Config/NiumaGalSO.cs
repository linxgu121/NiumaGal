using NiumaGal.Dialogue.Config.Core;
using UnityEngine;

namespace NiumaGal.Dialogue.Config
{
    [CreateAssetMenu(fileName = "NiumaGalSO", menuName = "NiumaGal/Config/NiumaGalSO", order = 0)]
    public class NiumaGalSO
    {
        [Header("核心功能模块")]
        [Tooltip("打字机与自动播放参数")]
        public DialogueCoreSO Core;

        [Tooltip("语音与音效参数")]
        public DialogueAudioSO Audio;

        [Tooltip("输入缓冲与快进参数")]
        public DialogueInputSO Input;
    }
}