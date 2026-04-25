using System;
using NiumaGal.Dialogue.Config.Core;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// 打字机逐字显示系统
    /// 由 DialoguePresenter 驱动 Update
    /// </summary>
    public class TypewriterSystem
    {
        private readonly NiumaGalBlackboard _blackboard;
        private readonly DialogueCoreSO _config;

        /// <summary>
        /// 当前正在显示的完整文本
        /// </summary>
        private string _fullText;
        /// <summary>
        /// 当前已显示的文本长度
        /// </summary>
        private int _currentLength;
        /// <summary>
        /// 打字机计时器
        /// </summary>
        private float _timer;

        /// <summary>
        /// 构造函数，注入黑板和配置
        /// </summary>
        public TypewriterSystem(NiumaGalBlackboard blackboard, DialogueCoreSO config)
        {
            _blackboard = blackboard;
            _config = config;
        }

        /// <summary>
        /// 启动新句子的逐字显示
        /// </summary>
        public void Start(string text)
        {
            _fullText = text ?? string.Empty;
            _currentLength = 0;
            _timer = 0f;
            _blackboard.SetLineState(LineState.Playing);
        }

         /// <summary>
        /// 每帧驱动，由 DialoguePresenter.Update 调用
        /// </summary>
        public void Update(float deltaTime)
        {
            // 仅在正在逐字显示且有文本时更新
            if (_blackboard.LineState != LineState.Playing) return;
            // 当文本为空时直接完成
            if (string.IsNullOrEmpty(_fullText)) return;

            // 获取配置的打字机间隔，默认为 0.05 秒
            float interval = _config?.TypewriterInterval ?? 0.05f;

            // 间隔为 0 时瞬间完成
            if (interval <= 0f)
            {
                _currentLength = _fullText.Length;
                _blackboard.SetLineState(LineState.Completed);
                return;
            }

            _timer += deltaTime;
            while (_timer >= interval && _currentLength < _fullText.Length)
            {
                _timer -= interval;
                _currentLength++;
                OnCharacterTyped?.Invoke(_fullText[_currentLength - 1]);
            }

            if (_currentLength >= _fullText.Length)
            {
                _blackboard.SetLineState(LineState.Completed);
            }
        }

        /// <summary>
        /// 跳过打字机，瞬间显示完整文字
        /// </summary>
        public void Skip()
        {
            if (_blackboard.LineState != LineState.Playing) return;
            _currentLength = _fullText?.Length ?? 0;
            _blackboard.SetLineState(LineState.Completed);
        }

        /// <summary>
        /// 获取当前应显示的文本片段
        /// 供 UI 每帧读取刷新
        /// </summary>
        public string GetCurrentDisplayText()
        {
            if (_fullText == null) return string.Empty;
            return _fullText.Substring(0, _currentLength);
        }

        /// <summary>
        /// 逐字显示事件，供音效订阅
        /// </summary>
        public event Action<char> OnCharacterTyped;
    }
}