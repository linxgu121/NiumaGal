using UnityEngine;
using UnityEngine.Audio;

namespace NiumaGal.Dialogue.Config.Core
{
    [CreateAssetMenu(fileName = "DialogueAudioSO", menuName = "NiumaGal/Config/DialogueAudioSO")]
    public class DialogueAudioSO : ScriptableObject
    {
        [Header("语音播放")]
        [Tooltip("默认语音音量")]
        [Range(0f, 1f)]
        public float VoiceVolume = 1f;

        [Tooltip("语音混音器组（可选）")]
        public AudioMixerGroup VoiceMixerGroup;

        [Tooltip("语音播放前延迟（秒），用于对口型准备")]
        public float VoicePreDelay = 0f;

        [Header("音效")]
        [Tooltip("打字机逐字音效（可选）")]
        public AudioClip TypewriterSFX;

        [Tooltip("逐字音效播放间隔(秒),0 为每个字都播")]
        public float TypewriterSFXInterval = 0.05f;
    }
}