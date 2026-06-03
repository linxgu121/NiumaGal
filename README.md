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
- 用于任务或剧情的对话 ID 一旦发布不要修改，避免存档和任务目标断链。

## 场景使用方法
推荐放置方式：`DialogueRoot` 管全局对话播放，NPC 物体只放触发入口和 DialogueAsset 引用。

- `DialogueRoot`：挂 `NiumaDialogueController`，绑定 DialogueAsset 列表、输入推进设置和 TPC 阻塞引用。
- `DialogueRoot`：在 `NiumaDialogueController` 的 `Action Handler Provider` 上绑定实现 `IDialogueActionHandler` 的组件，例如 `MiniGameDialogueActionHandler`；若暂时不绑定，可开启自动查找用于调试。
- `DialogueRoot`：在 `NiumaDialogueController` 的 `Condition Resolver Provider` 上绑定实现 `IDialogueConditionResolver` 的组件，用于任务、剧情、背包等条件判断。
- `DialogueRoot`：挂 `DialoguePresenter`，负责文本播放、打字机和关闭 UI。
- `DialogueRoot`：挂 `NiumaUIDialogueViewBridge`，绑定 UIManager、DialoguePresenter、NiumaDialogueController。
- `DialogueRoot`：挂 `NiumaGalProgressStore`，保存已读对话和已触发环境叙事 ID。
- `DialogueRoot/SaveAdapter` 或全局 `SaveRoot`：挂 `NiumaGalSaveAdapter`。
- `NPC_xxx`：挂 `NiumaDialogueInteractable`，绑定该 NPC 的 DialogueAsset，通过 NiumaInteract 触发对话。
- `NPC_xxx` 或 `StoryTrigger_xxx`：临时测试可挂 `SimpleDialogueTrigger`，正式流程建议走 Interact 或 Story。
- `Ambient_xxx`：环境叙事物体挂 `AmbientDialogueDriver` 或 `ProximityMonologueDriver`，绑定 AmbientAsset / MonologueAsset。
- `UIRoot/DialogueWindow`：放对话框 Binding。若使用选项，确认 ChoiceRoot / ChoiceButtonTemplate 已绑定或允许自动生成。
- 进入 MiniGame 的对话选项不再放旧二选一面板。建议建 `MiniGameGalBridge` 物体挂 `MiniGameDialogueActionHandler`，再拖给 `NiumaDialogueController.Action Handler Provider`；也可以把该脚本直接挂在 `DialogueRoot` 上。

## 协作边界
Gal 负责“说什么、显示什么、玩家选什么”。任务推进、场景切换、小游戏入口、剧情 Flag 等行为由外部 Handler 实现。


