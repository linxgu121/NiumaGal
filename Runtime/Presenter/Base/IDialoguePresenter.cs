namespace NiumaGal.Presenter
{
    /// <summary>
    /// 对话表现接口
    /// 由声音系统、UI系统等对话表现组件实现
    /// </summary>
     public interface IDialoguePresenter
    {
        /// <summary>播放指定索引的句子</summary>
        void PlaySentence(int sentenceIndex);

        /// <summary>跳过打字机，瞬间显示完整文字</summary>
        void SkipTypewriter();

        /// <summary>停止当前语音</summary>
        void StopVoice();

        /// <summary>关闭对话表现（隐藏 UI 等）</summary>
        void CloseDialogue();

        /// <summary>隐藏/显示 UI</summary>
        void HideUI();
    }
}
