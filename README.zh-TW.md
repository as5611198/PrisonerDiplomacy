# Prisoner Diplomacy

[English](README.md) | **繁體中文** | [简体中文](README.zh-CN.md) | [日本語](README.ja.md) | [한국어](README.ko.md)

Prisoner Diplomacy 是 RimWorld 1.6 的確定性戰俘贖金、換俘與派系外交模組。它提供完整交涉流程，同時讓原版通訊台、俘虜釋放、旅隊移動與模組自己的 `PrisonerDiplomacyGameComponent` 保持最終權威。

**目前版本：** `1.2.0`  
**存檔 Schema：** `17`  
**狀態：** 可發布候選版。剩餘極端情況交由發布後社群 QA 回報；建置、本地化、Smoke 與遙測閘門仍是必要檢查。

## 功能

- 透過原版通訊台進行玩家主動與派系主動的交涉。
- 白銀、依科技層級提供的派系物資、好感、停火、情報與混合報酬，並具備財力、儲備、物資上限及一次性履約驗證。
- 還價與修訂條件、以俘虜換回遭綁架殖民者，以及補償／退款處理。
- 持久化派系記憶：可靠度、俘虜待遇、積怨、歷史恩怨與人際脈絡。
- 海盜交易風險、延遲付款、武裝救援、越獄壓力與報復後果。
- 只影響符合條件主動襲擊的策略停火與一次性預警情報。
- 中立世界地圖交易點、假投降與滲透、公開審判、救援與贖金伏擊等後續事件。
- 舊版存檔遷移與保守相容性修復。
- 選配 AI 敘事與 RimChat 共存；AI 預設只負責文字，確定性核心掌管交易結果。
- 選配、須同意的匿名錯誤回報，固定 30／180 天保留期限。
- 主題化交涉 UI、派系瀏覽器、協議／歷史／事件分頁與開發者診斷工具。
- 版本化擴展 API，支援種族 Adapter、特殊物品報酬、事件與社群 Add-on。

## 安裝

1. 建置或取得 `1.6` 模組資料夾。
2. 將資料夾複製到 `RimWorld/Mods/PrisonerDiplomacy`。
3. 在 RimWorld 模組清單中讓 **Harmony** 排在 Prisoner Diplomacy 前面。
4. 選用整合會在執行時偵測，不會變成硬依賴。

新手請先閱讀[五語玩家指南](Docs/PlayerGuide/README.md)。

## 本地化

模組提供 English、繁體中文、简体中文、日本語與 한국어 五種完整 Keyed 本地化，共 573 個 Key。修改語言檔後執行：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateLocalization.ps1
```

## 玩家入口

原版通訊台是完整的主動交涉入口；派系信件可在沒有通訊台時帶來主動提案。攜帶 `PD_PortableDiplomacyTerminal` 的殖民者可透過裝備 Gizmo 開啟已知派系聯絡人。開發者模式的 **Prisoner Diplomacy** 分類提供可重現的俘虜、提案、還價、交換、事件、世界交易點、獎勵與診斷測試。

AI 與 RimChat 都是選配層，不能繞過交易狀態機或直接宣稱付款、改動 Pawn、期限或事件結果。

## 建置

預設 RimWorld 路徑位於 `Directory.Build.props`，也可以覆寫：

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
dotnet build .\PrisonerDiplomacy.csproj -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

專案目標為 `net48`、C# 7.3，會遞迴包含 `*.cs`。

## Smoke 測試

```powershell
& 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe' `
  -savedatafolder=C:\CodexPDTest `
  -logFile C:\CodexPDTest\SmokeTest.log `
  -quicktest `
  -pdsmoketest `
  -popupwindow
```

成功結束時應看到 `Prisoner Diplomacy SmokeTest] PASS cases=127`。Smoke 涵蓋報酬、換俘、停火／情報、海盜風險、還價、事件、遷移、診斷、AI 防護、RimChat 隔離、API 註冊與離線錯誤回報契約；視覺、長文字、縮放、按鈕可達性與實際旅隊事件仍須人工測試。上傳前請使用 [`Docs/ReleaseChecklist.md`](Docs/ReleaseChecklist.md)。

## 原始碼分類

完整資料夾地圖請看 [`Source/README.md`](Source/README.md) 與 [`Docs/Architecture.md`](Docs/Architecture.md)。

| 區域 | 路徑 |
| --- | --- |
| 核心交易、報酬、紀錄與經濟 | [`Source/PrisonerDiplomacy/Core`](Source/PrisonerDiplomacy/Core) |
| 公開 API 與範例 Add-on | [`Source/PrisonerDiplomacy/Api`](Source/PrisonerDiplomacy/Api) |
| 事件與中立世界地圖交換 | [`Source/PrisonerDiplomacy/Events`](Source/PrisonerDiplomacy/Events) |
| 策略後果與襲擊 | [`Source/PrisonerDiplomacy/Strategic`](Source/PrisonerDiplomacy/Strategic) |
| UI 與主題 | [`Source/PrisonerDiplomacy/UI`](Source/PrisonerDiplomacy/UI) |
| 選配 AI 與 RimChat | [`Source/PrisonerDiplomacy/AI`](Source/PrisonerDiplomacy/AI)、[`Integration`](Source/PrisonerDiplomacy/Integration) |
| 診斷與相容性 | [`Debug`](Source/PrisonerDiplomacy/Debug)、[`Compatibility`](Source/PrisonerDiplomacy/Compatibility) |

## Mod 作者文件

[`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md) 是公開 v1.2 API 指南，涵蓋註冊、版本檢查、唯讀快照、種族／特殊報酬 Adapter、Persona、有限 AI 建議、確定性驗證與事件 Add-on。正式 API 簽名以英文文件為準。

[`Compatibility.md`](Compatibility.md) 是玩家相容性報告；[`Docs/RewardCatalog.md`](Docs/RewardCatalog.md) 是報酬目錄；[`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md) 是最小擴展教學；[`Docs/TelemetryPrivacy.md`](Docs/TelemetryPrivacy.md) 說明錯誤回報隱私。擴展 API 採 fail-closed 設計，Add-on 不得反射存檔清單或呼叫內部 GameComponent 方法。

## 社群

QQ 討論群：[戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info)（群號：`211784688`）。

本模組 100% 由 Codex（GPT-5.6 SOL）製作；本人只提供想法。

## 授權

| 素材 | 條款 |
| --- | --- |
| C# 程式碼 | Apache License 2.0，另含 [`LICENSE`](LICENSE) 的專案非商業例外 |
| 美術、貼圖、截圖與品牌素材 | CC BY-NC-ND 4.0，見 [`ASSET-LICENSE.md`](ASSET-LICENSE.md) |
| RimWorld、Harmony、RimChat 與其他第三方素材 | 依各自作者授權 |

本專案程式碼基於 Apache 2.0 授權開源。除原條款外，任何衍生作品、二創整合包或分發版本均不得用於直接或間接之商業營利行為。完整授權與限制請閱讀 [`LICENSE`](LICENSE)。

## 開發歷史

版本紀錄位於 [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md)。貢獻時請維持確定性權威邊界，為新的狀態轉移加入 Debug 或 Smoke 路徑，並同步更新 API 指南。
