# NiumaGal

## 模块定位
NiumaGal 是对话与环境叙事模块，负责 DialogueAsset 播放、句子推进、选项、条件、行为分发、已读进度、环境叙事触发和 UI 对话框桥接。

## 框架设计思路
- DialogueService 负责协议层逻辑：启动、推进、选择、关闭、条件判断、Action 转发。
- Presenter 负责表现层播放：文本、打字机、语音、关闭 UI。
- 行为不写死在 Gal 内部，通过 IDialogueActionHandler 转发给 Scene、Quest、Story、MiniGame 等模块。
- 条件通过 IDialogueConditionResolver 扩展，避免 Gal 直接依赖任务、背包、剧情等实现。

## 核心流程
1. 外部调用 NiumaDialogueController.StartDialogue 或 StartDialogueById。
2. DialogueService 查找 DialogueAsset，校验起始句条件，执行 OnStart / EnterActions。
3. Presenter 播放当前句子并刷新 UI。
4. 玩家推进或选择选项。
5. DialogueService 执行选项 Actions，再 Continue / Jump / End。
6. 完整结束时记录已读 DialogueId，SaveAdapter 导出已读与环境叙事进度。

## 模块用法
- DialogueAsset 必须填写稳定 DialogueId，正式内容不要依赖 asset.name。
- 选项进入小游戏时，给 Choice 配置 ActionType.OpenMiniGame，并由 MiniGameDialogueActionHandler 执行场景切换。
- 不同选项跳转不同对话内容时，给当前句子的 Choices 配置不同 ChoiceId 和 NextSentenceId，并把 Behavior 设为 JumpToSentence。
- 用于任务或剧情的对话 ID 一旦发布不要修改，避免存档和任务目标断链。

## 场景使用方法
推荐放置方式：`DialogueRoot` 管全局对话播放，NPC 物体只放触发入口和 DialogueAsset 引用。

- `DialogueRoot`：挂 `NiumaDialogueController`，绑定 DialogueAsset 列表、输入推进设置和 TPC 阻塞引用。
- `DialogueRoot`：在 `NiumaDialogueController` 的 `Action Handler Provider` 上绑定实现 `IDialogueActionHandler` 的组件，例如 `MiniGameDialogueActionHandler`；若暂时不绑定，可开启自动查找用于调试。
- `DialogueRoot`：在 `NiumaDialogueController` 的 `Condition Resolver Provider` 上绑定实现 `IDialogueConditionResolver` 的组件，用于任务、剧情、背包等条件判断。
- `DialogueRoot`：挂 `DialoguePresenter`，负责文本播放、打字机和关闭 UI。
- `DialogueRoot`：挂 `NiumaUIDialogueViewBridge`，绑定 UIManager、DialoguePresenter、NiumaDialogueController。
- `DialogueRoot` 或 `UIRoot/AmbientNarrative`：挂 `AmbientNarrativeUIViewBridge`，绑定 DialoguePresenter、气泡 UI、字幕 UI 和 WorldCamera。
- `DialogueRoot`：挂 `NiumaGalProgressStore`，保存已读对话和已触发环境叙事 ID。
- `DialogueRoot/SaveAdapter` 或全局 `SaveRoot/GalSaveAdapter`：挂 `NiumaGalSaveAdapter`，手动绑定 NiumaGalProgressStore 和 NiumaSaveController；调试阶段可开启自动查找。
- `NPC_xxx`：挂 `NiumaDialogueInteractable`，绑定该 NPC 的 DialogueAsset，通过 NiumaInteract 触发对话。
- `NPC_xxx` 或 `StoryTrigger_xxx`：临时测试可挂 `SimpleDialogueTrigger`，正式流程建议走 Interact 或 Story。
- `Ambient_xxx`：环境叙事物体挂 `AmbientDialogueDriver`，绑定 AmbientAsset、PlayerTransform、DialoguePresenter 和 NiumaGalProgressStore；如果开启视线检测，必须设置 ObstructionMask。
- `MonologueTrigger_xxx`：近距离独白触发区挂 `ProximityMonologueDriver`，物体需要 Collider 且 IsTrigger=true，绑定 MonologueAsset、DialoguePresenter 和 NiumaGalProgressStore。
- `UIRoot/DialogueWindow`：放对话框 Binding。若使用选项，确认 ChoiceRoot / ChoiceSlots 已绑定。NiumaUI 不再自动创建保底选项按钮。
- 进入 MiniGame 的对话选项不再放旧二选一面板。建议建 `MiniGameGalBridge` 物体挂 `MiniGameDialogueActionHandler`，再拖给 `NiumaDialogueController.Action Handler Provider`；也可以把该脚本直接挂在 `DialogueRoot` 上。

### 不同选项跳转不同对话
这是 Gal 分支对话的基础用法，适合 NPC 问答、进入小游戏确认、任务分支、剧情分歧等。

示例流程：

```text
SentenceId: npc_intro
文本：要不要玩一局你画我猜？

选项 A：进入你画我猜
ChoiceId: enter_draw_guess
Behavior: JumpToSentence
NextSentenceId: enter_minigame_confirm

选项 B：下次再说
ChoiceId: maybe_next_time
Behavior: JumpToSentence
NextSentenceId: goodbye
```

配置步骤：

1. 打开对应 `DialogueAsset`。
2. 给每个 `DialogueSentence` 填稳定 `SentenceId`，例如 `npc_intro`、`enter_minigame_confirm`、`goodbye`。
3. 在需要分支的句子上展开 `Choices`。
4. 每个选项填写唯一 `ChoiceId`。
5. `DisplayText` 填按钮显示文本。
6. `Behavior` 选择 `JumpToSentence`。
7. `NextSentenceId` 填要跳转到的目标句子 ID。
8. 目标句子必须存在于同一个 `DialogueAsset.Sentences` 中。

常用行为：

- `Continue`：选择后按句子数组顺序继续播放下一句。
- `JumpToSentence`：选择后跳到 `NextSentenceId` 指定句子。
- `EndDialogue`：选择后直接结束当前对话。
- `Custom`：先执行选项 Actions；如果填写了 `NextSentenceId`，再跳转到目标句子。

推荐规范：

- `ChoiceId` 使用稳定业务 ID，例如 `accept_quest`、`enter_draw_guess`、`refuse_help`。
- `SentenceId` 不要用中文，不要随剧情文本一起频繁改名。
- `NextSentenceId` 为空时不要使用 `JumpToSentence`，否则会返回结构化失败。
- 需要隐藏未满足条件的选项时，给 Choice 配 `Conditions` 并勾选 `HideWhenUnavailable`。
- 需要显示灰色不可选按钮时，配置 `Conditions`，不勾 `HideWhenUnavailable`，并填写 `DisabledText`。

### 选项跳转场景或触发业务
如果选项不是跳到另一句，而是要进入小游戏、接任务、推进剧情或切场景，不建议直接在 UI Button 事件里做。推荐通过 Choice 的 `Actions` 转发给外部模块。

进入 MiniGame 示例：

1. 当前句子配置一个选项：
   - `ChoiceId = enter_draw_guess`
   - `DisplayText = 进入你画我猜`
   - `Behavior = Custom`
2. 在该 Choice 的 `Actions` 中添加：
   - `Type = OpenMiniGame`
   - `TargetId` 或 `StringValue` 填小游戏入口 ID / 场景 ID，具体由 `MiniGameDialogueActionHandler` 解释。
3. 如果进入小游戏前还要播放一句确认文本，可以同时填写：
   - `NextSentenceId = enter_minigame_confirm`
4. `NiumaDialogueController.Action Handler Provider` 绑定实现 `IDialogueActionHandler` 的桥接组件。

这样 UI 只负责显示选项，Gal 负责记录选择，MiniGame / Scene / Quest / Story 负责执行业务。

## 协作边界
Gal 负责“说什么、显示什么、玩家选什么”。任务推进、场景切换、小游戏入口、剧情 Flag 等行为由外部 Handler 实现。


