using UnityEngine;

namespace NiumaGal.Extension.Ambient
{
    /// <summary>
    /// 环境叙事表现层数据。
    /// 只描述当前应该显示什么，不包含触发、推进和 UI 创建逻辑。
    /// </summary>
    public readonly struct AmbientLineViewData
    {
        public readonly string Speaker;
        public readonly string FullText;
        public readonly string DisplayText;
        public readonly AmbientMode Mode;
        public readonly Transform SourceTransform;
        public readonly bool IsTextCompleted;
        public readonly bool IsVoiceCompleted;
        public readonly float TextProgress;

        public AmbientLineViewData(
            string speaker,
            string fullText,
            string displayText,
            AmbientMode mode,
            Transform sourceTransform,
            bool isTextCompleted,
            bool isVoiceCompleted,
            float textProgress)
        {
            Speaker = speaker;
            FullText = fullText;
            DisplayText = displayText;
            Mode = mode;
            SourceTransform = sourceTransform;
            IsTextCompleted = isTextCompleted;
            IsVoiceCompleted = isVoiceCompleted;
            TextProgress = Mathf.Clamp01(textProgress);
        }
    }
}
