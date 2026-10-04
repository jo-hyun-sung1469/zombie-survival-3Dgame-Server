# 생존 게임 세션과 보상 연동

플레이어는 `POST /api/game-sessions`로 시작 세션 ID를 받고 `GET /api/game-sessions/{sessionId}`로 자신의 세션을 조회합니다. 종료 시 `POST /api/rewards/survival`의 `RewardRequest`에 `sessionId`, `survivalTime`, `clearWave`, `killZombies`를 담아 전송합니다. 계정은 JWT의 `userId`로 식별합니다. 서버는 최초 수령 요청에서 보고된 기록과 종료·지급 상태 및 골드를 함께 저장합니다.

신뢰할 수 있는 서버 게임 로직은 `IServerGameSessionRecorder`를 생성자 주입받아 실제 게임 시작 시 `StartAsync(playerId, cancellationToken)`을 호출하고, 게임 종료 시 서버가 판정한 웨이브·처치 수로 `CompleteAsync(playerId, sessionId, clearWave, killZombies, cancellationToken)`을 호출합니다. 생존시간은 서버의 시작·종료 시각 차이입니다. 플레이어당 진행 중인 세션은 하나이며, 시작 재시도는 기존 진행 세션을 반환합니다. 종료된 세션의 결과는 수정할 수 없습니다.

내부 인터페이스는 향후 서버 게임 로직 연동용으로 유지합니다. 클라이언트의 종료 보고는 보상 API에서 별도로 처리하며, 이미 내부 로직이 확정한 종료 기록이 있으면 저장된 기록으로 계산하고 요청값으로 덮어쓰지 않습니다. 현재 전투 서버는 구현되어 있지 않으므로 플레이어 API에서 제출한 통계의 진위를 보장하지 못합니다. 서버 시각은 시작·접수 시점을 기록하고, 클라이언트 경로의 생존시간은 제출값을 저장합니다. 검증 한계와 사용 예제는 루트 README에 명시합니다.

보상은 계산기에서 산출합니다. 종료 기록·지급 상태·플레이어 골드는 한 번의 `SaveChangesAsync`로 MySQL 트랜잭션 안에서 함께 저장됩니다. 세션과 플레이어 저장 데이터의 동시성 버전으로 중복 및 다른 재화 변경과의 경쟁을 감지합니다. 충돌 응답은 409이며 같은 세션 ID와 DTO로 재시도할 수 있습니다. 이미 지급된 세션은 당시의 보상액·수령 시각과 현재 골드를 반환하고 다시 지급하지 않습니다. 골드 상한으로 거부되면 세션은 미수령 상태로 남고 요청 통계를 새로 확정하지 않습니다. 누락되거나 잘못된 요청은 400입니다.

스키마 변경은 `AddSurvivalGameSessions` 마이그레이션에 포함됩니다. 운영 DB에 별도로 적용하지 않았으며 기존 시작 시 마이그레이션 흐름을 사용합니다.
