using NiumaGal.Dialogue.Arbitration;
using NiumaGal.Dialogue.Config.Core;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Dialogue.Driver
{
    /// <summary>
    /// 自动播放驱动
    /// 负责在自动播放模式下根据黑板状态自动推进对话
    /// </summary>
    public class AutoPlayDriver : MonoBehaviour
    {
        private float _autoAdvanceDelay = 1.5f;
        private GalArbiter _arbiter;
        private NiumaGalBlackboard _blackboard;
        private float _timer;

        public void Initialize(GalArbiter arbiter, NiumaGalBlackboard blackboard, DialogueCoreSO coreConfig = null)
        {
            _arbiter = arbiter;
            _blackboard = blackboard;
            if (coreConfig != null)
                _autoAdvanceDelay = coreConfig.AutoAdvanceDelay;
        }

        private void Update()
        {
            if (_arbiter == null || _blackboard == null) return;
            if (_blackboard.PlaybackMode != PlaybackMode.Auto) return;
            if (_blackboard.ScriptState != DialogueScriptState.BetweenSentences) return;
            if (_blackboard.LineState != LineState.Completed) return;
            if (_blackboard.VoiceState == VoiceState.Playing) return;

            _timer += Time.deltaTime;
            if (_timer >= _autoAdvanceDelay)
            {
                _timer = 0f;
                _arbiter.AutoPlayTick();
            }
        }

        public void ResetTimer() => _timer = 0f;
    }
}
