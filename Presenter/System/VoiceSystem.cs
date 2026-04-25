using NiumaGal.Dialogue.Config.Core;
using NiumaGal.Dialogue.RuntimeData;
using NiumaGal.Enum;
using UnityEngine;

namespace NiumaGal.Presenter
{
    /// <summary>
    /// 对话语音播放系统
    /// 持有外部注入的 AudioSource
    /// </summary>
    public class VoiceSystem
    {
        private readonly NiumaGalBlackboard _blackboard;
        private readonly DialogueAudioSO _config;
        private readonly AudioSource _audioSource;

        public VoiceSystem(NiumaGalBlackboard blackboard, DialogueAudioSO config, AudioSource audioSource)
        {
            _blackboard = blackboard;
            _config = config;
            _audioSource = audioSource;
        }

        /// <summary>
        /// 播放指定语音片段
        /// </summary>
        /// <param name="clip"></param>
        public void Play(AudioClip clip)
        {
            if (_audioSource == null || clip == null)
            {
                _blackboard.SetVoiceState(VoiceState.Idle);
                return;
            }

            // 配置音量和混音组
            _audioSource.clip = clip;
            _audioSource.volume = _config?.VoiceVolume ?? 1f;
            if (_config?.VoiceMixerGroup != null)
                _audioSource.outputAudioMixerGroup = _config.VoiceMixerGroup;

            //标记状态
            _blackboard.SetVoiceState(VoiceState.Playing);

            // 播放语音，支持配置的预延迟
             if (_config != null && _config.VoicePreDelay > 0f)
                _audioSource.PlayDelayed(_config.VoicePreDelay);
            else
                _audioSource.Play();
            if (_config != null && _config.VoicePreDelay > 0f)
                _audioSource.PlayDelayed(_config.VoicePreDelay);
            else
                _audioSource.Play();
        }

        /// <summary>
        /// 每帧检测播放完成，由 DialoguePresenter.Update 调用
        /// </summary>
        public void Update()
        {
            if (_blackboard.VoiceState != VoiceState.Playing) return;
            if (_audioSource == null || !_audioSource.isPlaying)
            {
                _blackboard.SetVoiceState(VoiceState.Completed);
            }
        }

        /// <summary>
        /// 强制停止当前语音
        /// </summary>
        public void Stop()
        {
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();

            _blackboard.SetVoiceState(VoiceState.Idle);
        }
    }
}
