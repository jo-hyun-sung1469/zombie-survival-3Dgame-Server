# Docker Compose 배포 가이드

## 현재 운영 전략

Windows PC의 Docker Desktop에서 앱과 MySQL을 로컬 검증하고, 개인 Ubuntu 서버에는 같은 Compose 구성을 SSH로 배포합니다. AWS 계정은 필요하지 않으며 S3 백업은 기본적으로 비활성화되어 있습니다.

### 로컬 실행

```powershell
.\deployment\scripts\prepare-local-env.ps1
notepad app.env
docker compose --env-file app.env up -d --build mysql app
curl.exe -i http://localhost:5000/health
```

Gmail을 사용할 경우 `app.env`의 `SMTP_PASSWORD`에 새 Gmail 앱 비밀번호를 직접 입력해야 합니다. `app.env`는 Git에서 제외되며 `app.env.example`에는 실제 비밀값을 넣지 않습니다.

## 구성

- 앱: .NET 10, linux/amd64, 비루트 UID 1654, 포트 8080
- DB: `mysql:8.4.10`, 외부 3306 포트 미공개
- 공개 프록시: `caddy:2.11.4-alpine`, TCP 80/443 및 HTTP/3용 UDP 443
- 백업: MySQL 8.4.10 클라이언트와 AWS CLI 2.27.49
- 영구 데이터: `zombie_survival_mysql_data` named volume
- TLS 인증서 및 Caddy 상태: `zombie_survival_caddy_data`, `zombie_survival_caddy_config` named volume
- 임시 백업: `zombie_survival_mysql_backup_tmp` named volume, 생성 후 2일이 지난 dump 삭제

앱은 시작할 때 미적용 EF Migration을 실행하고 총기 카탈로그를 upsert합니다. `/health`는 인증 없이 MySQL 연결을 검사하며 정상일 때 200, 장애일 때 503을 반환합니다.
앱 포트는 호스트의 `127.0.0.1`에만 바인딩되고, 같은 Compose의 Caddy만 Docker backend 네트워크의 `app:8080`으로 접근합니다. Caddy는 `APP_DOMAIN`의 인증서를 자동 발급·갱신하고 HTTP를 HTTPS로 전환합니다. TLS 없이 앱 포트를 외부 인터페이스에 직접 공개하면 안 됩니다.
운영 앱은 요청 제한의 클라이언트 구분을 위해 한 단계의 `X-Forwarded-For`/`X-Forwarded-Proto`를 신뢰합니다. 역방향 프록시는 외부에서 들어온 동일 헤더를 그대로 전달하지 말고 실제 연결 정보로 덮어써야 합니다.

## 사전 준비

1. 배포할 Linux PC에 Docker Engine과 Docker Compose v2를 설치합니다.
2. `~/zombie-survival-server` 디렉터리를 만듭니다.
3. `app.env.example`을 참고해 Linux 서버에 `~/zombie-survival-server/app.env`를 만들고 `chmod 600 app.env`를 적용합니다. `ASPNETCORE_ENVIRONMENT=Production`을 유지합니다.
4. GitHub Actions에서 접근할 수 있도록 Linux 서버의 SSH 접속 주소와 키를 준비합니다.
5. Ubuntu 방화벽과 공유기에서 Caddy용 TCP 80/443을 공개합니다. HTTP/3을 사용할 경우 UDP 443도 공개하고, 앱의 5000 포트와 MySQL 3306은 공개하지 않습니다.

## DuckDNS 공개 접속 설정

운영 API 주소는 `https://zombie-survival-3d-game.duckdns.org`입니다. DuckDNS의 `zombie-survival-3d-game` 도메인이 현재 집의 공인 IPv4를 가리켜야 하며, `app.env`에는 다음 값을 유지합니다.

```dotenv
APP_DOMAIN=zombie-survival-3d-game.duckdns.org
```

게임 클라이언트의 로그인 요청 URL은 다음과 같습니다.

```text
POST https://zombie-survival-3d-game.duckdns.org/api/auth/login
```

Ubuntu 서버에서 공인 IPv4와 DuckDNS 응답을 비교합니다.

```bash
curl -4 https://api.ipify.org
getent ahostsv4 zombie-survival-3d-game.duckdns.org
```

공인 IP가 바뀌는 가정용 회선이라면 DuckDNS 토큰을 Git 저장소나 `app.env`에 넣지 말고 Ubuntu 사용자 홈의 비공개 파일에 저장합니다.

```bash
mkdir -p ~/.config/duckdns
chmod 700 ~/.config/duckdns
printf '%s\n' 'DUCKDNS_TOKEN=DuckDNS에서_발급받은_토큰' > ~/.config/duckdns/credentials
chmod 600 ~/.config/duckdns/credentials
```

다음 cron 항목으로 5분마다 공인 IPv4를 갱신할 수 있습니다. `whoami`로 확인한 실제 사용자 경로로 `/home/사용자명`을 바꿉니다.

```cron
*/5 * * * * . /home/사용자명/.config/duckdns/credentials; curl --fail --silent --show-error "https://www.duckdns.org/update?domains=zombie-survival-3d-game&token=${DUCKDNS_TOKEN}&ip=" >/dev/null
```

Ubuntu 방화벽을 사용한다면 SSH 접속을 먼저 허용한 뒤 HTTPS 포트를 엽니다.

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 443/udp
sudo ufw enable
```

공유기 관리 화면에서는 TCP 80과 TCP 443을 Ubuntu 서버의 고정 LAN IP로 포트포워딩합니다. HTTP/3을 사용할 경우 UDP 443도 같은 서버로 전달합니다. 공유기의 WAN IP가 `curl -4 https://api.ipify.org` 결과와 다르거나 WAN IP가 `100.64.0.0/10`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` 범위라면 CGNAT 또는 이중 공유기일 수 있어 ISP 공인 IPv4나 추가 상위 공유기 설정이 필요합니다.

Caddy가 인증서를 처음 발급하려면 DuckDNS가 올바른 공인 IP를 가리키고 외부 인터넷에서 TCP 80 또는 443이 서버까지 도달해야 합니다. 서버와 같은 LAN에서는 공유기의 NAT loopback 지원 여부 때문에 도메인 접속이 실패할 수 있으므로, 최초 확인은 휴대전화 Wi-Fi를 끈 LTE/5G에서 수행합니다.

S3 백업은 선택 사항입니다. 사용할 경우에만 비공개 S3 버킷, 기본 암호화, 30일 Lifecycle, 쓰기 권한을 준비하고 `BACKUP_ENABLED=true`로 설정합니다. 사용하지 않으면 `BACKUP_ENABLED=false`를 유지합니다.

MySQL 공식 이미지가 최초 초기화 시 `MYSQL_USER` 계정을 만들고 `MYSQL_DATABASE`에 한정해 권한을 부여하므로, 앱 계정과 root 계정은 서로 다른 값을 사용해야 합니다. DB 비밀번호는 `app.env`의 `MYSQL_PASSWORD` 한 곳에만 저장되고 Compose가 앱·MySQL·백업 설정으로 전달합니다.
Compose 내부의 `mysql` 서비스 연결은 격리된 backend 네트워크이므로 `DATABASE_SSL_MODE=Disabled`를 사용합니다. 이 경우 MySQL 8.4의 `caching_sha2_password` 인증을 위해 RSA 공개키 조회를 내부 `mysql` 호스트에만 허용합니다. 외부 운영 DB에 연결할 때는 공개키 조회가 허용되지 않으며, 서버 인증서를 준비하고 `VerifyFull`을 사용해야 합니다. `Preferred`는 허용되지 않습니다.

## GitHub 설정

실제 Linux 서버 배포에 사용하는 `production` Environment에 다음 Secret을 등록합니다.

- `SSH_HOST`
- `SSH_USER`
- `SSH_KEY`
- `GHCR_USERNAME`
- `GHCR_PAT`
- `TS_OAUTH_CLIENT_ID`
- `TS_OAUTH_SECRET`

개발용과 운영용 CI/CD는 다음 역할로 분리됩니다.

- `Development CI`: `develop`·`release` 대상 push와 PR에서 .NET 빌드, 테스트, Compose·배포 스크립트, Migration baseline 및 컨테이너 빌드를 검증합니다.
- `Development CD`: `develop`·`release` 대상 push와 PR에서 별도의 일회성 MySQL·앱 환경을 직접 실행하고 `/health`를 검사한 뒤 모든 컨테이너와 volume을 제거합니다. GHCR 이미지 발행, 운영 Secret 사용, 원격 서버 배포는 하지 않습니다.
- `Main CI`: `main` 대상 PR과 push를 검증합니다. `main` push에서는 검증한 앱·백업 이미지를 변경 불가능한 Actions 아티팩트로 1일간 보관합니다.
- `Main CD`: 성공한 `main` push의 Main CI 아티팩트만 내려받아 재빌드 없이 commit SHA와 `latest` 태그로 GHCR에 게시합니다. Linux 서버가 준비되기 전에는 이미지 발행까지만 수행하고, 저장소 변수 `SSH_DEPLOY_ENABLED=true`를 설정한 경우에만 운영 서버에 배포합니다.

개발 CD는 PR에서도 실행되지만 GitHub Environment나 운영 Secret을 사용하지 않는 읽기 전용 검증 워크플로입니다. 실제 배포는 Main CD만 담당하며 같은 운영 환경의 배포는 동시에 실행되지 않습니다.

Main CD의 원격 배포는 다음 순서로 동작합니다.

1. 새 앱 이미지를 pull합니다.
2. `BACKUP_ENABLED=true`인 경우에만 백업 이미지를 pull하고 S3 쓰기 및 삭제 권한을 검사합니다.
3. 기존 MySQL 컨테이너와 named volume을 그대로 유지합니다.
4. 기존 테이블이 있으면서 최초 EF Migration 이력이 없는 DB인지 검사하고, 해당하면 기존 앱을 유지한 채 배포를 중단합니다.
5. 앱 컨테이너만 SHA 이미지로 교체하고 최대 90초 동안 healthy 상태를 기다립니다.
6. 앱이 정상이면 Caddy를 재생성하고 최대 60초 동안 프록시 상태를 확인합니다.
7. 앱 실패 시 직전 앱 이미지로 복구하고 워크플로를 실패 처리합니다.

백업 컨테이너는 `BACKUP_ENABLED=true`일 때 S3 쓰기 검증 후 지정된 이미지로 재생성됩니다. `false`로 변경하면 기존 백업 컨테이너를 제거해 선언한 상태와 실제 상태를 일치시킵니다.

운영 상태를 확인할 때는 배포 성공 후 생성되는 `deployment.env`도 함께 사용합니다.

```bash
docker compose --env-file app.env --env-file deployment.env ps
curl --fail http://127.0.0.1:5000/health
curl --fail https://zombie-survival-3d-game.duckdns.org/health
```

CD와 운영 절차에서 `docker compose down -v`를 실행하지 마세요. 이 명령은 영구 MySQL 볼륨까지 삭제합니다.

## Migration 주의사항

최초 Migration은 빈 MySQL 8.4 DB를 기준으로 생성되었습니다. 이전 버전의 `EnsureCreated()`로 이미 만들어진 DB에는 `__EFMigrationsHistory`가 없으므로 그대로 배포하면 최초 Migration의 테이블 생성과 충돌합니다.

기존 운영 DB가 있다면 배포 전에 전체 dump를 만들고, 실제 스키마가 최초 Migration과 동일한지 검토한 다음 별도의 baseline 절차를 승인해야 합니다. 검증 없이 migration 이력만 수동 삽입하지 마세요. 이후 migration은 컬럼·테이블 추가를 먼저 배포하고 구버전 앱이 더 이상 필요하지 않을 때 제거하는 확장 우선 방식으로 작성합니다.

배포 스크립트는 기존 테이블이 있는데 `20260720014315_InitialCreate` 이력이 없으면 새 앱을 교체하기 전에 실패합니다. 다음 명령으로 같은 검사를 직접 실행할 수 있습니다.

```bash
cd ~/zombie-survival-server
bash deployment/scripts/check-migration-readiness.sh
```

기존 DB를 보존하면서 baseline을 등록해야 할 때는 먼저 별도 외부 백업을 확보한 다음 아래 명령을 실행합니다.

```bash
cd ~/zombie-survival-server
bash deployment/scripts/baseline-existing-database.sh --confirm-initial-baseline
```

이 명령은 현재 스키마의 테이블, 컬럼 형식과 null 허용 여부, 인덱스, 외래키가 최초 Migration과 일치하는지 검사합니다. 하나라도 다르면 DB를 변경하지 않고 종료합니다. 검증을 통과하면 `deployment/backups/`에 전체 dump를 생성한 후에만 `__EFMigrationsHistory`에 최초 Migration을 등록합니다. 생성된 dump가 안전하게 보관되었는지 확인한 다음 배포를 다시 실행하세요.

## 백업 및 복원 검증

이 절은 `BACKUP_ENABLED=true`로 S3 백업을 사용하는 경우에만 적용됩니다.

백업 서비스는 시작 즉시 한 번, 이후 기본 86,400초마다 다음 작업을 수행합니다.

1. `mysqldump --single-transaction`으로 일관된 dump를 만듭니다.
2. gzip 압축 파일을 `MYSQL_BACKUP_S3_URI/{database}/`에 업로드합니다.
3. 로컬에서 2일이 지난 `.sql.gz` 파일을 삭제합니다.

첫 운영 배포 후에는 최근 S3 객체 하나를 임시 MySQL 8.4.10 컨테이너에 복원해 다음을 비교합니다.

- `Users`, `PlayerSaveData`, `PlayerWeaponStates`, `PlayerStatUpgradeStates`의 행 수
- 대표 사용자 ID, 골드, 무기 소유·강화 상태
- `__EFMigrationsHistory`와 `FirearmDefinitions`

복원 검증은 운영 MySQL 볼륨이나 `game-mysql` 컨테이너를 사용하지 말고 별도 임시 volume과 컨테이너에서 수행해야 합니다. 검증이 끝나면 임시 리소스만 제거합니다.
