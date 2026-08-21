# Prisoner Diplomacy 1.2 API 實作導讀

[English](API-Cookbook.md) | [繁體中文](API-Cookbook.zh-TW.md)

這是示範 Add-on 的繁中導讀；正式型別、簽名與完整契約以 [`API-Cookbook.md`](API-Cookbook.md) 英文版為準，完整可執行實作位於 [`../Source`](../Source)。

## 1. 最重要的邊界

Add-on 可以：

- 註冊穩定的 Extension、Adapter、Persona、特殊報酬與事件 Metadata ID；
- 讀取俘虜種族、PawnKind、原派系 Def 與派系科技層級；
- 回傳有限的外交價值修正；
- 提供由有效 `ThingDef` 支撐的固定特殊物品報酬；
- 提供只影響敘事語氣的 Persona；
- 讀取公開俘虜、交易、派系、事件快照；
- 使用 `PreviewDemand` 做唯讀、確定性的條件預覽；
- 在三個指定區域繪製唯讀交涉 UI。

Add-on 不可以：

- 建立、接受、改寫、完成或取消 Prisoner Diplomacy 交易；
- 釋放、轉移、殺死、招募或改動 Pawn；
- 自行生成談妥的報酬、扣款或修改派系儲備；
- 修改派系記憶、停火、情報或核心事件紀錄；
- 反射 `PrisonerDiplomacyGameComponent` 或 Patch 私有交易方法；
- 把 AI 文字當成遊戲狀態已完成的證明。

核心永遠是交易權威：它驗證條件、保存協議、確認俘虜交接，並確保報酬只發一次。

## 2. 專案與依賴

目標為 RimWorld 1.6、.NET Framework 4.8、C# 7.3。編譯時參考遊戲 Assembly 與核心 `PrisonerDiplomacy.dll`，所有 Reference 都應設 `<Private>false</Private>`，不要把核心 DLL 複製進 Add-on。

`About/About.xml` 必須宣告：

```xml
<modDependencies>
  <li>
    <packageId>g1061.prisonerdiplomacy</packageId>
    <displayName>Prisoner Diplomacy</displayName>
    <steamWorkshopUrl>steam://url/CommunityFilePage/3787243156</steamWorkshopUrl>
  </li>
</modDependencies>
<loadAfter>
  <li>g1061.prisonerdiplomacy</li>
</loadAfter>
```

這是必要的編譯期整合，請不要宣稱「沒有核心也可運作」。

## 3. 一次性註冊

在 `Mod` 建構子中分別註冊三個公開介面：

```csharp
PrisonerDiplomacyExtensionRegistry.Register(new MyExtension());
PrisonerDiplomacyExtensionRegistry.RegisterPersonaProvider(new MyPersonaProvider());
PrisonerDiplomacyUiExtensionRegistry.Register(new MyHeaderUiExtension());
```

註冊會拒絕空物件、空 ID、不相容 API 版本與重複 ID。ID 建議使用 `作者.套件.功能`，發佈後不要更名，因為它會出現在診斷與存檔相關紀錄中。

示範 Add-on 需要 API `1.2.0` 以上且 Major 必須相同：

```csharp
Version required = new Version("1.2.0");
Version current = new Version(PrisonerDiplomacyBackendApi.ApiVersion);
bool compatible = current.Major == required.Major && current >= required;
```

## 4. 種族／派系 Adapter

`PrisonerDiplomacyRaceContext` 提供：

| 欄位 | 用途 |
| --- | --- |
| `Prisoner` | 當前 Pawn，只讀使用 |
| `Faction` | 原派系／交涉派系，只讀使用 |
| `RaceDefName` | 種族 ThingDef 名稱 |
| `PawnKindDefName` | PawnKind Def 名稱 |
| `FactionDefName` | 派系 Def 名稱 |
| `FactionTechLevel` | 原版 `TechLevel` |

`AppliesTo` 應精準篩選目標：

```csharp
return context != null
    && context.Prisoner != null
    && context.Faction != null
    && context.RaceDefName == "MyDragonRace";
```

`GetDiplomaticValueAdjustment` 回傳整數修正；每個 Adapter 會限制在 `-1000..1000`，多個適用 Adapter 會加總。請保持數值可解釋、有限且確定性。

Adapter 可能在 UI 開啟期間被呼叫多次，必須快速、無副作用、不使用亂數，並處理目錄查詢時 `context == null` 的狀況。

## 5. 特殊報酬

```csharp
yield return new PrisonerDiplomacySpecialRewardDefinition(
    "author.dragon.reward.ember-core",
    "DragonAddon_EmberCoreLabel",
    "DragonAddon_EmberCoreDescription",
    "DragonAddon_EmberCore",
    1);
```

四個字串 ID 都必須穩定；`ThingDef` 必須是可堆疊、具有正市場價值的 Item。核心會依市場價值與數量納入預算／物資上限，保存玩家選擇，並在俘虜完成合法交接後一次性生成。

不要在 Adapter 中呼叫 `ThingMaker`、`GenSpawn`、`Destroy`、庫存保留、亂數或網路服務，也不要提供武器、服裝、建築、任務物品或不穩定的生成物件。

本範例示範：

- 低科技派系提供 2 枚外交印信，每枚市場價值 90；
- 工業以上派系提供 1 份加密外交帳冊，市場價值 600；
- 原版人類價值 `+10`，帝國俘虜再 `+25`。

## 6. Persona

`IPrisonerDiplomacyPersonaProvider` 只回傳簡短人設：

```csharp
return "proud, possessive, formal, status-conscious, and protective of clan honor";
```

它只能影響 AI／模板敘事語氣，不可改變白銀、物資、好感、停火、情報、成功率、期限或事件結果。核心會標準化文字，並依穩定 Provider ID 順序取第一個適用結果。

## 7. 事件 Metadata 的限制

API 1.2 公開四個家族：中立交易點、假投降滲透、公開戰犯審判、贖金伏擊報復。

但外部 `PrisonerDiplomacyEventDefinition` 目前只是可發現的目錄 Metadata。註冊不會排程、觸發、推進或結算事件；公開 API 也沒有暴露核心排程器。

若要做完全自訂事件，Add-on 必須自行擁有 GameComponent、存檔、信件、Incident、重試與結果，只能把 Prisoner Diplomacy 快照當唯讀 Context。不要反射寫入核心事件紀錄。

## 8. 唯讀 Backend API

常用入口：

| 方法 | 用途 |
| --- | --- |
| `GetRegisteredExtensionIds()` | 已註冊 Extension ID |
| `GetEventDefinitions()` | 事件 Metadata 目錄 |
| `GetSpecialRewardOptions(Pawn, Faction)` | 當前 Context 可用特殊報酬 |
| `GetEventSnapshots()` | 核心持久化事件快照 |
| `GetDiplomaticValueAdjustment(Pawn, Faction)` | Adapter 修正合計 |
| `GetPrisonerSnapshots(Map)` | 地圖俘虜快照 |
| `GetFactionSnapshots(Map)` | 地圖派系快照 |
| `TryGetActiveDealSnapshot(Pawn, out ...)` | Pawn 的活動交易 |
| `PreviewDemand(...)` | 不寫入狀態的條件評估 |

快照屬性唯讀，但其中的 Pawn/Faction 仍是真實可變遊戲物件，Add-on 必須把它們當成只讀參考。`PreviewDemand` 不會開始交涉、扣儲備或套冷卻，也不能當成自行發報酬的授權。

## 9. 唯讀 UI Extension

可使用 `FactionHeader`、`PrisonerSummary`、`NegotiationBody` 三個區域。先以 `GetHeight` 回報固定高度，再於核心提供的 Rect 中繪製；不適用時回傳 0。

`PrisonerDiplomacyUiContext` 只提供派系、俘虜、交易快照與 `CompactLayout`。文字過長時必須裁切或縮短，繪製後要恢復 `GUI.color`、`Text.Font`、`Text.Anchor`，不要加入繞過核心交易的按鈕。

核心 Theme 是 internal，不屬於公開 API；Add-on 應自行維護主題，不要反射使用。

## 10. 本地化、測試與複製方式

UI、事件與報酬文字使用 Keyed；物品使用 DefInjected。所有語言的 Key 與 `{0}` 等 Placeholder 必須一致，並使用自己的前綴，別直接沿用本範例 `PDX_`。

遊戲內可從模組設定開啟唯讀 API Inspector，查看註冊、俘虜、派系、交易、報酬與事件快照；開發者模式另有報告、單一俘虜快照、250 白銀條件預覽與 Adapter Context 四類診斷。

請依 [`TestGuide.md`](TestGuide.md) 完成測試。可複製範本位於 [`../Templates`](../Templates)，完整實作位於 [`../Source`](../Source)。開始新 Add-on 時必須先更換 Package ID、Namespace、Def 名稱、翻譯前綴與所有穩定 API ID，再刪除不需要的功能。
