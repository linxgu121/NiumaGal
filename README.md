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
- `DialogueRoot` 或 `UIRoot/UIBridges/DialogueToolkitBridge`：挂 `NiumaGalToolkitDialogueViewBridge`，绑定 UIToolkitUIManager、DialoguePresenter、NiumaDialogueController。第三人称项目建议把 Cursor Mode 保持为 `VisibleWhenChoices`，这样只有出现选项时才解锁并显示鼠标；准心和 HUD 可拖入 `Hide Objects During Dialogue` 临时隐藏。
- `DialogueRoot` 或 `UIRoot/UIBridges/AmbientNarrativeToolkitBridge`：正式 UI Toolkit 场景挂 `NiumaGalToolkitAmbientNarrativeBridge`，绑定 DialoguePresenter、UIToolkitUIManager 和 WorldCamera。旧 `AmbientNarrativeUIViewBridge` 只用于历史测试场景，不再推荐。
- `DialogueRoot`：挂 `NiumaGalProgressStore`，保存已读对话和已触发环境叙事 ID。
- `DialogueRoot/SaveAdapter` 或全局 `SaveRoot/GalSaveAdapter`：挂 `NiumaGalSaveAdapter`，手动绑定 NiumaGalProgressStore 和 NiumaSaveController；调试阶段可开启自动查找。
- `NPC_xxx`：挂 `NiumaDialogueInteractable`，绑定该 NPC 的 DialogueAsset，通过 NiumaInteract 触发对话。
- `NPC_xxx` 或 `StoryTrigger_xxx`：临时测试可挂 `SimpleDialogueTrigger`，正式流程建议走 Interact 或 Story。
- `Ambient_xxx`：环境叙事物体挂 `AmbientDialogueDriver`，绑定 AmbientAsset、PlayerTransform、DialoguePresenter 和 NiumaGalProgressStore；如果开启视线检测，必须设置 ObstructionMask。
- `MonologueTrigger_xxx`：近距离独白触发区挂 `ProximityMonologueDriver`，物体需要 Collider 且 IsTrigger=true，绑定 MonologueAsset、DialoguePresenter 和 NiumaGalProgressStore。
- `UIToolkitViewRegistrySO`：注册 `DialogueWindow`，并由 `DialogueToolkitBindingProvider` 绑定对话 UXML 元素。若使用选项，确认 ChoiceRoot / ChoiceButtonNames 或 `niuma-dialogue-choice` USS class 已配置。
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

### 选项显示与鼠标控制
对话选项只会在当前句文字播放完成后显示。这样可以避免玩家在句子没读完时提前点击分支。

`NiumaGalToolkitDialogueViewBridge` 的 Cursor Mode 有三种：

- `DoNotControl`：Gal 不处理鼠标，交给其他系统管理。
- `VisibleDuringDialogue`：整个正式对话期间显示鼠标。
- `VisibleWhenChoices`：只有出现选项时显示鼠标，默认推荐。

第三人称角色控制器通常会锁定鼠标，导致 UI Button 无法点击。使用 `VisibleWhenChoices` 后，选项出现时会临时执行：

```csharp
Cursor.visible = true;
Cursor.lockState = CursorLockMode.None;
```

选项提交、对话关闭或 UI 隐藏后，会恢复进入选项 UI 前的鼠标状态。

### 对话期间隐藏准心和 HUD
正式对话开始后，建议隐藏准心、交互提示和普通 Gameplay HUD，避免玩家误以为仍处于自由操作状态。

`NiumaGalToolkitDialogueViewBridge` 提供 `Hide Objects During Dialogue`：拖入需要临时隐藏的 GameObject，例如交互提示根物体、普通准心 UI、小地图、任务追踪面板、Gameplay HUD 根节点等。桥接层会记录每个物体进入对话前的 `activeSelf`，结束时按原状态恢复。

推荐绑定方式：

1. 在 `DialogueRoot/NiumaGalToolkitDialogueViewBridge` 或 `UIRoot/UIBridges/DialogueToolkitBridge` 上配置 `Hide Objects During Dialogue`。
2. 把 `UIRoot/GameplayHUD`、`UIRoot/InteractionPrompt`、`UIRoot/Reticle` 等普通 Gameplay UI 拖进隐藏列表。
3. 不要把承载 `DialogueWindow` 的 Toolkit Root 或桥接物体拖进隐藏列表，否则会把对话框自己隐藏掉。

如果选项不显示，优先检查：

1. `ChoiceId` 是否为空。为空的选项会被 UI 桥接层过滤。
2. `ChoiceRoot` 和 `ChoiceSlots` 是否已绑定。
3. `ChoiceSlots` 数组数量是否少于当前句子的选项数量。
4. 当前句子文字是否已经播放完成。文字没完成时选项不会显示。
5. 选项条件是否未满足，并且勾选了 `HideWhenUnavailable`。

## 协作边界
Gal 负责“说什么、显示什么、玩家选什么”。任务推进、场景切换、小游戏入口、剧情 Flag 等行为由外部 Handler 实现。

## 场景挂载与 Inspector 配置
### NiumaDialogueController
建议挂载位置：`CoreScene/BootstrapRoot/GameplayServicesRoot/GalRoot`，或只在剧情测试场景中临时放置。

用途：创建对话服务，管理 DialogueAsset、对话黑板、Presenter 和行为处理器。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Dialogue Assets` | 拖所有对话资产 | 不建议 | 只能播放外部直接传入的对话，按 ID 查找会失败 |
| `Presenter Provider` | 拖 `DialoguePresenter` 或自制 Presenter | 不可以 | 对话逻辑可推进，但不会显示 UI |
| `Action Handler Provider` | 拖对话行为桥接脚本，例如 MiniGame / Audio / Quest Handler | 可以 | 对话 Action 不执行或被跳过 |
| `Register Service To Context` | 核心场景开启 | 可以关闭 | 其他模块无法从 GameContext 获取对话服务 |

### NiumaGalToolkitDialogueViewBridge
建议挂载位置：`CoreScene/BootstrapRoot/UIRoot/UIBridges/DialogueToolkitBridge`，也可以和 `DialoguePresenter` 放在同一个 `DialogueRoot` 上。

用途：UI Toolkit 对话窗口桥接。它把 `DialoguePresenter` 和 `DialogueService` 的当前句子、打字机文本、选项数据转换为 NiumaUI 的 `DialogueToolkitViewData`，再推给 `DialogueToolkitBindingProvider`。NiumaUI 2.1.0 起不再保留旧 UGUI 对话桥接，新场景统一使用本脚本。

推荐场景层级：

```text
CoreScene/BootstrapRoot
├── GameplayServicesRoot
│   └── GalRoot
│       ├── NiumaDialogueController
│       └── DialoguePresenter
└── UIRoot
    ├── UIManager
    │   ├── UIToolkitUIManager
    │   └── UIToolkitViewFactory
    ├── UIToolkitRoot
    │   └── BindingProviders
    │       └── DialogueToolkitBindingProvider
    └── UIBridges
        └── DialogueToolkitBridge
            └── NiumaGalToolkitDialogueViewBridge
```

`UIToolkitViewRegistrySO` 中需要注册一条：

| ViewId | LayerId | BindingProviderId | InputPolicy | InputBlockMode | 说明 |
| --- | --- | --- | --- | --- | --- |
| `DialogueWindow` | `Dialogue` | `DialogueWindow` | `BlockGameplayInput` | `Dialogue` | 对话窗口 |

`NiumaGalToolkitDialogueViewBridge` 字段：

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `UI Manager` | 拖 `UIRoot/UIManager` 上的 `UIToolkitUIManager` | 不建议 | 对话数据不会显示到 Toolkit 窗口 |
| `Presenter` | 拖 `DialogueRoot/GalRoot` 上的 `DialoguePresenter` | 不可以 | 无法收到对话刷新、文本完成和关闭事件 |
| `Dialogue Controller` | 拖 `DialogueRoot/GalRoot` 上的 `NiumaDialogueController` | 不可以 | 无法读取选项，也无法提交玩家选择 |
| `Dialogue View Id` | 默认 `DialogueWindow`，要与注册表 ViewId 一致 | 不建议 | ViewId 不匹配时窗口打不开 |
| `Auto Open View` | 建议开启 | 可以 | 关闭后需要其他脚本先打开 `DialogueWindow` |
| `Close View On Dialogue Close` | 建议开启 | 可以 | 对话结束后窗口可能留在屏幕上 |
| `Use Typewriter Text` | 需要逐字显示时开启 | 可以 | 关闭后直接显示完整句子 |
| `Cursor Mode` | 第三人称项目推荐 `VisibleWhenChoices` | 可以 | `DoNotControl` 时鼠标由其他系统管理 |
| `Hide Objects During Dialogue` | 拖准心、交互提示、小地图等 Gameplay UI 根物体 | 可以 | 对话期间这些 UI 不会自动隐藏 |

`DialogueToolkitBindingProvider` 字段在 NiumaUI README 中配置。策划只需要保证 UXML 里有对应 name，例如 `SpeakerText`、`BodyText`、`ContinueHint`、`ChoiceRoot`，并给选项按钮填写 `ChoiceButtonNames` 或添加 `niuma-dialogue-choice` USS class。

### NiumaGalToolkitAmbientNarrativeBridge
建议挂载位置：`CoreScene/BootstrapRoot/UIRoot/UIBridges/AmbientNarrativeToolkitBridge`，也可以和 `DialoguePresenter` 放在同一个 `DialogueRoot` 上。

用途：UI Toolkit 环境叙事桥接。它监听 `DialoguePresenter` 的 Ambient 事件，把 `AmbientDialogueDriver` / `ProximityMonologueDriver` 播放出的气泡或旁白字幕转换为 NiumaUI 的 `AmbientNarrativeToolkitViewData`，再推给 `AmbientNarrativeToolkitBindingProvider`。

推荐场景层级：

```text
CoreScene/BootstrapRoot
├── GameplayServicesRoot
│   └── GalRoot
│       └── DialoguePresenter
└── UIRoot
    ├── UIManager
    │   ├── UIToolkitUIManager
    │   └── UIToolkitViewFactory
    ├── UIToolkitRoot
    │   └── BindingProviders
    │       └── AmbientNarrativeToolkitBindingProvider
    └── UIBridges
        └── AmbientNarrativeToolkitBridge
            └── NiumaGalToolkitAmbientNarrativeBridge
```

`UIToolkitViewRegistrySO` 中需要注册一条：

| ViewId | LayerId | BindingProviderId | InputPolicy | InputBlockMode | 说明 |
| --- | --- | --- | --- | --- | --- |
| `AmbientNarrative` | `Prompt` | `AmbientNarrative` | `None` | `Menu` | 环境叙事气泡 / 旁白字幕 |

`NiumaGalToolkitAmbientNarrativeBridge` 字段：

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `UI Manager` | 拖 `UIRoot/UIManager` 上的 `UIToolkitUIManager` | 不建议 | 环境叙事不会显示到 Toolkit View |
| `Presenter` | 拖 `DialogueRoot/GalRoot` 上的 `DialoguePresenter` | 不可以 | 无法收到 Ambient 开始、刷新、关闭事件 |
| `Ambient View Id` | 默认 `AmbientNarrative`，要与注册表 ViewId 一致 | 不建议 | ViewId 不匹配时窗口打不开 |
| `Auto Open View` | 建议开启 | 可以 | 关闭后需要其他脚本先打开 `AmbientNarrative` |
| `Close View On Ambient Closed` | 建议开启 | 可以 | 关闭后会刷新空数据但不关闭 View |
| `World Camera` | 拖玩家主相机；留空时用 `Camera.main` | 可以 | Bubble 模式无法找到相机时不能跟随 NPC |
| `Bubble Screen Offset` | 建议 `(0, 80)` 起步 | 可以 | 气泡会贴近目标中心点 |
| `Update Bubble Position Every Frame` | NPC 或玩家会移动时开启 | 可以 | 关闭后 Bubble 只在文本刷新时更新位置 |

`AmbientNarrativeToolkitBindingProvider` 字段在 NiumaUI README 中配置。策划只需要保证 UXML 里有对应 name，例如 `AmbientBubbleRoot`、`BubbleBodyText`、`AmbientSubtitleRoot`、`SubtitleBodyText`。

### NiumaGalSaveAdapter
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot/SaveAdapters`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Dialogue Controller` | 拖 `NiumaDialogueController` | 不建议 | 已读对话、环境叙事触发记录不存档 |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | 无法注册存档 Provider |


