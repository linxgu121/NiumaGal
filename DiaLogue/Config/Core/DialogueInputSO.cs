using UnityEngine;

namespace NiumaGal.Dialogue.Config.Core
{
    [CreateAssetMenu(fileName = "DialogueInputSO", menuName = "NiumaGal/Config/DialogueInputSO")]
    public class DialogueInputSO : ScriptableObject
    {
        [Header("Advance 按键缓存")]
        [Tooltip("Advance 按键的缓存时间（秒），在此时间内视为已按下")]
        public float ActionBufferTime = 0.2f;

        [Header("快进触发")]
        [Tooltip("长按 Advance 触发快进的时间阈值（秒）")]
        public float FastForwardThreshold = 0.5f;
    }
}