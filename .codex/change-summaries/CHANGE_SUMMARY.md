# 변경사항 요약

하네스, 워크플로, 또는 여러 파일에 걸친 구현 변경을 한눈에 확인하기 위한 기록입니다.

## 2026-06-27 - 기능 단위 커밋 세분화 규칙 강화

- 목적: 기능 개발 변경사항을 한 번에 묶지 않고 DTO, 모델/엔티티, 영속성, 설정, 서비스 로직, 컨트롤러, 검증/문서 단위로 세분화해 커밋하도록 커밋 스킬과 git 전담 에이전트 지침을 강화했습니다.
- 변경 영역: `.codex/skills/commit/SKILL.md`, `.codex/agents/git-manager.toml`, `.codex/agents/_catalog/workflow/git-manager.md`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: `rg`로 세분화 기준 반영을 확인했고, 훅 종료 품질 게이트를 실행했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-26 - 훅 실행 wrapper 추가

- 목적: `.codex/hooks.json`의 인라인 PowerShell 변수 확장 및 실행 cwd 의존 문제로 훅이 실행 전에 실패하던 문제를 방지하기 위해 공통 실행 wrapper와 절대 경로 호출을 추가했습니다.
- 변경 영역: `.codex/hooks.json`, `.codex/hooks/run_hook.ps1`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: wrapper 경유 훅 실행, repo 밖 cwd 실행, 빈 payload no-op 처리, `hooks.json` 파싱, 기존 훅 self-test를 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-06-26 - PR #19 훅 리뷰 코멘트 반영

- 목적: PR #19에 올라온 훅 리뷰 코멘트를 반영해 경로 파싱, 날짜 파싱, 변경 요약 검증, 비밀번호 감지, 프롬프트 추출 로직을 보강했습니다.
- 변경 영역: `.codex/hooks/ZombieHookCommon.ps1`, `.codex/hooks/pre_tool_guard.ps1`, `.codex/hooks/stop_quality_gate.ps1`, `.codex/hooks/user_prompt_context.ps1`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: PowerShell 훅 self-test, JSON/TOML 파싱, 종료 품질 게이트를 실행했습니다.
- 남은 사용자 결정: GitHub 리뷰 스레드에 답변하거나 해결 표시할지는 사용자가 선택하면 됩니다.

## 2026-06-25 - 서브 에이전트 폴더 정리와 성능/범위 가드 추가

- 목적: 루트에 흩어진 서브 에이전트 초안을 공식 `.codex/agents` 구조로 정리하고, 성능 리뷰 스킬과 과도한 단독 판단/광범위 코드 변경 제한 훅을 추가했습니다.
- 변경 영역: `.codex/agents`, `.codex/skills/performance-review`, `.codex/skills/code-review/SKILL.md`, `.codex/skills/security-checklist/SKILL.md`, `.codex/hooks`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: 에이전트 TOML 파싱, 스킬 frontmatter 대체 검증, PowerShell 훅 self-test, stale 참조 검색을 실행했습니다. `quick_validate.py`는 로컬 Python의 `yaml` 모듈 누락으로 실행되지 않았습니다.
- 남은 사용자 결정: Codex에서 `/hooks`로 신규/변경 훅을 검토하고 신뢰 처리할지 결정하면 됩니다.

## 2026-06-25 - 좀비 서버 Codex 훅 추가

- 목적: 다른 프로젝트 훅을 직접 복사하지 않고, 현재 좀비 서바이벌 서버의 스킬과 규칙에 맞춘 Codex 훅을 추가했습니다.
- 변경 영역: `.codex/hooks.json`, `.codex/hooks`, `.gitignore`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 검증: PowerShell 훅 스크립트 self-test, stdin payload 테스트, `hooks.json` 파싱, 종료 품질 게이트 실행을 확인했습니다.
- 남은 사용자 결정: Codex에서 `/hooks`로 새 프로젝트 훅을 검토하고 신뢰 처리할지 결정하면 됩니다.

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

## 2026-06-25 - PR #18 리뷰 코멘트 반영

- 목적: PR #18에 올라온 미해결 리뷰 코멘트 9건을 반영해 스텟 강화와 기본 데이터 보정 로직의 런타임 예외 및 데이터 유실 가능성을 줄였습니다.
- 변경 영역: `Player/PlayerStatsCalculator.cs`, `Player/PlayerController.cs`, `Player/PlayerStatUpgradeService.cs`, `Inventory/PlayerDefaultDataRepairService.cs`.
- 반영 내용:
  - 스텟 강화 비용 계산에서 `Math.Pow` 결과가 `NaN`, `Infinity`, `int.MaxValue` 이상이면 `int.MaxValue`로 제한해 오버플로우로 인한 음수 비용 가능성을 차단했습니다.
  - 스텟 조회 계산에서 중복 `StatName`이 있어도 `ToDictionary` 예외가 발생하지 않도록 루프 기반 딕셔너리 구성으로 바꾸고, 중복 중 더 높은 강화 레벨을 사용하도록 했습니다.
  - `CriticalChance`에서 `HeadshotDamageMultiplier`로 레거시 스텟명을 보정할 때 양쪽 행이 모두 있으면 기존 레거시 강화 레벨을 삭제하지 않고 더 높은 레벨을 보존하도록 했습니다.
  - 레거시 보정 및 스텟 강화 조회에서 `SingleOrDefault`를 `FirstOrDefault`로 바꿔 중복 데이터가 있어도 API가 크래시되지 않도록 했습니다.
  - 스텟 강화 API 요청 본문에 `[FromBody]`를 명시하고, 요청 본문 또는 `StatName`이 비어 있으면 `400 Bad Request`를 반환하도록 했습니다.
  - `PlayerDefaultData` 설정의 `WeaponStates` 또는 `StatUpgradeLevels`가 누락/null이어도 기본 데이터 보정 중 `NullReferenceException`이 발생하지 않도록 방어했습니다.
- 검증: `dotnet build`가 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: GitHub 리뷰 스레드에 답변/해결 표시를 할지, 그리고 이 수정사항을 별도 커밋 후 push할지는 사용자가 선택해야 합니다.

## 2026-07-18 - Docker Compose 기반 EC2 배포 및 MySQL S3 백업

- 목적: 외부 DB 없이 단일 EC2에서 앱, MySQL 8.4.10, S3 백업 서비스를 운영하고 앱 실패 시 데이터 볼륨을 유지한 채 직전 이미지로 복구하도록 배포 체계를 구성했습니다.
- 변경 영역: `Program.cs`, 프로젝트 패키지와 .NET 10 타깃, `Data/Migrations`, `Dockerfile`, `compose.yaml`, `deployment`, GitHub CI/CD, 환경 변수 예시와 배포 문서.
- 검증:
  - .NET 10 Release restore/build/test가 경고와 오류 없이 통과했습니다.
  - EF 모델과 최초 Migration 사이에 미반영 변경이 없음을 확인했습니다.
  - 앱과 백업 이미지를 linux/amd64로 빌드했고 앱이 UID 1654로 실행되며 Docker 헬스체크를 포함하는지 확인했습니다.
  - 빈 MySQL에서 Migration 1건과 총기 시드 5건이 생성되고 앱 재시작 시 중복되지 않음을 확인했습니다.
  - MySQL 컨테이너 재생성 후 검증용 사용자와 플레이어 저장 데이터가 유지됨을 확인했습니다.
  - `/health`가 정상 DB에서 200, DB 중지 시 503, 복구 후 200을 반환함을 확인했습니다.
  - 백업 이미지의 AWS CLI 2.27.49와 mysqldump 8.4.10 실행을 확인했고 앱 이미지 레이어에 JWT, DB, SMTP 비밀 설정 흔적이 없음을 확인했습니다.
- 남은 사용자 결정: S3 버킷 기본 암호화·30일 Lifecycle, EC2 IAM Role, GitHub Environment Secret을 실제 인프라에 설정해야 합니다. 기존 `EnsureCreated()` 운영 DB가 있다면 최초 배포 전 baseline 또는 빈 DB 이전 절차를 별도로 승인해야 합니다.

## 2026-07-22 - 로컬 우선 및 일반 Linux SSH 배포 전환

- 목적: 유료 AWS 리소스 없이 Windows Docker Desktop에서 먼저 운영 검증하고, 이후 개인 Linux 서버가 준비되면 동일 구성을 GitHub Actions SSH로 배포하도록 전환했습니다.
- 변경 영역: `app.env.example`, `compose.yaml`, `.github/workflows/CD.yml`, `deployment/scripts`, 배포 문서.
- 반영 내용:
  - 실제 비밀값이 들어 있던 예제 파일을 Git에서 제외되는 `app.env`로 격리하고 예제는 빈 템플릿으로 재생성했습니다.
  - 노출됐던 MySQL 앱/root 비밀번호와 JWT 키를 새 랜덤 값으로 교체하는 로컬 준비 스크립트를 추가했습니다.
  - SSH 배포 대상을 EC2 전용 Secret에서 일반 Linux용 `SSH_HOST`, `SSH_USER`, `SSH_KEY`로 변경했습니다.
  - `SSH_DEPLOY_ENABLED=true`일 때만 원격 배포하며, 그 전에는 GHCR 이미지 발행만 수행합니다.
  - S3 백업은 `BACKUP_ENABLED=true`일 때만 실행하도록 선택 기능으로 변경했습니다.
- 검증: 로컬 환경 파일의 비밀값 설정 여부만 확인하고 값 자체는 출력하지 않았으며, Compose와 CD 정적 검증을 수행했습니다.
- 남은 사용자 결정: 노출된 기존 Gmail 앱 비밀번호를 Google 계정에서 폐기하고 새 앱 비밀번호를 `app.env`에 입력해야 합니다. 개인 Linux 서버 준비 후 SSH Secret과 `SSH_DEPLOY_ENABLED` 설정이 필요합니다.

