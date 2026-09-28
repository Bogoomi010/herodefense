# CI: EditMode 테스트

`.github/workflows/editmode-tests.yml` 이 [GameCI](https://game.ci/) `unity-test-runner` 로 `Assets/_Project/Tests/EditMode/` 의 EditMode 테스트를 돌립니다.

- **언제**: `main`, `develop`, `develop_hero` 에 push 할 때, 모든 PR, Actions 탭에서 수동 실행(`workflow_dispatch`).
- **Unity 버전**: `ProjectSettings/ProjectVersion.txt` 에서 자동으로 읽습니다 (현재 6000.3.24f1, `unityci/editor:ubuntu-6000.3.24f1-base-3` 이미지).
- **결과**: PR/커밋에 `EditMode test results` 체크로 표시되고, 결과 XML은 `editmode-test-results` 아티팩트로 올라갑니다.
- `Library/` 는 캐시되어 두 번째 실행부터 빨라집니다.

## 처음 한 번: Unity 라이선스 시크릿 등록

GitHub 저장소 **Settings → Secrets and variables → Actions → New repository secret** 에서 아래를 추가합니다. 시크릿이 없으면 워크플로가 라이선스 활성화 단계에서 실패합니다.

### Unity Personal 라이선스 (무료)

| 시크릿 | 값 |
|---|---|
| `UNITY_LICENSE` | `.ulf` 라이선스 파일의 **전체 내용** |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |

`.ulf` 파일은 Unity Hub로 Personal 라이선스를 활성화한 PC에 있습니다.

- Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
- macOS: `/Library/Application Support/Unity/Unity_lic.ulf`

파일을 메모장으로 열어 전체 내용을 `UNITY_LICENSE` 에 붙여 넣습니다.

### Unity Pro/Plus 라이선스

`UNITY_LICENSE` 대신 `UNITY_SERIAL` (시리얼 키)을 추가하고, 워크플로의 `env:` 에 `UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}` 를 한 줄 넣습니다. `UNITY_EMAIL`, `UNITY_PASSWORD` 는 그대로 필요합니다.

자세한 내용: <https://game.ci/docs/github/activation>

## 참고

- 테스트를 추가하면 `TowerDefense.Tests.EditMode` 어셈블리 안에 두면 자동으로 포함됩니다.
- PlayMode 테스트가 생기면 `testMode: all` 로 바꾸면 됩니다.
- 대용량 에셋 때문에 Git LFS를 도입해도 체크아웃에 `lfs: true` 가 이미 켜져 있습니다.
