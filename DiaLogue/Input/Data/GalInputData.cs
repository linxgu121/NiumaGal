namespace NiumaGal.DiaLogue.Input.Data
{
    /// <summary>
    /// 堆内存输入黑板
    /// 供全局读取，外部系统只能读取引用，禁止直接修改内部字段
    /// </summary>
    public class GalInputData
    {
        /// <summary>
        /// 当前帧输入数据
        /// </summary>
        public GalFrameInputData currentFrameData;
        /// <summary>
        /// 上一帧输入数据
        /// </summary>
        public GalFrameInputData lastFrameData;
    }
}
