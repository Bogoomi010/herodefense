# TowerDefense

Unity 타워 디펜스 게임 프로젝트.

## 개발 환경

| 항목 | 값 |
|---|---|
| Unity | 6000.3.24f1 (Unity 6.3) |
| 렌더 파이프라인 | URP (Universal 3D 템플릿) |
| 입력 | Input System 패키지 |
| 에디터 경로 | `D:\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe` |

## 폴더 구조

```
Assets/_Project/
  Scripts/          # C# 소스 (Core, Towers, Enemies, Waves, Map, UI)
  Prefabs/          # 프리팹 (Towers, Enemies, Projectiles, UI)
  Scenes/           # 게임 씬
  ScriptableObjects/# 타워/적/웨이브 데이터
  Materials/
  Art/              # Models, Textures, Sprites
  Audio/
  Animations/
```

템플릿이 생성한 `Assets/Scenes/SampleScene.unity` 와 `Assets/Settings/` (URP 설정)은 그대로 둡니다.

## 프로젝트 열기

```bash
unity open D:\Workspace\TowerDefense
```

## Git 규칙

- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, IDE 파일은 커밋하지 않습니다 (`.gitignore`).
- 씬/프리팹/에셋 충돌은 UnityYAMLMerge로 해결합니다 (`.gitattributes`).
- 대용량 바이너리 에셋이 늘어나면 Git LFS를 도입합니다.
