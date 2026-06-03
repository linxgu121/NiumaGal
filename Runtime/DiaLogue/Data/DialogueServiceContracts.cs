using NiumaGal.Dialogue.Data;
using NiumaGal.Save;

namespace NiumaGal.Dialogue.Service
{
    /// <summary>
    /// 对话查询接口。UI、任务、剧情条件只依赖查询能力，不应获得命令能力。
    /// </summary>
    public interface IDialogueQuery
    {
        long Revision { get; }
        bool IsDialoguePlaying { get; }
        string CurrentDialogueId { get; }
        int CurrentSentenceIndex { get; }
        DialoguePlaybackSnapshot GetPlaybackSnapshot();
        DialogueViewData BuildViewData();
        bool IsDialogueRead(string dialogueId);
    }

    /// <summary>
    /// 对话命令接口。外部模块通过它启动、推进、选择和关闭对话。
    /// </summary>
    public interface IDialogueCommand
    {
        DialogueOperationResult StartDialogue(DialogueStartRequest request);
        DialogueOperationResult Advance(DialogueAdvanceRequest request);
        DialogueOperationResult SelectChoice(DialogueChoiceSelectRequest request);
        DialogueOperationResult ForceClose(DialogueCloseRequest request);
    }

    /// <summary>
    /// 对话组合服务接口。存档导出只放在组合服务上，避免只读查询方获得存档能力。
    /// </summary>
    public interface IDialogueService : IDialogueQuery, IDialogueCommand
    {
        DialogueProgressSnapshot ExportProgressSnapshot();
        DialogueOperationResult ImportProgressSnapshot(DialogueProgressSnapshot snapshot);
    }

    /// <summary>
    /// 对话配置能力接口。后续根控制器热更新 DialogueAsset 列表时使用。
    /// </summary>
    public interface IDialogueConfigurationService
    {
        void SetDialogueAssets(DialogueAsset[] dialogueAssets);
        void SetConditionResolver(IDialogueConditionResolver resolver);
        void SetActionHandler(IDialogueActionHandler handler);
        void SetProgressStore(NiumaGalProgressStore progressStore);
    }

    /// <summary>
    /// 对话条件解析器。Quest、Story、Inventory、MiniGame 等模块可通过桥接实现。
    /// </summary>
    public interface IDialogueConditionResolver
    {
        bool IsConditionMet(in DialogueConditionContext context);
    }

    /// <summary>
    /// 对话行为处理器。Gal 核心只请求行为，不直接引用具体业务模块。
    /// </summary>
    public interface IDialogueActionHandler
    {
        DialogueOperationResult Execute(in DialogueActionContext context);
    }
}
