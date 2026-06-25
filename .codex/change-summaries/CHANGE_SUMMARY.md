# 변경사항 요약

하네스, 워크플로, 또는 여러 파일에 걸친 구현 변경을 한눈에 확인하기 위한 기록입니다.

## 2026-06-04 - 하네스 결정 가드레일

- 목적: 사용자 결정 지점에 가드레일을 추가하고 중요한 선택을 쉽게 검토할 수 있게 정리했습니다.
- 변경 영역: `AGENTS.md`, `.codex/codex.md`, `.codex/skills`, `.agents/skills`.
- 검증: `git diff --no-index`로 `.codex`와 `.agents` 스킬 내용 일치 여부를 확인했고, `rg`로 가드레일 문구 반영을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-04 - 하네스 문자 깨짐 정리

- 목적: 하네스 문서의 깨진 트리 문자와 상태 표기를 읽기 쉬운 ASCII/한글 표기로 정리했습니다.
- 변경 영역: `AGENTS.md`, `.codex/codex.md`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: `rg`로 깨진 문자열 패턴 잔여 여부를 확인했고, `??` 검색 결과는 정상 C# null-coalescing 예시만 남은 것을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-06 - 하네스 minor 용어 정리

- 목적: 하네스 결정 가드레일의 중요도가 낮은 항목 표현을 더 간결한 `minor`로 통일했습니다.
- 변경 영역: `AGENTS.md`, `.codex/codex.md`, `.codex/skills`, `.agents/skills`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: `rg`로 이전 표현이 남아 있지 않고 `minor decisions`, `minor findings`가 반영된 것을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-08 - 회원가입 이메일 인증 전환

- 목적: 로그인 인증이 아니라 회원가입 전 이메일 인증을 완료해야 계정이 생성되도록 인증 흐름을 수정했습니다.
- 변경 영역: `Auth`, `Contracts/Auth`, `Data/GameDbContext.cs`, `Options`, `Program.cs`, `zombie_servival-3Dgame_Server.http`.
- 검증: `rg`로 로그인 이메일 인증 관련 이전 참조가 남지 않은 것을 확인했고, `dotnet build`가 통과했습니다.
- 남은 사용자 결정: 실제 SMTP 설정값과 기존 DB 스키마 반영 방식 결정이 필요합니다.

## 2026-06-11 - PR 이메일 인증 리뷰 코멘트 반영

- 목적: PR #16의 이메일 인증 리뷰 코멘트 중 로깅, null 방어, SMTP 전송 구현 개선 사항을 반영했습니다.
- 변경 영역: `Auth/DbAuthService.cs`, `Auth/SmtpEmailSender.cs`, `zombie_survival-3Dgame_Server.csproj`.
- 검증: `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: GitHub 리뷰 스레드 해결 표시 및 코멘트 답변 여부는 사용자가 선택해야 합니다.

## 2026-06-18 - 플레이어 스텟 강화 구현

- 목적: 플레이어가 골드를 사용해 전투 스텟을 선택 강화하고, 최종 스텟 조회에 강화 보너스를 반영하도록 구현했습니다.
- 변경 영역: `Player`, `Contracts/Player`, `Inventory/Models`, `Data/GameDbContext.cs`, `Options`, `Program.cs`, `zombie_servival-3Dgame_Server.http`.
- 검증: `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 기존 MySQL DB에 새 `PlayerStatUpgradeStates` 테이블을 반영하는 운영 방식 결정이 필요합니다.

## 2026-06-20 - 스텟 강화   레벨 명확화

- 목적: 플레이어 레벨과 혼동되지 않도록 스텟별 강화 레벨을 `UpgradeLevel`로 명확히 하고, 기본 1레벨에서 시작하도록 정리했습니다.
- 변경 영역: `Player`, `Contracts/Player`, `Data/GameDbContext.cs`.
- 검증: `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 기존 DB에 `Level` 컬럼이 이미 만들어졌다면 `UpgradeLevel` 컬럼으로 수동 반영하거나 개발 DB를 재생성해야 합니다.

## 2026-06-23 - 스텟 강화 저장 조회 도메인 분리

- 목적: `Inventory` 저장소가 스텟 강화 상태를 Include하지 않도록 하고, 스텟 강화 상태 조회는 `Player` 도메인의 서비스에서 직접 처리하도록 분리했습니다.
- 변경 영역: `Inventory/PlayerSaveDataStore.cs`, `Player/PlayerStatUpgradeService.cs`.
- 검증: `dotnet build --no-restore`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-24 - 하네스 코드 변경 및 커밋 규칙 강화

- 목적: 갑작스럽거나 계획되지 않은 코드 변경은 AI가 과도하게 판단하지 않고 개발자에게 세 가지 선택지를 제시한 뒤 선택을 기다리도록 하고, 커밋 시 변경사항을 논리 단위로 세분화하도록 하네스 규칙을 강화했습니다.
- 변경 영역: `AGENTS.md`, `.codex/codex.md`, `.codex/skills/commit/SKILL.md`.
- 검증: `rg`로 신규 가드레일과 커밋 분리 문구 반영을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-24 - 중복 `.agents` 스킬 경로 제거

- 목적: `.codex/skills`를 기준 스킬 경로로 유지하고, 중복 관리되던 `.agents/skills`를 제거해 스킬 수정 지점을 단일화했습니다.
- 변경 영역: `.agents/skills`.
- 검증: `rg`로 `.agents` 참조가 남아 있지 않은 것을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-25 - 로그인 시 플레이어 기본 데이터 보정

- 목적: 테스트 중 계정을 삭제하고 다시 만들지 않아도 로그인 성공 시 누락된 플레이어 저장 데이터와 기본 무기/스텟 값을 자동 보정하도록 했습니다.
- 변경 영역: `Auth/AuthController.cs`, `Inventory`, `Options`, `Program.cs`, `.gitignore`, `player-defaults.example.json`.
- 검증: `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 로컬 `player-defaults.local.json`의 실제 기본 골드, 기본 소유 무기, 기본 스텟 강화 레벨 값은 개발자가 원하는 테스트 값으로 조정하면 됩니다.

## 2026-06-25 - 헤드샷 데미지 배수 스텟 전환

- 목적: 플레이어 치명타 확률 스텟과 무기 치명타 배율 용어를 헤드샷 데미지 배수로 통일했습니다.
- 변경 영역: `Player`, `Firearm`, `Inventory`, `Gacha`, `WeaponUpgrade`, `Contracts`, `Data/GameDbContext.cs`, `appsettings.json`, `player-defaults.example.json`.
- 검증: `rg`로 이전 `CriticalMultiplier` 참조가 제거되고 `CriticalChance`는 legacy 보정 상수만 남은 것을 확인했으며, `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 기존 MySQL DB를 유지하려면 `FirearmDefinitions`와 `PlayerWeaponStates`의 `CriticalMultiplier` 컬럼을 `HeadshotDamageMultiplier`로 수동 변경하거나 개발 DB를 재생성해야 합니다.
