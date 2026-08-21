# Prisoner Diplomacy

[English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md) | **한국어**

Prisoner Diplomacy는 RimWorld 1.6용 결정론적 전쟁 포로 몸값, 교환 및 세력 외교 모드입니다. 바닐라 통신기, 죄수 석방, 대상단 이동과 `PrisonerDiplomacyGameComponent`를 거래의 최종 권위로 유지하면서 완전한 협상 흐름을 제공합니다.

**현재 버전:** `1.2.0`  
**세이브 Schema:** `17`  
**상태:** 출시 후보. 남은 예외 상황은 출시 후 커뮤니티 QA로 확인하며 빌드, 현지화, Smoke와 텔레메트리 검증은 필수입니다.

## 기능

- 바닐라 통신기에서 시작하는 플레이어／세력 주도 협상.
- 은화, 기술 단계별 물자, 우호도, 휴전, 정보와 복합 보상. 세력 예산, 비축량, 물자 한도와 1회성 이행을 검증합니다.
- 역제안과 조건 수정, 포로 한 명과 납치된 정착민의 교환, 보상과 환불.
- 신뢰, 포로 대우, 원한, 역사와 관계를 기억하는 세력 기억.
- 해적 거래의 지급 지연, 구출, 탈옥 선동, 매복과 보복.
- 조건에 맞는 능동 습격에만 적용되는 전략 휴전과 1회성 조기 경보 정보.
- 중립 월드맵 교환 지점, 항복 위장과 침투, 공개 재판, 구출과 몸값 매복 이벤트.
- 이전 세이브 마이그레이션과 보수적인 호환성 복구.
- 선택적 AI 서사와 RimChat 공존. AI는 기본적으로 텍스트만 담당하고 결정론적 코어가 결과를 판정합니다.
- 동의가 필요한 익명 오류 보고와 30／180일 보존 정책.
- 테마형 협상 UI, 세력 브라우저, 계약／역사／이벤트 탭과 개발자 진단.
- 종족 어댑터, 특수 아이템 보상, 이벤트와 커뮤니티 Add-on을 위한 버전 API.

## 설치

1. `1.6` 모드 폴더를 빌드하거나 준비합니다.
2. `RimWorld/Mods/PrisonerDiplomacy`에 복사합니다.
3. RimWorld 모드 목록에서 **Harmony**를 Prisoner Diplomacy보다 먼저 로드합니다.
4. 선택 통합은 실행 중 감지되며 필수 의존성이 되지 않습니다.

초보자는 [5개 언어 플레이어 가이드](Docs/PlayerGuide/README.md)부터 읽어 주세요.

## 현지화

English, 번체 중국어, 간체 중국어, 일본어, 한국어 5개 언어의 573개 Keyed 번역을 제공합니다. 변경 후 검사:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\ValidateLocalization.ps1
```

## 플레이어 진입점

바닐라 통신기가 능동 협상의 완전한 진입점입니다. 통신기가 없어도 세력 편지가 도착할 수 있습니다. `PD_PortableDiplomacyTerminal`을 소지, 착용 또는 장비한 정착민은 장비 Gizmo로 알려진 세력 연락처를 열 수 있습니다. 개발자 모드의 **Prisoner Diplomacy** 분류에는 포로, 제안, 역제안, 교환, 이벤트, 월드 교환 지점, 보상과 진단을 재현하는 도구가 있습니다.

AI와 RimChat은 선택 계층이며 거래 상태 머신, Pawn, 지급, 기한 또는 이벤트 결과를 우회할 수 없습니다.

## 빌드와 Smoke 테스트

```powershell
dotnet build .\PrisonerDiplomacy.csproj -c Release -t:Rebuild --nologo
dotnet build .\PrisonerDiplomacy.csproj -c Release -p:RimWorldDir="D:\Games\RimWorld"
```

Smoke 테스트는 외부 AI를 호출하지 않고 성공 시 `Prisoner Diplomacy SmokeTest] PASS cases=127`을 출력합니다. 보상, 교환, 휴전／정보, 해적 위험, 역제안, 이벤트, 마이그레이션, 진단, AI 보호, RimChat 격리, API 등록과 오프라인 오류 보고 계약을 확인합니다. UI, 긴 번역, 배율, 버튼, 실제 대상단 이벤트는 수동 확인이 필요합니다. 출시 전 [`Docs/ReleaseChecklist.md`](Docs/ReleaseChecklist.md)를 사용하세요.

## 소스 구조

폴더 지도는 [`Source/README.md`](Source/README.md)와 [`Docs/Architecture.md`](Docs/Architecture.md)에 있습니다. 거래 코어는 `Core`, 공개 API는 `Api`, 이벤트는 `Events`, 전략 효과는 `Strategic`, UI는 `UI`, AI／RimChat은 `AI`와 `Integration`, 진단／호환성은 `Debug`와 `Compatibility`에 있습니다.

## Mod 제작자 문서

[`PrisonerDiplomacyApi.md`](PrisonerDiplomacyApi.md)는 등록, 버전, 읽기 전용 스냅샷, 종족／특수 보상 어댑터, 페르소나, 제한된 AI 조언, 결정론적 검증과 이벤트 Add-on을 설명하는 공개 v1.2 가이드입니다. 공식 API 시그니처는 영어 문서를 기준으로 합니다.

[`Compatibility.md`](Compatibility.md), [`Docs/RewardCatalog.md`](Docs/RewardCatalog.md), [`Docs/AddonQuickstart.md`](Docs/AddonQuickstart.md), [`Docs/TelemetryPrivacy.md`](Docs/TelemetryPrivacy.md)도 참고하세요. 실제로 실행 가능한 [`ExampleAddon`](ExampleAddon)에는 5개 언어, API Inspector, 복사 가능한 템플릿, 테스트／배포 도구가 포함됩니다. API는 fail-closed이며 Add-on은 세이브 목록을 리플렉션하거나 내부 GameComponent를 호출하면 안 됩니다.

## 커뮤니티

QQ 그룹: [戰俘外交（Prisoner Diplomacy）模組討論群](https://qun.qq.com/universal-share/share?ac=1&authKey=kO4hgI4yAGKZaIMkDgtwdF7V9G9aylRatK8pqb&busi_data=eyJncm91cENvZGUiOiIyMTE3ODQ2ODgiLCJ0b2tlbiI6InJMNDZ0VDd2RnhHSjhBbE51dVhQOUR6NTNhMlR4cjdhQUVNcmVlUzQybGJTMEg4MHd2ZGlxT1JLWXBYdDVNQXMiLCJ1aW4iOiIzODMxMDIzMDUwIn0%3D&data=ii_Z7GGfk0K0tX3nuOIWOG9w0Vt8TpomZx82ytn1-cooF1oRHAXYR8Nss77V5VBQER3K33djQUT_bNS6Lt1UXg&svctype=4&tempid=h5_group_info) (`211784688`).

이 모드는 Codex(GPT-5.6 SOL)가 100% 제작했으며 프로젝트 소유자는 아이디어만 제공했습니다.

## 라이선스

| 자료 | 조건 |
| --- | --- |
| C# 소스 코드 | Apache License 2.0 및 [`LICENSE`](LICENSE)의 비상업 예외 |
| 아트, 텍스처, 스크린샷, 브랜드 | CC BY-NC-ND 4.0（[`ASSET-LICENSE.md`](ASSET-LICENSE.md)） |
| RimWorld, Harmony, RimChat 및 기타 | 각 권리자의 라이선스 |

코드는 Apache 2.0을 기반으로 하지만 파생물이나 배포본을 직접 또는 간접적인 상업적 이익에 사용할 수 없습니다. 자세한 내용은 [`LICENSE`](LICENSE)를 읽어 주세요.

## 개발 기록

변경 기록은 [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md)에 있습니다. 기여할 때는 결정론적 권한 경계를 유지하고, 새 상태 전이에 Debug 또는 Smoke 경로를 추가하며 API 가이드를 업데이트하세요.
