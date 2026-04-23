namespace NiumaGal.DiaLogue.Input.Data
{
    public struct GalFrameInputData
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// 原始输入数据
        /// </summary>
        public GalRawInputData Raw;
        /// <summary>
        /// 处理后的数据
        /// </summary>
        public GalProcessedInput Processed;
    }
}
