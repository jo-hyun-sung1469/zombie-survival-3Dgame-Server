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

- 앱: digest로 고정한 .NET 10 SDK/runtime, linux/amd64, 비루트 UID 1654, 포트 8080
- DB: digest로 고정한 `mysql:8.4.10`, 외부 3306 포트 미공개
- 공개 프록시: digest로 고정한 `caddy:2.11.4-alpine`, TCP 80/443 및 HTTP/3용 UDP 443
- 백업: digest로 고정한 MySQL 8.4.10 클라이언트와 AWS CLI 2.27.49
- 영구 데이터: `zombie_survival_mysql_data` named volume
- TLS 인증서 및 Caddy 상태: `zombie_survival_caddy_data`, `zombie_survival_caddy_config` named volume
- 임시 백업: `zombie_survival_mysql_backup_tmp` named volume, 생성 후 2일이 지난 dump 삭제

앱은 시작할 때 미적용 EF Migration을 실행하고 총기 카탈로그를 upsert합니다. 내부 readiness인 `/health`는 MySQL 연결을 검사하며 정상일 때 200, 장애일 때 503을 반환합니다. 공개 liveness인 `/live`는 DB를 조회하지 않고 앱 프로세스와 HTTPS 프록시 연결만 확인합니다. Caddy는 외부 `/health` 요청을 404로 차단하고 컨테이너 내부 loopback 리스너에서 `/live`를 앱까지 프록시한 결과로 자신의 상태를 판단합니다.

앱 포트는 호스트의 `127.0.0.1`에만 바인딩됩니다. Caddy와 앱은 `frontend` 네트워크로 통신하고, 앱과 MySQL·백업은 별도의 `backend` 네트워크로 통신합니다. Caddy는 `${APP_SCHEME}://${APP_DOMAIN}`의 인증서를 자동 발급·갱신하고 HTTP를 HTTPS로 전환합니다. TLS 없이 앱 포트를 외부 인터페이스에 직접 공개하면 안 됩니다.

`frontend`의 기본 대역은 `172.29.0.0/24`이며 Compose가 같은 값을 앱의 `ReverseProxy:KnownNetworkCidr`로 전달합니다. 운영 앱은 이 사설 대역에서 한 단계 앞의 프록시가 보낸 `X-Forwarded-For`와 `X-Forwarded-Proto`만 신뢰합니다. 다른 대역을 사용해야 한다면 겹치지 않고 네트워크 주소로 정렬된 RFC1918 IPv4 `/24`~`/29`를 `FRONTEND_SUBNET`에 사용합니다. `/30`~`/32`는 gateway·앱·Caddy 주소가 부족하며, `0.0.0.0/0`처럼 넓거나 공개된 대역도 허용되지 않습니다.

## 사전 준비

1. 배포할 Linux PC에 Docker Engine, Docker Compose v2와 `flock`을 제공하는 `util-linux`를 설치합니다.
2. 로그인 등 다른 용도로 쓰지 않는 전용 비-root 배포 계정을 만들고, 그 계정으로 `install -d -m 700 ~/zombie-survival-server`를 실행합니다. 배포 스크립트는 이 디렉터리가 현재 계정 소유가 아니거나 group/other 쓰기 권한이 있으면 중단합니다.
3. `app.env.example`을 참고해 Linux 서버에 `~/zombie-survival-server/app.env`를 만들고 `chmod 600 app.env`를 적용합니다. `ASPNETCORE_ENVIRONMENT=Production`, `APP_SCHEME=https`, bare hostname 형식의 `APP_DOMAIN`을 유지합니다.
4. GitHub Actions에서 접근할 수 있도록 Linux 서버의 Tailscale SSH 주소와 키를 준비합니다. `SSH_HOST`에는 공개 DuckDNS 주소가 아니라 Tailscale IP 또는 허용된 MagicDNS 이름을 사용합니다.
5. Ubuntu 방화벽과 공유기에서 Caddy용 TCP 80/443을 공개합니다. HTTP/3을 사용할 경우 UDP 443도 공개하고, 앱의 5000 포트와 MySQL 3306은 공개하지 않습니다.

## DuckDNS 공개 접속 설정

운영 API 주소는 `https://zombie-survival-3d-game.duckdns.org`입니다. DuckDNS의 `zombie-survival-3d-game` 도메인이 현재 집의 공인 IPv4를 가리켜야 하며, `app.env`에는 다음 값을 유지합니다.

```dotenv
APP_SCHEME=https
APP_DOMAIN=zombie-survival-3d-game.duckdns.org
FRONTEND_SUBNET=172.29.0.0/24
```

`APP_DOMAIN`에는 `https://`나 경로를 붙이지 않습니다. 운영 배포는 `APP_SCHEME=https`와 bare FQDN이 아니면 시작 전에 중단됩니다. `APP_SCHEME=http`, `APP_DOMAIN=localhost` 조합은 일회성 개발 CD에서만 사용합니다.

게임 클라이언트의 로그인 요청 URL은 다음과 같습니다.

```text
POST https://zombie-survival-3d-game.duckdns.org/api/auth/login
```

Ubuntu 서버에서 공인 IPv4와 DuckDNS 응답을 비교합니다.

```bash
curl -4 https://api.ipify.org
getent ahostsv4 zombie-survival-3d-game.duckdns.org
```

공인 IP가 바뀌는 가정용 회선이라면 DuckDNS 토큰을 Git 저장소나 `app.env`에 넣지 말고 Ubuntu 사용자 홈의 비공개 파일에 저장합니다. 다음 입력은 토큰을 화면에 표시하지 않고 셸 기록에도 명령 인자로 남기지 않습니다.

```bash
mkdir -p ~/.config/duckdns
chmod 700 ~/.config/duckdns
read -rsp 'DuckDNS token: ' DUCKDNS_TOKEN; printf '\n'
umask 077
printf 'DUCKDNS_TOKEN=%s\n' "$DUCKDNS_TOKEN" > ~/.config/duckdns/credentials
unset DUCKDNS_TOKEN
chmod 600 ~/.config/duckdns/credentials
```

`deployment/duckdns/update.sh`는 현재 사용자 소유의 일반 자격 증명 파일과 안전한 잠금 디렉터리만 허용합니다. 자격 증명을 비공개 파일에서 읽고 curl 설정을 표준 입력으로 전달하므로 토큰을 curl의 process argv에 노출하지 않습니다. 사용자 curl 설정은 비활성화하고 HTTPS만 허용하며, 연결은 10초, 전체 요청은 30초로 제한합니다. `flock`으로 이전 갱신이 끝나지 않은 경우 중복 실행도 건너뜁니다. 먼저 직접 한 번 검증합니다.

```bash
cd ~/zombie-survival-server
DUCKDNS_DOMAIN=zombie-survival-3d-game bash deployment/duckdns/update.sh
```

성공을 확인한 뒤 `crontab -e`에 다음 항목을 추가하면 5분마다 갱신합니다. `whoami`로 확인한 실제 사용자 경로로 `/home/사용자명`을 바꿉니다. cron에는 토큰이나 DuckDNS 요청 URL을 직접 적지 않습니다.

```cron
*/5 * * * * DUCKDNS_DOMAIN=zombie-survival-3d-game bash /home/사용자명/zombie-survival-server/deployment/duckdns/update.sh >/dev/null
```

Ubuntu 방화벽에서는 SSH를 Tailscale 인터페이스로만 허용한 뒤 공개 HTTPS 포트를 엽니다. 현재 SSH 세션을 유지한 상태에서 다음 규칙을 추가하고, 별도의 새 터미널에서 Tailscale 주소로 SSH 재접속이 되는지 먼저 확인하세요.

```bash
sudo ufw allow in on tailscale0 to any port 22 proto tcp comment 'SSH via Tailscale'
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 443/udp
sudo ufw status numbered
sudo ufw enable
```

새 Tailscale SSH 접속이 확인된 후에만 기존의 전체 인터페이스 `OpenSSH` 허용 규칙을 `sudo ufw delete allow OpenSSH`로 제거합니다. 원격 작업 중 `ufw reset`을 실행하거나 새 접속 확인 전에 기존 SSH 규칙을 지우면 서버에서 잠길 수 있습니다. 공유기에는 22번 포트를 포워딩하지 않습니다.

공유기 관리 화면에서는 TCP 80과 TCP 443을 Ubuntu 서버의 고정 LAN IP로 포트포워딩합니다. HTTP/3을 사용할 경우 UDP 443도 같은 서버로 전달합니다. 공유기의 WAN IP가 `curl -4 https://api.ipify.org` 결과와 다르거나 WAN IP가 `100.64.0.0/10`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16` 범위라면 CGNAT 또는 이중 공유기일 수 있어 ISP 공인 IPv4나 추가 상위 공유기 설정이 필요합니다.

Caddy가 인증서를 처음 발급하려면 DuckDNS가 올바른 공인 IP를 가리키고 외부 인터넷에서 TCP 80 또는 443이 서버까지 도달해야 합니다. 배포 전에 `sudo ss -lntp`로 80/443을 기존 Apache나 다른 관리 서비스가 점유하지 않는지 확인하고, 충돌한 서비스의 용도를 확인한 뒤 포트를 변경하거나 내부 전용으로 전환합니다. 서비스를 확인하지 않고 중지하지 마세요. 공유기 TCP 80/443의 목적지는 모두 Ubuntu Caddy 호스트여야 합니다.

서버와 같은 LAN에서는 공유기의 NAT loopback 지원 여부 때문에 도메인 접속이 실패할 수 있으므로, 최초 확인은 휴대전화 Wi-Fi를 끈 LTE/5G에서 `https://zombie-survival-3d-game.duckdns.org/live`로 수행합니다. 응답 헤더의 `Server`가 Apache이거나 443 연결이 실패하면 아직 포트포워딩이 Caddy에 도달하지 않은 상태입니다.

S3 백업은 선택 사항입니다. 사용할 경우에만 비공개 S3 버킷, 기본 암호화, 30일 Lifecycle, 해당 버킷에만 쓰기·삭제할 수 있는 전용 AWS 자격 증명을 준비하고 `BACKUP_ENABLED=true`로 설정합니다. 개인 Ubuntu 서버에서는 자격 증명 파일을 `chmod 600`으로 보호하고 `AWS_CREDENTIALS_FILE=/home/배포사용자/.aws/credentials`, `AWS_PROFILE=default`처럼 절대 경로와 프로필만 `app.env`에 기록합니다. Compose는 파일을 백업 컨테이너에 읽기 전용으로 마운트하며 실제 키 값은 `app.env`에 복사하지 않습니다. 인스턴스 역할을 사용하는 환경에서는 `AWS_CREDENTIALS_FILE=/dev/null`을 유지할 수 있습니다. 사용하지 않으면 `BACKUP_ENABLED=false`와 `/dev/null`을 유지합니다.

MySQL 공식 이미지가 최초 초기화 시 `MYSQL_USER` 계정을 만들고 `MYSQL_DATABASE`에 한정해 권한을 부여하므로, 앱 계정과 root 계정은 서로 다른 값을 사용해야 합니다. DB 비밀번호는 `app.env`의 `MYSQL_PASSWORD` 한 곳에만 저장되고 Compose가 앱·MySQL·백업 설정으로 전달합니다.
Compose 내부의 `mysql` 서비스 연결은 격리된 backend 네트워크이므로 `DATABASE_SSL_MODE=Disabled`를 사용합니다. 이 경우 MySQL 8.4의 `caching_sha2_password` 인증을 위해 RSA 공개키 조회를 내부 `mysql` 호스트에만 허용합니다. 외부 운영 DB에 연결할 때는 공개키 조회가 허용되지 않으며, 서버 인증서를 준비하고 `VerifyFull`을 사용해야 합니다. `Preferred`는 허용되지 않습니다.

## GitHub 설정

실제 Linux 서버 배포에 사용하는 `production` Environment에 다음 Secret을 등록합니다.

- `SSH_HOST`
- `SSH_USER`
- `SSH_KEY`
- `SSH_HOST_FINGERPRINT`
- `GHCR_USERNAME`
- `GHCR_PAT`
- `TS_OAUTH_CLIENT_ID`
- `TS_OAUTH_SECRET`

`SSH_HOST_FINGERPRINT`는 신뢰할 수 있는 Ubuntu 콘솔 또는 기존 Tailscale SSH 세션에서 다음 명령으로 확인한 ED25519 호스트 키의 `SHA256:...` 값을 등록합니다. SSH 대상 호스트가 바뀌지 않았는데 fingerprint가 달라지면 배포를 진행하지 말고 원인을 확인합니다.

```bash
sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
```

Main CD는 GitHub 실행기에 기본 설치된 OpenSSH만 사용합니다. Tailscale 연결 후 `ssh-keyscan`으로 받은 ED25519 키의 SHA256 지문을 위 Secret과 정확히 비교하고, 일치할 때만 `StrictHostKeyChecking=yes`로 접속합니다. 실행 중 내려받는 Drone SSH/SCP 바이너리는 사용하지 않으며, 임시 개인 키와 `known_hosts`는 잡 종료 시 삭제합니다.

배포 키는 이 서버의 전용 배포 계정에만 등록하고 다른 SSH 용도로 재사용하지 않습니다. Tailscale ACL은 `tag:ci`가 해당 호스트의 TCP 22에만 접근하도록 제한하고, GitHub `production` Environment에는 required reviewer를 설정합니다. `main`은 PR 리뷰와 Main CI 성공을 요구하는 branch protection을 적용하세요. Docker 그룹과 rootful Docker socket 접근은 사실상 호스트 root 권한이므로, 장기적으로는 rootless Docker 또는 제한된 배포 에이전트로 전환하는 것이 좋습니다.

`GHCR_PAT`는 운영 서버가 비공개 GHCR 이미지를 pull할 때만 사용합니다. 전용 토큰에 `read:packages`만 부여하고 `write:packages`, `delete:packages` 및 불필요한 저장소 권한은 부여하지 않습니다. 이 값은 원격 `prepare` 단계에만 전달되며, 이미지 게시에는 워크플로의 짧은 수명 `GITHUB_TOKEN`이 사용됩니다.

같은 `production` Environment의 Variable `APP_DOMAIN`에는 스킴 없는 `zombie-survival-3d-game.duckdns.org`를 등록합니다. 생략하면 워크플로의 같은 기본 도메인이 사용되지만, 도메인을 바꿀 때는 서버 `app.env`와 Environment Variable을 함께 변경해야 외부 검증 대상이 일치합니다. Repository 또는 Organization Variable `SSH_DEPLOY_ENABLED=true`는 실제 원격 배포를 활성화할 때만 설정합니다. 이 변수는 `production` Environment에만 두면 잡 실행 전 조건에서 읽히지 않습니다.

개발용과 운영용 CI/CD는 다음 역할로 분리됩니다.

- `Development CI`: `develop`·`release` 대상 push와 PR에서 .NET 빌드, 테스트, Compose·배포 스크립트, expand-first Migration 규칙, Migration baseline 및 컨테이너 빌드를 검증합니다.
- `Development CD`: `develop`·`release` 대상 push와 PR에서 별도의 일회성 MySQL·앱·Caddy 환경을 직접 실행하고 내부 `/health`와 프록시 `/live`를 검사한 뒤 모든 컨테이너와 volume을 제거합니다. GHCR 이미지 발행, 운영 Secret 사용, 원격 서버 배포는 하지 않습니다.
- `Main CI`: `main` 대상 PR과 push를 검증합니다. MySQL 장애 중 `/health=503`과 `/live=200`, 복구 후 `/health=200`도 확인합니다. `main` push에서는 검증한 앱·백업 이미지를 변경 불가능한 Actions 아티팩트로 1일간 보관합니다.
- `Main CD`: 성공한 최신 `main` push의 Main CI 아티팩트만 내려받아 재빌드 없이 commit SHA 태그로 GHCR에 게시하고, 게시 결과의 변경 불가능한 image digest를 배포합니다. 더 오래된 CI가 늦게 끝난 실행은 건너뜁니다. Linux 서버가 준비되기 전에는 이미지 발행까지만 수행하고, 저장소 변수 `SSH_DEPLOY_ENABLED=true`를 설정한 경우에만 운영 서버에 배포합니다.

개발 CD는 PR에서도 실행되지만 GitHub Environment나 운영 Secret을 사용하지 않는 읽기 전용 검증 워크플로입니다. 실제 배포는 Main CD만 담당하며 같은 운영 환경의 배포는 동시에 실행되지 않습니다.

Main CD의 원격 배포는 다음 순서로 동작합니다.

1. 새 앱 이미지를 pull합니다.
2. `BACKUP_ENABLED=true`인 경우에만 백업 이미지를 pull하고 S3 쓰기 및 삭제 권한을 검사합니다.
3. 기존 MySQL 컨테이너와 named volume을 그대로 유지합니다.
4. 기존 테이블이 있으면서 최초 EF Migration 이력이 없는 DB인지 검사하고, 해당하면 기존 앱을 유지한 채 배포를 중단합니다.
5. 앱 컨테이너를 CI가 게시한 image digest로 교체하고 최대 90초 동안 내부 `/health`가 healthy인지 기다립니다.
6. 앱이 정상이면 Caddy를 재생성하고 최대 60초 동안 loopback 프록시 `/live` 상태를 확인합니다.
7. 앱·Caddy·백업 상태 확인이 실패하면 직전 앱과 백업 컨테이너, Caddy, Compose 및 버전 관리되는 `deployment/` 파일을 복구한 뒤 워크플로를 실패 처리합니다. `deployment/backups/`의 DB dump는 성공·롤백 모두에서 그대로 보존합니다.
8. 원격 배포가 끝나면 GitHub Actions 실행기가 실제 `https://${APP_DOMAIN}/live`에 접속해 DNS, 공개 443 포트, 인증서와 프록시 경로를 함께 검증합니다. 이 검증이 실패해도 같은 배포에서 직전 이미지로 복구합니다.

백업 컨테이너는 `BACKUP_ENABLED=true`일 때 S3 쓰기 검증 후 지정된 이미지로 재생성되고 실제 `running` 상태까지 확인합니다. `false`로 변경하면 기존 백업 컨테이너를 제거해 선언한 상태와 실제 상태를 일치시킵니다. 후보 배포가 확정되지 않으면 이전 백업 컨테이너의 존재 여부와 이미지도 함께 복구합니다.

컨테이너 롤백은 이미 적용된 DB Migration을 되돌리지 않습니다. 따라서 자동 CI/CD는 Migration의 `Up()`에서 `Drop*`, `Rename*`, `AlterColumn`, 임의 SQL, 데이터 삭제·수정 및 검사할 수 없는 helper 호출을 거부합니다. 컬럼·테이블 추가 같은 확장형 변경만 자동 배포에 포함하세요. 축소 작업이 꼭 필요하면 외부 백업, 구버전 앱 종료, 유지보수 시간과 명시적 승인을 갖춘 별도 수동 DB 작업으로 수행해야 하며 이 자동 배포 파이프라인에는 포함하지 않습니다.

배포 스크립트는 트랜잭션 방식으로 실행됩니다. Main CD는 실행마다 고유한 staging 디렉터리에 후보 파일만 업로드하고, 후보 `deploy.sh`가 잠금을 획득한 뒤 staging 검증·설정 스냅샷·active 파일 승격을 순서대로 수행합니다. `prepare`로 후보 앱을 기동하면 권한 0600의 `.deployment-transaction.env`가 남고, 외부 HTTPS 검사 성공 시 `confirm`, 실패 시 `rollback`을 실행합니다. 프로세스 중복 실행은 PID·부팅 ID·프로세스 시작 시각을 기록한 `.deployment.lock`으로 차단하며, 실제 프로세스가 사라진 오래된 잠금은 다음 실행에서 안전하게 회수합니다. Compose와 `deployment.env`, `deployment/backups/`를 제외한 기존 배포 파일을 스냅샷으로 보관합니다. 워크플로가 중간에 강제 종료되어 보류 상태가 남았다면 서버에서 실행 중인 배포 프로세스가 없는지 먼저 확인한 뒤 다음 중 하나를 명시적으로 선택합니다.

```bash
cd ~/zombie-survival-server
bash deployment/scripts/deploy.sh validate

# 외부 /live와 공개 /health=404를 직접 확인한 경우에만 실행
bash deployment/scripts/deploy.sh confirm

# 후보 배포를 확정할 수 없거나 외부 검사가 실패한 경우 실행
bash deployment/scripts/deploy.sh rollback
```

`.deployment-transaction.env` 또는 `.deployment.lock`을 먼저 삭제해 검사를 우회하지 마세요. 현재 배포 프로세스가 살아 있으면 잠금이 유지되고, 강제 종료나 재부팅으로 남은 잠금은 스크립트가 판별합니다. 소유 정보가 없는 구형 잠금은 경쟁 상태 방지를 위해 생성 후 5분이 지나야 자동 회수됩니다. `prepare`는 이미지 주소와 GHCR 자격 증명이 필요한 Main CD용 단계이며, 일반 수동 상태 확인 명령으로 사용하지 않습니다.

운영 상태를 확인할 때는 배포 성공 후 생성되는 `deployment.env`도 함께 사용합니다.

```bash
docker compose --env-file app.env --env-file deployment.env ps
curl --fail http://127.0.0.1:5000/health
curl --fail https://zombie-survival-3d-game.duckdns.org/live
curl --output /dev/null --write-out '%{http_code}\n' https://zombie-survival-3d-game.duckdns.org/health
```

마지막 명령은 `404`가 정상입니다. Caddy는 `/health`, 후행 슬래시, 대소문자 변형과 인코딩된 슬래시를 모두 차단합니다. 공개 readiness 경로가 200을 반환하면 Caddy 우회 또는 잘못된 프록시 구성이므로 배포를 정상으로 판단하지 않습니다. Caddy 접근 로그는 JSON으로 표준 출력에 기록되고 Docker 로그는 파일당 10MB, 최대 5개로 순환됩니다.

이 변경을 기존 서버에 처음 배포할 때 `zombie_survival_frontend`가 이전 동적 대역으로 만들어져 있으면 배포 스크립트가 연결된 컨테이너를 검사한 뒤 앱과 Caddy만 잠시 중지하고 해당 네트워크를 `FRONTEND_SUBNET`으로 재생성합니다. 알 수 없는 컨테이너가 연결돼 있으면 자동 변경하지 않고 배포를 중단합니다. MySQL과 `zombie_survival_backend`는 유지되며 `docker compose down -v`는 사용하지 않습니다.

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
