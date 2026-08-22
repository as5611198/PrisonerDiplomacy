# Prisoner Diplomacy

[English](README.md) | [繁體中文](README.zh-TW.md) | **简体中文** | [日本語](README.ja.md) | [한국어](README.ko.md)

Prisoner Diplomacy 是 RimWorld 1.6 的确定性战俘赎金、换俘与派系外交模组。它提供完整谈判流程，同时让原版通讯台、俘虏释放、商队移动和模组自己的 `PrisonerDiplomacyGameComponent` 保持最终权威。

**当前版本：** `1.2.1`
**存档 Schema：** `17`  
**状态：** 可发布候选版。剩余极端情况交由发布后的社区 QA 回报；构建、本地化、Smoke 和遥测检查仍是必要门槛。

## 功能

- 通过原版通讯台进行玩家主动和派系主动的谈判。
- 白银、按科技等级提供的派系物资、好感、停火、情报和混合奖励，并具备财力、储备、物资上限及一次性履约验证。
- 还价与修订条款、用俘虏换回被绑架殖民者，以及补偿／退款处理。
- 持久化派系记忆：可靠度、俘虏待遇、积怨、历史恩怨和关系背景。
- 海盗交易风险、延迟付款、武装救援、越狱压力与报复后果。
- 只影响符合条件的主动袭击的策略停火和一次性预警情报。
- 中立世界地图交易点、假投降与渗透、公开审判、救援和赎金伏击等后续事件。
- 旧存档迁移与保守兼容性修复。
- 可选 AI 叙事和 RimChat 共存；AI 默认只负责文字，确定性核心掌管交易结果。
- 可选、需要同意的匿名错误回报，固定 30／180 天保留期限。
- 主题化谈判 UI、派系浏览器、协议／历史／事件分页和开发者诊断工具。
- 版本化扩展 API，支持种族适配器、特殊物品奖励、事件和社区 Add-on。

## 安装

1. 构建或取得 `1.6` 模组文件夹。
2. 将文件夹复制到 `RimWorld/Mods/PrisonerDiplomacy`。
3. 在 RimWorld 模组列表中让 **Harmony** 排在 Prisoner Diplomacy 前面。
4. 可选整合会在运行时检测，不会变成硬依赖。

新玩家请先阅读[五语玩家指南](Docs/PlayerGuide/README.md)。

## 本地化

模组提供 English、繁体中文、简体中文、日本語与 한국어 五种完整 Keyed 本地化，共 573 个 Key。修改语言文件后运行：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateLocalization.ps1
```

## 玩家入口

原版通讯台是完整的主动谈判入口；派系信件可以在没有通讯台时带来主动提案。携带 `PD_PortableDiplomacyTerminal` 的殖民者可以通过装备 Gizmo 打开已知派系联系人。开发者模式的 **Prisoner Diplomacy** 分类提供可重复的俘虏、提案、还价、交换、事件、世界交易点、奖励和诊断测试。

AI 和 RimChat 都是可选层，不能绕过交易状态机，也不能直接宣称付款、修改 Pawn、期限或事件结果。

## 构建

默认 RimWorld 路径位于 `Directory.Build.props`，也可以覆盖：

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
dotnet build .\PrisonerDiplomacy.csproj -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

项目目标为 `net48`、C# 7.3，并递归包含 `*.cs`。

## Smoke 测试

```powershell
& 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe' `
  -savedatafolder=C:\CodexPDTest `
  -logFile C:\CodexPDTest\SmokeTest.log `
  -quicktest `
  -pdsmoketest `
  -popupwindow
```

成功结束时应看到 `Prisoner Diplomacy SmokeTest] PASS cases=127`。Smoke 涵盖奖励、换俘、停火／情报、海盗风险、还价、事件、迁移、诊断、AI 防护、RimChat 隔离、API 注册和离线错误回报契约；视觉、长文本、缩放、按钮可达性和实际商队事件仍需人工测试。上传前请使用 [`Docs/ReleaseChecklist.md`](Docs/ReleaseChecklist.md)。

## 源代码分类

完整文件夹地图请看 [`Source/README.md`](Source/README.md) 和 [`Docs/Architecture.md`](Docs/Architecture.md)。核心交易位于 `Core`，公开 API 位于 `Api`，事件位于 `Events`，策略后果位于 `Strategic`，界面位于 `UI`，AI／RimChat 位于 `AI` 与 `Integration`，诊断与兼容性位于 `Debug` 与 `Compatibility`。

## Mod 作者文档

[`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md) 是公开 v1.2 API 指南，涵盖注册、版本检查、只读快照、种族／特殊奖励适配器、Persona、有限 AI 建议、确定性验证与事件 Add-on。正式 API 签名以英文文档为准。

[`Compatibility.md`](Compatibility.md) 是玩家兼容性报告；[`Docs/RewardCatalog.md`](Docs/RewardCatalog.md) 是奖励目录；[`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md) 是最小扩展教程；[`ExampleAddon`](ExampleAddon) 是可直接游玩、附五语本地化、API Inspector、可复制模板和发布工具的完整示例；[`Docs/TelemetryPrivacy.md`](Docs/TelemetryPrivacy.md) 说明错误回报隐私。扩展 API 采用 fail-closed 设计，Add-on 不得反射存档列表或调用内部 GameComponent 方法。

## 社区

QQ群：[战俘外交（Prisoner Diplomacy）模组讨论群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info)（群号：`211784688`）。

本模组 100% 由 Codex（GPT-5.6 SOL）制作；项目所有者只提供想法。

## 授权

| 素材 | 条款 |
| --- | --- |
| C# 源代码 | Apache License 2.0，另含 [`LICENSE`](LICENSE) 中的项目非商业例外 |
| 美术、贴图、截图和品牌素材 | CC BY-NC-ND 4.0，见 [`ASSET-LICENSE.md`](ASSET-LICENSE.md) |
| RimWorld、Harmony、RimChat 和其他第三方素材 | 遵循各自作者的许可 |

本项目代码基于 Apache 2.0 授权开源。除原条款外，任何衍生作品、二创整合包或分发版本均不得用于直接或间接商业营利。完整授权与限制请阅读 [`LICENSE`](LICENSE)。

## 开发历史

版本记录位于 [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md)。贡献时请保持确定性权威边界，为新的状态转移加入 Debug 或 Smoke 路径，并同步更新 API 指南。
