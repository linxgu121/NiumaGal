namespace NiumaGal.Dialogue.Input.Data
{
    /// <summary>
    /// 原始输入采样数据
    /// 由 InputSource 每帧从硬件采样写入
    /// </summary>
    public struct GalRawInputData
    {
        /// <summary>
        /// 长按推进-快进
        /// </summary>
        public bool AdvancePressed;
        /// <summary>
        /// 推进按键刚按下
        /// </summary>
        public bool AdvanceJustPressed;

        // 其他离散命令 - 只需要 JustPressed
        /// <summary>
        /// 跳过当前单元（长按无效，避免误触）
        /// </summary>
        public bool SkipUnitJustPressed;
        /// <summary>
        /// 切换自动推进（长按无效，避免误触）
        /// </summary>
        public bool ToggleAutoJustPressed;
        /// <summary>
        /// 打开菜单（长按无效，避免误触）
        /// </summary>
        public bool MenuJustPressed;
        /// <summary>
        /// 历史记录（长按无效，避免误触）
        /// </summary>
        public bool LogJustPressed;
        /// <summary>
        /// 隐藏/显示 UI（长按无效，避免误触）
        /// </summary>
        public bool HideUIJustPressed;
        /// <summary>
        /// 保存进度（长按无效，避免误触）
        /// </summary>
        public bool SaveJustPressed;
        /// <summary>
        /// 读取进度（长按无效，避免误触）
        /// </summary>
        public bool LoadJustPressed;
    }
}
