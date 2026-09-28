# CI: EditMode 테스트

`.github/workflows/editmode-tests.yml` 이 [GameCI](https://game.ci/) `unity-test-runner` 로 `Assets/_Project/Tests/EditMode/` 의 EditMode 테스트를 돌립니다.

- **언제**: `develop`, `main` 에 push 할 때 (기능 브랜치는 develop으로 PR을 열면 실행), 모든 PR, Actions 탭에서 수동 실행(`workflow_dispatch`).
- **Unity 버전**: `ProjectSettings/ProjectVersion.txt` 에서 자동으로 읽습니다 (현재 6000.3.24f1, GameCI `unityci/editor` Linux 이미지).
- **결과**: PR/커밋에 `EditMode test results` 체크로 표시되고, 결과 XML은 `editmode-test-results` 아티팩트로 올라갑니다.
- `Library/` 는 캐시되어 두 번째 실행부터 빨라집니다.

## 처음 한 번: Unity 라이선스 시크릿 등록

GitHub 저장소 **Settings → Secrets and variables → Actions → New repository secret** 에서 아래를 추가합니다. 시크릿이 없으면 워크플로가 라이선스 활성화 단계에서 `License activation strategy could not be determined` 로 실패합니다.

### Unity Personal 라이선스 (무료)

| 시크릿 | 값 |
|---|---|
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |

이 둘만 있으면 됩니다. 계정에 2단계 인증(2FA)이 켜져 있으면 CI에서 로그인할 수 없으니, 이메일/비밀번호로만 로그인되는 계정을 쓰거나 GameCI 문서를 참고하세요.

### Unity Pro/Plus 라이선스

위 두 개에 더해 `UNITY_SERIAL` (시리얼 키)을 추가합니다.

### Enterprise/Industry (.ulf 파일)

`UNITY_LICENSE` 에 `.ulf` 파일의 전체 내용을 넣습니다. Personal/Pro라면 필요 없습니다.

워크플로는 네 시크릿을 모두 넘기고, 비어 있는 것은 무시됩니다. 자세한 내용: <https://game.ci/docs/github/activation>

## 참고

- 테스트를 추가하면 `TowerDefense.Tests.EditMode` 어셈블리 안에 두면 자동으로 포함됩니다.
- PlayMode 테스트가 생기면 `testMode: all` 로 바꾸면 됩니다.
- 대용량 에셋 때문에 Git LFS를 도입해도 체크아웃에 `lfs: true` 가 이미 켜져 있습니다.
