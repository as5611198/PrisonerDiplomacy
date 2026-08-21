# Error Telemetry Privacy Notice / 錯誤遙測隱私說明

Effective date / 生效日期: 2026-08-21

## English

Prisoner Diplomacy error telemetry is optional. The mod sends nothing until it detects an error in a guarded Prisoner Diplomacy operation and you choose **Send this report**, **Allow for this session**, or **Always send future reports**. Persistent consent skips later prompts and can be revoked at any time in the mod settings. Declining or disabling telemetry has no effect on gameplay.

### Data included

- A random event ID and a SHA-256 error fingerprint.
- The exception type, sanitized exception message and sanitized stack trace.
- RimWorld and Prisoner Diplomacy versions.
- A bounded anonymous deal snapshot: deal ID, state, origin, negotiation round, and delivery flags.
- Active mod Package IDs and versions.
- Server receipt time. Cloudflare necessarily processes the request IP address; the Worker stores only a salted hash in a short-lived rate-limit bucket, never the raw address.

The mod does not upload saves, screenshots, Pawn records, colony data or hardware identifiers. It attempts to remove local paths, operating-system user and machine names, and recognized API keys, tokens and secrets before upload, and the server applies the same class of redaction again. Exception text is created by game and mod code, so automated redaction cannot guarantee that arbitrary user-created text is never present. Review this limitation before consenting.

### Purpose and processing

Reports are used only to group crashes, diagnose Prisoner Diplomacy defects and prepare human-reviewed repair candidates. Cloudflare Workers validates requests, D1 stores indexes and aggregates, and R2 stores detailed JSON reports.

When AI analysis is enabled, representative samples from an error group are sent first to the official Google Gemini API using Gemini 3.7 Flash for classification. Only reports classified as likely internal Prisoner Diplomacy defects with the configured confidence and severity are sent to **AI-HUB** (`ai.aiyuhub.com`), an OpenAI-compatible third-party API relay, for a GPT 5.6 Sol repair candidate. AI-HUB may forward the request to upstream model infrastructure under its own service terms. The model receives the sanitized exception, stack trace, anonymous deal snapshot, versions and bounded active-mod summary described above, plus bounded excerpts of the matching source files fetched from a fixed commit in the project's public GitHub repository. The public source excerpts add no player data. The model does not receive an account identifier from this system. Provider changes require this notice to be updated before the new provider handles production reports.

The production AI stages are currently disabled until their production-only credentials are installed. Error collection and retention continue to work without either AI provider.

The system never downloads code to players, applies runtime hot fixes, edits saves or lets an AI control game state.

### Retention

- Detailed event indexes and R2 JSON reports: 30 days from server receipt.
- Aggregated error statistics, AI audit rows and repair candidates: 180 days from the last accepted event.
- Salted rate-limit buckets: approximately two minutes.

Cleanup is automatic. Because reports contain no account or stable player identifier, an individual report normally cannot be linked back to a player for targeted deletion; the fixed retention limits are the deletion mechanism.

## 繁體中文

《戰俘外交》的錯誤遙測是選用功能。只有模組在受保護的《戰俘外交》流程中偵測到錯誤，而且你選擇「傳送這次回報」、「本次遊戲期間允許」或「之後一律傳送」後，才會傳送資料。永久同意會略過之後的詢問視窗，並可隨時在模組設定中撤銷。拒絕或停用錯誤回報不影響遊戲。

### 會包含的資料

- 隨機事件 ID 與 SHA-256 錯誤指紋。
- 例外類型、經清理的例外訊息與呼叫堆疊。
- RimWorld 與《戰俘外交》版本。
- 有上限的匿名交易快照：交易 ID、階段、來源、談判回合與交付旗標。
- 啟用模組的 Package ID 與版本。
- 伺服器接收時間。Cloudflare 在處理網路請求時必然會接觸 IP 位址；Worker 只在短期限流資料中保存加鹽雜湊，不保存原始 IP。

模組不會上傳存檔、截圖、Pawn 紀錄、殖民地資料或硬體識別資訊。上傳前會嘗試移除本機路徑、作業系統使用者與機器名稱，以及可辨識的 API Key、權杖與秘密資訊；伺服器會再次執行同類清理。例外文字由遊戲與模組程式產生，自動清理無法保證任意玩家自訂文字永遠不會出現，請在同意前留意此限制。

### 用途與處理方式

回報只用於彙整錯誤、診斷《戰俘外交》的缺陷，以及準備需人工審查的修復候選。Cloudflare Workers 負責驗證請求，D1 保存索引與統計，R2 保存詳細 JSON。

啟用 AI 分析後，每個錯誤群組的代表樣本會先傳送至 Google 官方 Gemini API，由 Gemini 3.7 Flash 進行分類。只有被判定為《戰俘外交》內部問題，且達到設定信心與嚴重度門檻的回報，才會再傳送至 **AI-HUB**（`ai.aiyuhub.com`）這個相容 OpenAI API 的第三方中轉服務，由 GPT 5.6 Sol 產生修復候選。AI-HUB 可能依其服務條款把請求轉送至上游模型基礎設施。模型會收到前述經清理的例外、堆疊、匿名交易快照、版本與有上限的啟用模組摘要，另加上從本專案公開 GitHub 倉庫固定 commit 取得的相關原始碼片段；公開原始碼片段不會增加玩家資料。本系統不會提供玩家帳號識別碼。日後更換供應商時，必須先更新本說明，新的供應商才可處理正式回報。

正式環境的 AI 階段目前仍關閉，直到正式環境專用憑證完成設定。即使不啟用任何 AI 供應商，錯誤接收與保存期限機制仍會正常運作。

系統不會向玩家下載程式碼、不會套用執行期熱修補、不會修改存檔，也不會讓 AI 控制遊戲狀態。

### 保存期限

- 詳細事件索引與 R2 JSON：自伺服器收到起 30 天。
- 錯誤彙整統計、AI 稽核紀錄與修復候選：自最後一筆接受事件起 180 天。
- 加鹽限流雜湊：大約兩分鐘。

系統會自動清理。由於回報不含帳號或穩定的玩家識別碼，通常無法把單筆資料重新連結到特定玩家後個別刪除；固定保存期限就是刪除機制。
