# 생존 게임 세션과 보상 연동

플레이어는 `POST /api/game-sessions`로 시작 세션 ID를 받고 `GET /api/game-sessions/{sessionId}`로 자신의 세션을 조회합니다. 활성 세션 조회는 Redis 진행 값, 종료 세션 조회는 MySQL 확정 값을 반환합니다. 웨이브 완료마다 `POST /api/game-sessions/{sessionId}/waves`로 `clearWave`, `killZombies`, `survivalTimeSeconds`를 보고합니다. 종료 시 기존 `RewardRequest`의 `sessionId`, `survivalTime`, `clearWave`, `killZombies`를 그대로 보냅니다. 계정은 JWT의 `userId`로 식별합니다.

신뢰할 수 있는 서버 게임 로직은 `IServerGameSessionRecorder`를 생성자 주입받아 실제 게임 시작 시 `StartAsync(playerId, cancellationToken)`을 호출하고, 게임 종료 시 서버가 판정한 웨이브·처치 수로 `CompleteAsync(playerId, sessionId, clearWave, killZombies, cancellationToken)`을 호출합니다. 생존시간은 서버의 시작·종료 시각 차이입니다. 플레이어당 진행 중인 세션은 하나이며, 시작 재시도는 기존 진행 세션을 반환합니다. 종료된 세션의 결과는 수정할 수 없습니다.

내부 인터페이스는 향후 신뢰할 수 있는 서버 게임 로직 연동용으로 유지합니다. 이미 내부 로직이 확정한 종료 결과는 클라이언트 제출값으로 덮어쓰지 않습니다. 클라이언트 경로는 `SessionProgressValidator -> IGameSessionProgressStore`를 거칩니다. 생존시간을 서버 경과 시간과 비교한 뒤 서버 시간으로 저장하며, Redis Lua에서 웨이브 +1·누적 처치 비감소·종료 후 변경 금지를 원자적으로 검사합니다. 최종 완료 웨이브는 마지막 체크포인트와 같아야 합니다. 진행 값 유실 시 제출 DTO로 새 Redis 기록을 만들거나 보상을 지급하지 않습니다.

검증을 통과한 종료 결과를 MySQL에 먼저 확정하고 Redis의 만료 시각을 `CompletedAtUtc + 30초`로 지정합니다. 재시도로 TTL을 연장하지 않습니다. SQL 저장 전의 잠정 종료 결과는 Redis에서 3일 동안 고정합니다. 골드와 지급 상태는 이후 하나의 `SaveChangesAsync`로 같은 MySQL 트랜잭션에 저장합니다. 세션과 저장 데이터의 동시성 버전으로 중복 및 다른 재화 변경과의 경쟁을 감지합니다. 충돌은 409이며 같은 세션 ID와 DTO로 재시도할 수 있습니다. 이미 지급된 세션은 당시의 보상액·수령 시각과 현재 골드를 반환합니다. 골드 한도로 거부해도 종료 기록은 확정되어 Redis 없이 같은 결과로 재시도할 수 있습니다.

활성 세션은 시작 후 3일, 종료 세션은 종료 후 3일에 만료됩니다. 정리 작업은 10분 간격이며 API도 만료를 검사합니다. 시작 재시도는 진행 중인 세션을 반환하며 유실된 진행을 초기화하지 않습니다. Redis 연결 장애는 503, 연결은 가능하지만 진행 기록이 없으면 다음 웨이브·보상 보고는 409입니다. 유실된 활성 세션은 자동 복원하지 않으며 3일 만료 후 새 세션을 시작할 수 있습니다. 활성 조회 시 캐시가 없으면 SQL의 초기 값이 표시될 수 있으나 지급 검증을 통과하지는 않습니다.

현재 서버는 실제 전투를 관측하지 않습니다. 처치 수의 실제 상한·웨이브별 생성 수·처치 속도는 Unity 개발에서 규칙을 확정한 뒤 검증을 보강해야 합니다. 서버 시간 확인과 Redis 기록만으로 조작된 플레이를 완전히 판별할 수 없습니다.

기존 `AddSurvivalGameSessions`와 정리용 `AddSessionRetentionIndexes` 마이그레이션을 사용합니다. 운영 DB에 직접 적용하지 않았으며 기존 시작 시 마이그레이션 흐름을 유지합니다.
