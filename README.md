# zombie-survival-3Dgame-Server
3D 게임 서버입니다, 추가로 소켓도 만들 생각입니다

로컬 Docker 실행과 AWS EC2 직접 SSH 배포·운영 방법은 [deployment/README.md](deployment/README.md)를 참고하세요.
로컬 Docker 실행과 Linux SSH 배포 방법은 [deployment/README.md](deployment/README.md)를 참고하세요.

## 게임 종료 보상 요청

게임 시작 시 `POST /api/game-sessions`를 호출하고 응답의 `sessionId`와 `startedAtUtc`를 보관합니다. 진행 중인 세션이 있으면 같은 ID를 반환합니다. 모든 게임 요청에는 `Authorization: Bearer {accessToken}`이 필요합니다.

**완료한 웨이브마다** `POST /api/game-sessions/{sessionId}/waves`로 다음 누적 값을 보냅니다. 첫 완료 웨이브는 1이며, 다음 보고는 반드시 +1이어야 합니다. 건너뛰기·감소·중복 웨이브는 409입니다. 중복 보고 후에는 `GET /api/game-sessions/{sessionId}`에서 Redis에 반영된 진행 값을 확인하세요.

```json
{
  "clearWave": 1,
  "killZombies": 10,
  "survivalTimeSeconds": 30.0
}
```

게임 종료 시에는 기존 최종 `RewardRequest`를 **그대로** `POST /api/rewards/survival`에 전송합니다. `clearWave`는 마지막으로 보고한 완료 웨이브와 같고, 처치 수는 그 기록보다 작을 수 없습니다. 마지막 미완료 웨이브에서 추가한 처치도 전송할 수 있습니다. 아래 예제의 완료 웨이브 3은 사전에 1→2→3 순서로 보고해야 합니다.

```json
{
  "sessionId": "0123456789abcdef0123456789abcdef",
  "survivalTime": 120.5,
  "clearWave": 3,
  "killZombies": 40
}
```

플레이어 ID는 JWT에서 읽습니다. 생존시간은 서버의 `현재 시각 - startedAtUtc`와 비교하며 기본 오차 허용치는 5초입니다(`RedisSession:TimeToleranceSeconds`, 최대 60초). 클라이언트는 세션 시작 이후의 경과 시간을 보고해야 하며 일시정지·백그라운드 중 시간도 서버에서 계속 흐릅니다. 실제 보상에는 서버 경과 시간을 사용합니다. 보상식은 `(int)(survivalTime * 3 + clearWave * 100 + killZombies * 5)`이며 계산 시간 상한은 2,000초입니다. 형식 오류·시간 불일치·계산 오버플로는 400, Redis 진행 불일치·골드 한도·DB 동시성 충돌은 409, 다른 계정·없는 세션·만료 세션은 404, Redis 연결 장애는 503입니다.

네트워크 오류나 동시성 충돌로 재시도할 때는 **같은 세션 ID와 같은 DTO**를 보내세요. 검증된 종료 결과를 MySQL에 먼저 확정하고, 이후 지급 상태와 골드를 같은 DB 트랜잭션에 저장합니다. 이미 지급된 세션은 `alreadyClaimed: true`와 기존 보상액·수령 시각 및 현재 골드를 반환하며 추가 지급하지 않습니다. 골드 한도로 거부해도 종료 결과는 확정되므로 통계를 바꿀 수 없습니다. Redis가 만료·장애 상태여도 SQL에 확정된 결과의 지급 재시도는 가능합니다. SQL 저장 전 잠정 종료 결과도 Redis에 고정해 재시도 중 시간이 증가하거나 요청값이 바뀌어도 최초 검증 결과를 사용합니다. 완료한 게임의 보상을 받으려고 새 세션 ID를 발급받으면 안 됩니다.

Redis 종료 기록은 SQL 결과 확정 뒤 `종료 시각 + 30초`에 만료되며 새 게임 시작 시에도 제거합니다. 아직 SQL에 저장하지 못한 잠정 결과와 활성 세션에는 3일 안전 TTL을 적용합니다. 종료 SQL 기록은 종료 후 3일, 활성 SQL 기록은 시작 후 3일에 만료됩니다. 10분 간격으로 정리하며 정리 전에도 API에서 만료를 거부합니다. 삭제된 세션 ID나 유실된 Redis 기록을 클라이언트 DTO로 복원하지 않습니다.

예제의 `sessionId`는 게임 시작 응답에서 받은 실제 ID로 교체해야 합니다.

## 클라이언트 기록의 검증 한계

Redis는 서버가 접수한 진행 기록을 보관합니다. 서버 시각과 진행 순서·누적 처치의 감소 여부는 검사하지만, 서버가 실제 전투를 관측하지 않으므로 플레이의 진위를 보장하지 못합니다. 시작한 뒤 기다리거나 정합성을 맞춘 가짜 웨이브·처치 보고를 전송하는 행위도 남아 있습니다. 2,000초 상한은 보상 계산 제한입니다.

세션별 중복 지급 방지는 같은 세션의 반복·동시 요청에 적용됩니다. 처음 제출한 통계를 부풀리거나 새 세션을 만들어 허위 게임 결과를 반복 제출하는 행위까지 방지하지는 못합니다. JWT와 요청 횟수 제한도 실제 플레이의 증거가 되지는 않습니다.

`KillZombies`의 웨이브별 최대값·생성 수·처치 속도 규칙은 Unity 개발 중 확정한 뒤 서버에 추가해야 합니다. 현재는 감소와 보상 계산 범위만 검사합니다. 실제 처치·웨이브·사망 판정 및 전투 재현은 향후 작업입니다. 실시간 전투 서버는 구현하지 않았습니다. [세션 연동 문서](zombie_servival-3Dgame_Server/GameSession/README.md)와 [Redis 운영 안내](deployment/README.md)를 참고하세요.
