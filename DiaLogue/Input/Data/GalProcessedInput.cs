namespace NiumaGal.Dialogue.Input.Data
{
    /// <summary>
    /// 后处理数据
    /// 由 GalInputPipeline 生成，包含动作缓存、长按检测等
    /// </summary>
    public struct GalProcessedInput
    {
        // 推进相关（唯一需要缓存和长按检测的命令
        public bool AdvanceHeld;          // 持续按住
        public float AdvanceBufferTimer; // 动作缓存计时器
        public bool AdvanceJustPressed;  // 本帧刚按下（透传）
        public bool FastForwardActive;   // 长按 Advance 触发的快进状态

        // 仅透传 JustPressed，无缓存
        public bool SkipUnitJustPressed;
        public bool ToggleAutoJustPressed;
        public bool MenuJustPressed;
        public bool LogJustPressed;
        public bool HideUIJustPressed;
        public bool SaveJustPressed;
        public bool LoadJustPressed;
    }
}
