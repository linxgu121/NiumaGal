using System;

namespace NiumaGal.Save
{
    /// <summary>
    /// NiumaGal 存档数据。
    /// 只保存已经发生的剧情事实，不保存正在播放中的临时状态。
    /// </summary>
    [Serializable]
    public sealed class GalSaveData
    {
        /// <summary>
        /// 已完整读完的对话 ID。
        /// </summary>
        public string[] ReadDialogueIds = Array.Empty<string>();

        /// <summary>
        /// 已触发的一次性环境叙事 ID。
        /// </summary>
        public string[] TriggeredAmbientIds = Array.Empty<string>();
    }
}
