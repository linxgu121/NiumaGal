using NiumaGal.Presenter;
using TMPro;
using UnityEngine;

namespace NiumaGal.Test
{
    /// <summary>
    /// 乞丐版对话测试 UI
    /// 挂在一个带 Canvas + TextMeshProUGUI 的物体上即可验证链路
    /// </summary>
    public class TempDialogueUI : MonoBehaviour
    {
        [Header("UI 组件")]
        public TextMeshProUGUI SpeakerText;
        public TextMeshProUGUI ContentText;
        public GameObject DialoguePanel;

        [Header("表现层引用")]
        public DialoguePresenter Presenter;

        private void Start()
        {
            if (Presenter == null)
                Presenter = FindObjectOfType<DialoguePresenter>();

            if (Presenter == null)
            {
                Debug.LogError("[TempDialogueUI] 场景中找不到 DialoguePresenter");
                return;
            }

            Presenter.OnRefreshUI += OnRefresh;
            Presenter.OnAmbientSubtitle += OnAmbientRefresh;
            Presenter.OnCloseUI += OnClose;
            Presenter.OnHideUI += OnHide;

            DialoguePanel?.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Presenter == null) return;
            Presenter.OnRefreshUI -= OnRefresh;
            Presenter.OnAmbientSubtitle -= OnAmbientRefresh;
            Presenter.OnCloseUI -= OnClose;
            Presenter.OnHideUI -= OnHide;
        }

        private void OnRefresh(string speaker, string fullText)
        {
            DialoguePanel?.SetActive(true);
            if (SpeakerText != null) SpeakerText.text = speaker;
            // ContentText 每帧从 TypewriterSystem 读取当前显示长度
        }

        private void OnAmbientRefresh(string speaker, string text)
        {
            // 环境叙事也显示在同一个面板，或你单独开一个小字幕条
            DialoguePanel?.SetActive(true);
            if (SpeakerText != null) SpeakerText.text = speaker;
            if (ContentText != null) ContentText.text = text;
        }

        private void OnClose()
        {
            DialoguePanel?.SetActive(false);
        }

        private void OnHide()
        {
            DialoguePanel?.SetActive(!DialoguePanel.activeSelf);
        }

        private void Update()
        {
            if (Presenter == null || ContentText == null) return;

            // 每帧刷新逐字内容（从 TypewriterSystem 读取当前显示文本）
            var typewriter = Presenter.GetTypewriterDisplayText?.Invoke();
            if (typewriter != null)
                ContentText.text = typewriter;
        }
    }
}
