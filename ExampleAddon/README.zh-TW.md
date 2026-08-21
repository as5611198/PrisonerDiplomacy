# Prisoner Diplomacy：示範 Add-on

[English](README.md) | **繁體中文**

這是一個可實際遊玩、附完整原始碼的 RimWorld 1.6 擴展模組，用來示範 Prisoner Diplomacy 1.2 公開 API。其他作者可以直接訂閱、測試與參考，不需要碰觸核心交易的私有狀態。

## 安裝後會示範什麼？

- 使用穩定 ID 與 API 版本檢查註冊 `IPrisonerDiplomacyExtension`。
- 一個確定性 Context Adapter，同時讀取種族、PawnKind、派系 Def 與科技層級。
- 兩種自訂 `ThingDef` 特殊報酬；價格、驗證、存檔與一次性發放全部由核心處理。
- 小幅外交價值修正：原版人類 `+10`，帝國俘虜再額外 `+25`。
- 帝國、海盜與部落的有限敘事 Persona。
- v1.2 四種事件家族的可發現 Metadata。
- 一條使用唯讀 Context 的 `IPrisonerDiplomacyUiExtension` Header 資訊列。
- 唯讀 API Inspector，可查看擴展 ID、俘虜、派系、交易、報酬、事件定義與事件快照。
- 透過開發者工具示範 `PreviewDemand` 與快照診斷。
- English、繁中、簡中、日文與韓文。

低科技派系會提供兩枚**外交印信**，工業以上派系會提供一份**加密外交帳冊**。這些物品會出現在正常的報酬選擇器，而且只有核心確認正確交接後才會發放。

## 需求

- RimWorld 1.6
- [Prisoner Diplomacy](https://steamcommunity.com/sharedfiles/filedetails/?id=3787243156) 1.2 或相容的 API 1.x 版本
- Harmony（由核心模組依賴）

請將本 Add-on 排在 Prisoner Diplomacy 後面。Package ID 為 `g1061.prisonerdiplomacy.exampleaddon`。

## 遊戲內使用

1. 啟用 Prisoner Diplomacy 與本 Add-on。
2. 抓到合格俘虜，開啟正常的 Prisoner Diplomacy 交涉視窗。
3. Header 會顯示範例 Adapter 的價值修正與特殊報酬數量。
4. 低科技與工業／太空派系會顯示不同特殊物品。
5. 前往**模組設定 > Prisoner Diplomacy：示範 Add-on > 開啟唯讀 API Inspector**。
6. 開發者模式可在 **Prisoner Diplomacy Example Add-on** 分類找到快照、預覽、Context 與報告工具。

## 建置

在 Prisoner Diplomacy 儲存庫根目錄執行：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\Build.ps1
```

專案會參考 `..\1.6\Assemblies\PrisonerDiplomacy.dll`，輸出到 `ExampleAddon\1.6\Assemblies`。在其他位置建置時可覆寫 `RimWorldDir` 或 `PrisonerDiplomacyRoot`。

部署兩個模組後可執行隔離載入測試：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\ExampleAddon\Tools\RunLoadSmoke.ps1
```

它會建立暫存 Savedata，只載入 Harmony、已擁有 DLC、核心與本 Add-on；要求 `PASS cases=127`、檢查三項註冊，並攔截 Add-on 貼圖／翻譯錯誤。腳本只會安全關閉自己啟動的測試程序。

## 文件

- [`Docs/API-Cookbook.md`](Docs/API-Cookbook.md)：英文權威 API 導讀與契約表。
- [`Docs/API-Cookbook.zh-TW.md`](Docs/API-Cookbook.zh-TW.md)：繁中導讀。
- [`Docs/TestGuide.md`](Docs/TestGuide.md)：玩家與作者測試矩陣。
- [`Templates`](Templates)：最小擴展、自訂種族 Adapter 與唯讀 UI 模板。
- [`Workshop`](Workshop)：五語 Steam 描述與上傳說明。

## 最重要的權威邊界

本 Add-on 不會釋放／轉移 Pawn、建立／完成交易、生成議定報酬、修改派系儲備或呼叫內部事件排程。它只回傳 Metadata 並讀取快照；Prisoner Diplomacy 是唯一交易權威。

API 1.2 的外部事件定義只是目錄 Metadata，註冊不等於排程。完全自訂事件必須把狀態保存在自己的 GameComponent，並只用 Prisoner Diplomacy 快照作為唯讀 Context，直到公開排程／執行契約推出。

## 授權

C# 原始碼採 Apache License 2.0 加上 [`LICENSE`](LICENSE) 中的非商業例外。美術、貼圖、截圖與品牌素材依 [`ASSET-LICENSE.md`](ASSET-LICENSE.md) 採 CC BY-NC-ND 4.0。

本 Add-on 100% 由 Codex（GPT-5.6 SOL）製作；本人只提供想法。
