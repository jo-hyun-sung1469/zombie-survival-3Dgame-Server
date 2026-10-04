# zombie-survival-3Dgame-Server
3D 게임 서버입니다, 추가로 소켓도 만들 생각입니다

로컬 Docker 실행과 AWS EC2 직접 SSH 배포·운영 방법은 [deployment/README.md](deployment/README.md)를 참고하세요.
로컬 Docker 실행과 Linux SSH 배포 방법은 [deployment/README.md](deployment/README.md)를 참고하세요.

## 게임 종료 보상 요청

게임 시작 시 `POST /api/game-sessions`를 호출하고 응답의 `sessionId`를 보관합니다. 진행 중인 세션이 있으면 같은 ID를 반환합니다. 게임 종료 시 `POST /api/rewards/survival`에 다음 Request DTO를 JSON으로 전송합니다. 두 요청 모두 `Authorization: Bearer {accessToken}`이 필요합니다.

```json
{
  "sessionId": "0123456789abcdef0123456789abcdef",
  "survivalTime": 120.5,
  "clearWave": 3,
  "killZombies": 40
}
```

플레이어 ID는 JWT에서 읽습니다. 보상은 `(int)(survivalTime * 3 + clearWave * 100 + killZombies * 5)`로 계산하며, 계산에 사용하는 생존시간의 상한은 2,000초입니다. 필수 값 누락·음수·비유한 시간·보상 계산 오버플로는 400으로 거부합니다. 골드 합산 상한과 동시성 충돌은 409, 다른 계정의 세션이나 존재하지 않는 세션은 404로 처리합니다.

네트워크 오류나 동시성 충돌로 재시도할 때는 **같은 세션 ID와 같은 DTO**를 보내세요. 골드와 종료·지급 상태는 같은 DB 트랜잭션에 저장됩니다. 이미 지급된 세션은 `alreadyClaimed: true`와 기존 보상액·수령 시각 및 현재 골드를 반환하며 추가 지급하지 않습니다. 이미 확정된 종료 기록이 있으면 그 기록으로 계산하고 요청의 통계로 덮어쓰지 않습니다. 골드 한도 거부 시에는 종료·지급 상태를 새로 확정하지 않습니다. 완료한 게임을 보상받기 위해 새 세션 ID를 발급받으면 안 됩니다.

예제의 `sessionId`는 게임 시작 응답에서 받은 실제 ID로 교체해야 합니다.

## 클라이언트 기록의 검증 한계

현재 보상 API는 클라이언트가 보고한 생존시간·완료 웨이브·처치 수를 받아 계산합니다. 입력 형식과 계산 범위는 검증하지만, 서버가 실제 전투를 관측하지 않으므로 해당 값의 진위를 보장하지 못합니다. 생존시간도 클라이언트 보고값이며 서버 경과시간과 비교하지 않습니다. 2,000초 상한은 보상 계산 제한입니다.

세션별 중복 지급 방지는 같은 세션의 반복·동시 요청에 적용됩니다. 처음 제출한 통계를 부풀리거나 새 세션을 만들어 허위 게임 결과를 반복 제출하는 행위까지 방지하지는 못합니다. JWT와 요청 횟수 제한도 실제 플레이의 증거가 되지는 않습니다.

실제 처치·웨이브·사망을 서버가 판정하거나 플레이 기록을 재현·검증하는 기능은 향후 작업입니다. 실시간 전투 서버는 이번 구현에 포함하지 않았습니다. 내부 서버 연동 방식은 [세션 연동 문서](zombie_servival-3Dgame_Server/GameSession/README.md)를 참고하세요.
