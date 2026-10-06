# PC에서 운영 CD 실행

Windows PC의 Ubuntu WSL에 등록한 `zombie-deploy-pc` 실행기를 사용합니다. PC와 WSL 실행기가 켜져 있으면 Main CD의 배포 잡을 PC에서 처리하고, 앱은 AWS EC2에서 계속 실행됩니다. 다른 CI·이미지 게시 잡은 기존 GitHub 실행기를 사용합니다.

## GitHub와 AWS 설정

- Repository Variable `DEPLOY_RUNNER`: `zombie-deploy-pc`
- Repository Variable `SSH_DEPLOY_ENABLED`: `true`
- production Secret `SSH_HOST`: 앱 EC2의 공개 IPv4 또는 공개 DNS
- production Secret `SSH_USER`: `ubuntu`
- 기존 `SSH_KEY`, `SSH_HOST_FINGERPRINT`, GHCR Secret을 유지합니다.
- EC2 보안 그룹의 SSH TCP 22 소스에 PC에서 실제 사용하는 공인 IPv4 `/32`를 허용합니다. VPN·네트워크 변경 시 출발지 IP도 달라질 수 있습니다.

현재 PC의 출발지 IP와 운영 SSH 도달 여부는 PowerShell에서 확인합니다.

```powershell
wsl -d Ubuntu -- curl --fail --silent --show-error https://checkip.amazonaws.com
wsl -d Ubuntu -- ssh-keyscan -T 10 -t ed25519 zombie-survival-3d-game.duckdns.org
```

`ssh-keyscan` 성공은 네트워크 도달 확인이며, 서버 신원 검증은 Main CD가 기존 `SSH_HOST_FINGERPRINT`와 비교해 수행합니다.

## PC 재시작 후 실행기 시작

현재 PC에서는 다음 명령을 PowerShell에서 실행하고 배포가 끝날 때까지 유지합니다.

```powershell
wsl -d Ubuntu -- bash /home/noname/actions-runner-zombie-deploy/start-pc-runner.sh
```

GitHub Settings → Actions → Runners에서 `zombie-deploy-pc`가 Idle인 것을 확인합니다. 이후 실패한 Main CD를 재실행합니다. 실행기가 Offline이면 배포 잡은 실행기를 기다리며, PC 절전·종료 또는 WSL 종료는 진행 중인 배포 연결을 끊을 수 있습니다. Windows 자동 시작은 별도로 설정하지 않았습니다.

이 저장소는 공개 저장소이며 외부 기여자의 PR 워크플로에 매번 승인을 요구하도록 `all_external_contributors` 정책을 설정했습니다. 외부 PR의 실행을 승인하기 전에 워크플로와 실행 코드를 확인해야 합니다. 실행기 라벨은 배포 대상 선택용이며 다른 워크플로 실행을 막는 보안 경계는 아닙니다. 개인 키·운영 환경 파일을 실행기 작업 디렉터리에 복사하지 않습니다.

참고: [GitHub 실행기 등록](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners), [공개 저장소 실행기 보안](https://docs.github.com/en/actions/reference/security/secure-use).
