# 변경사항 요약

하네스, 워크플로, 또는 여러 파일에 걸친 구현 변경을 한눈에 확인하기 위한 기록입니다.

## 2026-08-26 - 공개 서버 배포 후속 보강

- 목적: 개발 검증과 운영 배포를 분리한 상태에서 DuckDNS 공개 서버를 안전하게 갱신하고, 실패한 운영 배포를 이전 상태로 복구할 수 있도록 마무리했습니다.
- 변경 영역: Development/Main CI·CD, Caddy·Compose, DuckDNS updater, 배포 트랜잭션·Migration 검사, 상태 확인 API와 운영 문서.
- 반영 내용: 개발 CI/CD는 `develop`·`release` push와 PR에서 각각 코드 검증과 일회성 배포 검증을 수행합니다. Main CD는 최신 `main` CI 이미지 digest만 고유 staging으로 전송하며, `/live` 외부 검증 실패 시 앱·프록시·백업 설정을 롤백하고 `deployment/backups`는 보존합니다.
- 검증: .NET 10 Release 빌드와 자동 테스트 7개, EF pending-model 검사, Compose 렌더링, Bash 문법, expand-first Migration 정책과 `git diff --check`를 통과했습니다. Docker가 꺼진 Windows 환경에서 건너뛴 Linux 트랜잭션·DuckDNS·Caddy 기동 검증은 GitHub Actions에서 최종 확인해야 합니다.
- 남은 사용자 결정: 공유기 TCP 80/443 포트포워딩과 Ubuntu DuckDNS 토큰·GitHub production Secret을 설정한 뒤 모바일 네트워크에서 `/live`를 확인해야 합니다.

## 2026-08-02 - 운영·개발 CI/CD 독립 분리

- 목적: 운영 배포와 개발 배포 검증의 책임을 분리하고, 각 CI와 CD를 GitHub Actions에서 독립 실행 이력으로 확인할 수 있도록 구성했습니다.
- 변경 영역: `.github/workflows/Main-CI.yml`, `.github/workflows/Main-CD.yml`, `.github/workflows/Development-CI.yml`, `.github/workflows/Development-CD.yml`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 반영 내용: `main` 전용 CI 성공 push만 운영 이미지 게시와 Linux 호스트 배포로 연결했습니다. `develop`·`release` 전용 CI가 성공하면 개발 CD가 별도 러너에서 앱·MySQL 이미지를 빌드하고 임시 Docker Compose 환경을 기동해 `/health`를 확인한 뒤 컨테이너와 볼륨을 제거하며, GHCR 게시나 원격 서버 배포는 수행하지 않습니다.
- 검증: 워크플로 트리거·이름 연결·성공 조건·검증 완료 commit SHA checkout·운영 배포 제한을 정적으로 확인하고, `git diff --check`, .NET Release 빌드·테스트, Docker Compose 설정 렌더링과 임시 배포 헬스체크를 수행합니다.
- 남은 사용자 결정: `workflow_run` 기반 Main CD와 Development CD가 실행되도록 네 워크플로를 GitHub 기본 브랜치 `main`에 반영해야 합니다.

## 2026-08-01 - CI/CD 독립 워크플로 분리

- 목적: CI와 CD를 GitHub Actions 목록에서 각각 확인할 수 있도록 분리하면서, 성공한 CI 이후에만 자동 배포되도록 구성했습니다.
- 변경 영역: `.github/workflows/CI.yml`, `.github/workflows/CD.yml`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 반영 내용: CI의 재사용 CD 호출 작업을 제거하고, CD가 `main`, `release`, `develop`의 성공한 push CI 완료를 `workflow_run`으로 감지해 별도 실행되도록 했습니다. PR·수동 실행에서 발생한 CI 완료는 배포하지 않습니다.
- 검증: `git diff --check`와 정적 검색으로 트리거·이벤트·브랜치·commit SHA 전달 조건 및 이전 재사용 워크플로 참조 제거 여부를 확인합니다.
- 남은 사용자 결정: `workflow_run` 트리거가 동작하도록 변경된 CD 워크플로를 GitHub 기본 브랜치 `main`에 반영해야 합니다.

## 2026-07-31 - Tailscale OAuth 기반 SSH 배포 연결

- 목적: 공개 SSH 포트를 열지 않고 GitHub Actions 실행기가 Tailscale을 통해 개인 Linux 배포 호스트에 접근하도록 구성했습니다.
- 변경 영역: `.github/workflows/CD.yml`, `.codex/change-summaries/CHANGE_SUMMARY.md`.
- 반영 내용: 배포 파일 전송 전에 Tailscale OAuth client로 `tag:ci` 임시 노드를 생성하고, `SSH_HOST` 연결 확인이 완료된 뒤 SCP와 SSH 배포를 실행하도록 했습니다.
- 검증: 워크플로 diff와 YAML 구조를 정적으로 확인합니다. 실제 연결 검증은 Tailscale OAuth client 및 GitHub Secrets 등록 후 첫 배포에서 수행해야 합니다.
- 남은 사용자 결정: Tailscale OAuth client를 `auth_keys` 쓰기 권한과 `tag:ci`로 생성하고, GitHub에 `TS_OAUTH_CLIENT_ID`, `TS_OAUTH_SECRET`을 등록해야 합니다.

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

## 2026-07-22 - 커밋 이슈 번호 본문 표기 규칙 추가

- 목적: 이슈 번호가 커밋 제목에 섞이지 않도록 하고, 연관 이슈는 커밋 본문의 `Refs: #번호` 형식으로 일관되게 기록하도록 규칙을 명시했습니다.
- 변경 영역: `.codex/skills/commit/SKILL.md`.
- 검증: 커밋 제목과 본문 규칙, 커밋 명령 예시가 스킬에 반영되었는지 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-07-26 - Codex 훅 차단·경고·병렬 실행 안정화

- 목적: 위험 명령과 시크릿을 이벤트별 공식 deny 스키마로 차단하고, 경고를 Codex가 소비하는 JSON으로 전달하며, 병렬 `PostToolUse` 로그 기록 충돌이 훅 실패로 이어지지 않도록 했습니다.
- 변경 영역: `.codex/hooks/ZombieHookCommon.ps1`, `pre_tool_guard.ps1`, `post_tool_audit.ps1`, `subagent_stop_audit.ps1`, `stop_quality_gate.ps1`.
- 반영 내용:
  - `PreToolUse`는 `permissionDecision=deny`, `PermissionRequest`는 `decision.behavior=deny`를 반환하도록 이벤트별 출력을 분리했습니다.
  - 상대경로 재귀 삭제, 경로 checkout, PowerShell 축약 재귀 옵션과 추가 시크릿 필드 탐지를 보강했습니다.
  - `PostToolUse` 경고는 `systemMessage`와 `additionalContext`, `SubagentStop`·`Stop` 경고는 `systemMessage`로 한 번만 출력하도록 정리했습니다.
  - 활동 로그 쓰기에 재시도를 적용하고, 로깅·감사 예외가 훅 종료 코드 1로 전파되지 않도록 격리했습니다.
- 검증: 전체 PowerShell 구문 검사와 훅 자체 테스트를 통과했고, `PermissionRequest`, `PostToolUse`, `SubagentStop`, `Stop` 실제 형태 payload에서 종료 코드 0과 유효한 JSON 출력을 확인했습니다. 병렬 16개 `PostToolUse` 실행은 모두 성공했고, 장기 파일 잠금 상황에서도 종료 코드 0과 `systemMessage`·`additionalContext`가 반환됐습니다.
- 남은 사용자 결정: 없음.

## 2026-07-26 - 배포 PR Migration·TLS·CI/CD 안전장치 보강

- 목적: 기존 `EnsureCreated()` DB의 Migration 충돌로 인한 배포 중단, 평문 HTTP 직접 노출, CI 실패 커밋 배포와 가변 GitHub Action 태그 사용을 방지했습니다.
- 변경 영역: `.github/workflows/CI.yml`, `.github/workflows/CD.yml`, `compose.yaml`, `app.env.example`, `deployment/README.md`, `deployment/migrations`, `deployment/scripts`.
- 반영 내용:
  - 기존 테이블은 있지만 최초 Migration 이력이 없는 DB를 앱 교체 전에 감지해 배포를 중단하도록 했습니다.
  - 기존 DB의 테이블·컬럼·인덱스·외래키를 검증하고 전체 dump를 만든 뒤 최초 Migration baseline을 등록하는 명시적 명령을 추가했습니다.
  - 앱 포트를 호스트 loopback에만 바인딩하고 운영 환경 기본값을 `Production`으로 변경했으며, 외부 공개 시 TLS 역방향 프록시를 필수로 문서화했습니다.
  - CI를 통과한 `main`·`develop` push만 CD 재사용 워크플로를 호출하도록 연결했습니다.
  - CI/CD에서 사용하는 외부 GitHub Actions를 검증된 release commit SHA로 고정했습니다.
- 검증: .NET 10 Release 빌드가 경고·오류 없이 통과했고, Docker Compose 렌더링, 전체 Bash 구문 검사, 외부 Action 전체 40자리 SHA 고정 여부와 CD 직접 push 트리거 제거를 확인했습니다. 임시 MySQL 기반 Migration 차단·불일치 거부·baseline 성공 통합 테스트는 CI에 추가했으며 로컬 Docker 데몬이 실행 중이지 않아 현재 PC에서는 실행하지 못했습니다.
- 남은 사용자 결정: 기존 DB가 있다면 외부 백업 확보 후 baseline 명령을 실제 운영 환경에서 실행해야 합니다. 외부 API 공개 전 TLS 역방향 프록시와 인증서를 준비해야 합니다.

## 2026-07-28 - Stop 훅 세션 활동 요약 제거

- 목적: 정상 응답마다 표시되던 최근 도구 이벤트 수와 `dotnet build` 실행 횟수 안내를 제거해 불필요한 종료 메시지를 없앴습니다.
- 변경 영역: `.codex/hooks/stop_quality_gate.ps1`.
- 반영 내용: `Stop` 훅의 세션 활동 집계와 성공 요약 출력을 제거하고, 실제 정적 분석 경고·품질 차단·빌드 상태 안내만 유지했습니다.
- 검증: PowerShell 구문 검사와 `stop_quality_gate.ps1 -SelfTest`로 정상 동작을 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-07-28 - release 브랜치 CI/CD 통합

- 목적: `develop`의 배포 검증 구조를 유지하면서 `release` 대상 PR과 푸시에서도 CI를 실행하고, 검증을 통과한 `release` 푸시를 개발환경에 배포할 수 있도록 했습니다.
- 변경 영역: `.github/workflows/CI.yml`, `.github/workflows/CD.yml`.
- 반영 내용:
  - `release` 대상 PR과 `release` 푸시에 빌드, 테스트, Docker Compose, 배포 스크립트, Migration baseline 및 컨테이너 빌드 검증이 실행되도록 했습니다.
  - PR에서는 검증만 수행하고, 푸시에서만 재사용 CD 워크플로를 호출하는 기존 안전장치를 유지했습니다.
  - `release` 푸시 이미지는 commit SHA 태그와 `release` 채널 태그로 게시하도록 분리했습니다.
  - 원격 배포는 `SSH_DEPLOY_ENABLED=true`와 `DEVELOP_DEPLOY_ENABLED=true`일 때 개발환경으로만 실행하도록 제한했습니다.
- 검증: 워크플로 충돌 표시가 없고 `git diff --check`가 통과하는지 확인했으며, release 트리거·검증 단계·push 전용 CD 호출·release 이미지 태그·개발환경 배포 게이트를 정적 점검했습니다. .NET 10 Release 빌드는 경고와 오류 없이 통과했습니다.
- 남은 사용자 결정: 없음.

## 2026-07-28 - 커밋 본문 이슈 번호 형식 간소화

- 목적: 커밋 본문에서 `Refs:`와 `Closes:` 같은 접두사를 제거하고 이슈 번호만 기록하도록 규칙을 단순화했습니다.
- 변경 영역: `.codex/skills/commit/SKILL.md`.
- 반영 내용: 관련 이슈가 있으면 제목 다음 빈 줄에 `#번호`만 작성하고, 여러 번호는 한 줄에 하나씩 작성하도록 명시했습니다.
- 검증: 스킬 내 접두사 규칙이 제거되고 커밋 명령 예시와 동일한 `#번호` 형식으로 통일됐는지 확인했습니다.
- 남은 사용자 결정: 없음.

## 2026-07-30 - release 대비 develop 코드리뷰 지적사항 해소

- 목적: PR #24 전체 코드리뷰에서 확인한 서버 권한, 동시성, 인증 남용, 운영 설정, 배포 경쟁, 테스트 및 저장소 위생 문제를 해소했습니다.
- 변경 영역: Inventory/Auth/Gacha/Player/WeaponUpgrade, EF Migration, `Program.cs`, 테스트 프로젝트, CI/CD 및 배포 스크립트, Codex 훅·스킬·문서.
- 반영 내용:
  - 클라이언트가 골드와 무기 소유권을 덮어쓰던 저장 API를 제거하고 서버 소유 변경 경로만 유지했습니다.
  - 플레이어 및 이메일 인증 상태에 동시성 버전을 추가하고 충돌·중복 쓰기를 409로 변환했습니다.
  - 인증 및 재화 변경 요청 제한, 설정 시작 검증, SMTP 암호화와 외부 MySQL `VerifyFull` 정책을 추가했습니다.
  - 개발/운영 배포를 환경별로 직렬화하고 백업 컨테이너를 설정 상태에 맞춰 재생성하거나 제거합니다.
  - xUnit/NSubstitute 기반 테스트 프로젝트를 추가하고 IDE 캐시와 로컬 설정 파일을 Git 추적에서 제거했습니다.
- 검증: .NET 10 Release 빌드, 7개 자동 테스트, EF pending-model 검사, NuGet 취약점 검사, Compose/훅/정적 검사를 수행했습니다.
- 남은 사용자 결정: 없음.

## 2026-07-31 - 내부 MySQL 8.4 인증 호환성 보완

- 목적: TLS가 비활성화된 격리 Compose 네트워크에서 MySQL 8.4 `caching_sha2_password` 인증이 실패하는 문제를 해결했습니다.
- 변경 영역: `Program.cs`, `deployment/README.md`.
- 반영 내용: 호스트가 Compose 내부 서비스명 `mysql`이고 `SslMode=Disabled`인 경우에만 RSA 공개키 조회를 허용하고, 외부 DB 또는 TLS 연결에서는 허용하지 않도록 제한했습니다.
- 검증: .NET 포맷 검사, 경고·오류 없는 빌드, 자동 테스트 7개, Docker 앱 이미지 빌드 및 Migration baseline 통합 테스트가 모두 통과했습니다.
- 남은 사용자 결정: 없음.

## 2026-08-10 - 개발 CD PR 검증 및 운영 이미지 승격 보강

- 목적: 개발 CD가 `develop`·`release` 대상 push와 PR에서 안전하게 일회성 배포를 검증하고, 운영 CD가 CI에서 검사한 컨테이너 이미지를 재빌드 없이 그대로 게시하도록 개선했습니다.
- 변경 영역: `.github/workflows/Development-CD.yml`, `.github/workflows/Main-CI.yml`, `.github/workflows/Main-CD.yml`, `deployment/README.md`.
- 반영 내용:
  - 개발 CD를 권한 있는 `workflow_run`에서 PR 코드를 실행하던 구조 대신 읽기 전용 `push`·`pull_request` 직접 트리거로 변경했습니다.
  - Main CI의 성공한 `main` push 이미지를 Actions 아티팩트로 전달하고 Main CD가 동일 이미지를 SHA·`latest` 태그로 GHCR에 게시하도록 했습니다.
  - 개발 검증과 실제 운영 배포의 역할, 필요한 production Secret과 배포 활성화 조건을 현재 워크플로에 맞춰 문서화했습니다.
- 검증: Docker Compose 렌더링, 외부 Action 40자리 SHA 고정 검사와 `git diff --check`가 통과했고, .NET 10 Release 빌드는 경고·오류 없이 완료됐으며 자동 테스트 7개가 모두 통과했습니다. 로컬 Docker 데몬 권한이 없어 이미지 아티팩트의 실제 save/load는 GitHub Actions에서 최종 확인해야 합니다.
- 남은 사용자 결정: 없음.

## 2026-08-12 - DuckDNS HTTPS 공개 접속 구성

- 목적: 개인 Ubuntu 서버의 게임 API를 DuckDNS 도메인과 HTTPS로 다른 컴퓨터 및 게임 클라이언트에서 안전하게 사용할 수 있도록 구성했습니다.
- 변경 영역: `compose.yaml`, `app.env.example`, `deployment/caddy/Caddyfile`, `deployment/scripts/deploy.sh`, GitHub CI/CD 워크플로, `deployment/README.md`.
- 반영 내용: Caddy 역방향 프록시와 인증서 영구 볼륨, 자동 배포·상태 확인을 추가하고 개발 CD에서 HTTP 프록시까지 일회성 검증하도록 했습니다. `zombie-survival-3d-game.duckdns.org`의 IP 갱신, Ubuntu 방화벽, 공유기 포트포워딩 및 외부 검증 절차도 문서화했습니다.
- 검증: Docker Compose 렌더링, 공식 Caddy 이미지의 Caddyfile 검증, Ubuntu 기준 Bash 구문, 외부 Action SHA 고정 및 `git diff --check`가 통과했습니다. .NET 10 Release 빌드는 경고·오류 없이 완료됐고 자동 테스트 7개가 모두 통과했습니다. 실제 인증서 발급과 외부 HTTPS 접속은 포트포워딩 후 확인해야 합니다.
- 남은 사용자 결정: 공유기 TCP 80/443 포트포워딩과 DuckDNS 토큰 등록은 개인 네트워크에서 직접 설정해야 합니다.

## 2026-08-19 - 공개 배포 CI/CD와 롤백 보안 강화

- 목적: DuckDNS 공개 서버 배포가 최신 `main`의 검증된 이미지에만 적용되고, 외부 HTTPS 검증 실패 시 앱·백업·프록시·배포 파일을 일관되게 복구하도록 운영 경계를 강화했습니다.
- 변경 영역: Main/Development CI·CD, `Program.cs`, Dockerfile과 Compose, Caddy, DuckDNS updater, 배포·Migration 검증 스크립트, 운영 문서.
- 반영 내용:
  - DB readiness `/health`와 공개 liveness `/live`를 분리하고, Caddy는 공개 `/health`를 404로 차단하며 고정된 RFC1918 `/24`~`/29` 프록시 대역만 신뢰하도록 했습니다.
  - Main CD는 오래된 CI 실행을 건너뛰고 SHA 태그를 게시한 뒤 RepoDigest로 배포하며, 실행 중 바이너리를 내려받는 SSH Action 대신 호스트 지문을 검증한 기본 OpenSSH를 사용합니다.
  - `prepare → 외부 HTTPS 검사 → confirm/rollback` 트랜잭션에 전체 `deployment/`, Compose, 앱·Caddy·백업 컨테이너와 frontend 네트워크 복구를 포함하고, 오래된 잠금 자동 회수와 멱등 롤백을 추가했습니다.
  - MySQL·Caddy·.NET·AWS CLI 기반 이미지를 digest로 고정하고, DuckDNS 토큰의 argv 노출 방지·실행 제한 시간·중복 실행 잠금을 적용했습니다.
  - 컨테이너 롤백으로 되돌릴 수 없는 축소형 EF Migration을 CI에서 거부하고 정책·설정·트랜잭션·DuckDNS 테스트를 개발/메인 CI에 연결했습니다.
- 검증: .NET 10 Release 빌드(경고·오류 0), 자동 테스트 7개, actionlint, ShellCheck, Linux Bash 기반 Migration 정책·배포 설정·트랜잭션/백업 롤백·DuckDNS 테스트, Docker Compose 렌더링, 고정 digest Caddy 설정 검증과 `git diff --check`를 통과했습니다.
- 남은 사용자 결정: 실제 인증서 발급과 외부 HTTPS 접속을 위해 공유기 TCP 80/443 포트포워딩 및 Ubuntu DuckDNS 토큰 등록을 완료한 뒤 모바일 네트워크에서 `/live`를 확인해야 합니다.
