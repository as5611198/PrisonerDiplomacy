# Prisoner Diplomacy

[English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | **日本語** | [한국어](README.ko.md)

Prisoner Diplomacy は RimWorld 1.6 向けの、捕虜の身代金、交換、派閥外交を決定論的に処理する Mod です。バニラの通信機、捕虜の釈放、キャラバン移動、そして `PrisonerDiplomacyGameComponent` を取引の権威として維持します。

**バージョン:** `1.2.0`  
**セーブ Schema:** `17`  
**状態:** リリース候補。残る端ケースは公開後のコミュニティ QA で確認します。ビルド、ローカライズ、Smoke、テレメトリの検証は必須です。

## 機能

- バニラの通信機から開始するプレイヤー主導／派閥主導の交渉。
- シルバー、技術階層に応じた物資、友好度、停戦、情報、複合報酬。派閥の予算、備蓄、物資上限、一度きりの履行を検証。
- カウンターオファー、条件の修正、捕虜と誘拐された入植者の交換、補償と返金。
- 信頼、捕虜待遇、遺恨、歴史、人間関係を記録する派閥の記憶。
- 海賊取引の遅延支払い、救援、脱走工作、待ち伏せ、報復。
- 条件を満たす襲撃だけに作用する停戦と一度限りの早期警戒情報。
- 中立ワールドマップ交換、偽装降伏、潜入、公開裁判、救援、身代金待ち伏せなどのイベント。
- 古いセーブの移行と保守的な互換性修復。
- 任意の AI ナラティブと RimChat 共存。AI はデフォルトで文章のみを担当し、結果は決定論的コアが判定。
- 明示的な同意が必要な匿名エラー報告（30／180 日の保持）。
- テーマ付き交渉 UI、派閥ブラウザー、協定／履歴／イベントタブ、開発者診断。
- 種族アダプター、特殊アイテム報酬、イベント、コミュニティ Add-on 用のバージョン付き API。

## インストール

1. `1.6` Mod フォルダーをビルドまたは取得します。
2. `RimWorld/Mods/PrisonerDiplomacy` にコピーします。
3. RimWorld の Mod リストで **Harmony** を Prisoner Diplomacy より前に置きます。
4. 任意の連携は実行時に検出され、必須依存にはなりません。

初心者は[5 言語のプレイヤーガイド](Docs/PlayerGuide/README.md)から始めてください。

## ローカライズ

English、繁体字中国語、簡体字中国語、日本語、韓国語の 5 言語を完全な Keyed 翻訳として収録しています。各言語は 573 Key です。変更後の検証：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateLocalization.ps1
```

## プレイヤー入口

バニラの通信機が完全な能動交渉の入口です。派閥の手紙は通信機なしでも届きます。`PD_PortableDiplomacyTerminal` を所持・着用・装備した入植者には、既知の派閥連絡先を開く Gizmo が表示されます。開発者モードの **Prisoner Diplomacy** カテゴリには、捕虜、提案、カウンターオファー、交換、イベント、世界交換地点、報酬、診断の再現テストがあります。

AI と RimChat は任意の層であり、取引ステートマシン、Pawn、支払い、期限、イベント結果を迂回できません。

## ビルドと Smoke テスト

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
dotnet build .\PrisonerDiplomacy.csproj -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

Smoke テストは外部 AI を呼び出さず、成功時に `Prisoner Diplomacy SmokeTest] PASS cases=127` を出力します。報酬、交換、停戦／情報、海賊リスク、カウンターオファー、イベント、移行、診断、AI ガード、RimChat 隔離、API 登録、オフラインのエラー報告契約を確認します。UI、長い翻訳、縮尺、ボタン、実際のキャラバンイベントは手動確認が必要です。公開前は [`Docs/ReleaseChecklist.md`](Docs/ReleaseChecklist.md) を使用してください。

## ソース構成

フォルダー一覧は [`Source/README.md`](Source/README.md) と [`Docs/Architecture.md`](Docs/Architecture.md) にあります。取引コアは `Core`、公開 API は `Api`、イベントは `Events`、戦略効果は `Strategic`、UI は `UI`、AI／RimChat は `AI` と `Integration`、診断と互換性は `Debug` と `Compatibility` です。

## Mod 作者向け

[`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md) は公開 v1.2 API ガイドです。登録、バージョン、読み取り専用スナップショット、種族／特殊報酬アダプター、ペルソナ、制限付き AI アドバイス、決定論的検証、イベント Add-on を説明します。正式な API シグネチャは英語版を正とします。

[`Compatibility.md`](Compatibility.md)、[`Docs/RewardCatalog.md`](Docs/RewardCatalog.md)、[`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md)、[`Docs/TelemetryPrivacy.md`](Docs/TelemetryPrivacy.md) も参照してください。実際に動作する [`ExampleAddon`](ExampleAddon) には 5 言語、API Inspector、コピー可能なテンプレート、テスト／公開ツールが含まれます。API は fail-closed で、Add-on はセーブ一覧を反射したり内部 GameComponent を呼び出したりしてはいけません。

## コミュニティ

QQ グループ：[戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info)（211784688）。

この Mod は Codex（GPT-5.6 SOL）が 100% 制作し、プロジェクトオーナーはアイデアのみを提供しました。

## ライセンス

| 素材 | 条件 |
| --- | --- |
| C# ソース | Apache License 2.0 と [`LICENSE`](LICENSE) の非商用例外 |
| アート、テクスチャ、スクリーンショット、ブランド | CC BY-NC-ND 4.0（[`ASSET-LICENSE.md`](ASSET-LICENSE.md)） |
| RimWorld、Harmony、RimChat、その他 | 各権利者のライセンス |

コードは Apache 2.0 を基本としますが、派生物や配布版を直接または間接の商業目的に利用することはできません。詳細は [`LICENSE`](LICENSE) を読んでください。

## 開発履歴

変更履歴は [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md) にあります。貢献時は決定論的な権威境界を守り、新しい状態遷移に Debug または Smoke の経路を追加し、API ガイドを更新してください。
