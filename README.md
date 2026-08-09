# zombie-survival-3Dgame-Server
3D 게임 서버입니다, 추가로 소켓도 만들 생각입니다

로컬 Docker 실행과 Linux SSH 배포 방법은 [deployment/README.md](deployment/README.md)를 참고하세요.

서버를 관리할때의 좋은 팁은 뭔가요?
->
로그는 설계부터 짜기.
핵심 수치는 항상 기록 (동접, 패킷 처리량, DB 쿼리 시간)
배포는 롤백이 가능해야 함