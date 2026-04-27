namespace NiumaGal.Extension.Ambient
{
    /// <summary>
    /// 环境叙事表现模式
    /// </summary>
    public enum AmbientMode
    {
        Bubble,             // 头顶气泡（世界空间，跟随NPC）
        Subtitle,           // 屏幕旁白字幕（全局覆盖）
        ProximityMonologue  // 近距离独白（强制语音+字幕，自动推进）
    }
}