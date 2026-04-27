using System.Collections.Generic;
using NiumaGal.Dialogue.Data;
using UnityEngine;

namespace NiumaGal.Extension.Ambient
{
    [CreateAssetMenu(fileName = "AmbientAsset", menuName = "NiumaGal/Ambient/AmbientAsset")]
    public class AmbientAsset : ScriptableObject
    {
        [Header("台词池")]
        [Tooltip("环境台词池，随机播放其中一句；独白模式则按顺序播放全部")]
        public List<DialogueSentence> Lines = new List<DialogueSentence>();

        [Header("触发设置")]
        [Tooltip("触发半径(Bubble/Subtitle 距离检测用）")]
        public float TriggerRadius = 5f;

        [Tooltip("冷却时间（秒），防止反复触发")]
        public float Cooldown = 10f;

        [Tooltip("是否只触发一次")]
        public bool OneShot = false;

        [Header("表现设置")]
        [Tooltip("默认表现模式")]
        public AmbientMode DefaultMode = AmbientMode.Bubble;

        [Tooltip("气泡持续时间(秒),0 则按文本长度自动计算")]
        public float BubbleDuration = 3f;

        [Tooltip("独白模式下句间停顿（秒）")]
        public float MonologueLineInterval = 0.5f;
    }
}
