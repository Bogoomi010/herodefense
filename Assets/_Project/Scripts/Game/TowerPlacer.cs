using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Game
{
    /// <summary>
    /// 포탑 설치 모드 (docs/TOWER_PLACEMENT.md). 절벽 시점에서만: B 또는 HUD 버튼으로 켜고, 좌클릭 설치, 우클릭·ESC로 끈다.
    /// 커서를 따라 미리보기와 구역 원을 보여 준다 (설치 가능 초록 / 불가 빨강). 기존 포탑의 구역도 함께 보인다.
    /// </summary>
    public sealed class TowerPlacer : MonoBehaviour
    {
        public TowerSpec spec = new TowerSpec();
        [Tooltip("포탑 모델 (발밑 원점). 비우면 큐브")]
        public GameObject towerPrefab;

        public bool Active { get; private set; }
        public PlaceResult LastResult { get; private set; }

        /// <summary>이번 스테이지의 포탑 구역 반지름 (기준값 × 스킬 배율, 하한 바닥 반지름)</summary>
        public float ZoneRadius => PlacementRules.ZoneRadius(spec.zoneRadius, _session.Bonuses.ZoneMul, spec.footprintRadius);

        private EnemySpawner _session;
        private TileMap _map;
        private Camera _cam;
        private GameObject _ghost;
        private LineRenderer _ghostZone, _ghostFoot;

        private static readonly Color Ok = new Color(0.3f, 1f, 0.4f), Bad = new Color(1f, 0.3f, 0.25f);

        private void Start()
        {
            _session = GetComponent<EnemySpawner>();
            _map = _session.map;
            _cam = Camera.main;
        }

        public bool CanEnter => _session != null && !_session.Over && !_session.HeroInField;

        public void Toggle() => SetActive(!Active);

        public void SetActive(bool on)
        {
            on &= CanEnter;
            if (on == Active) return;
            Active = on;
            if (_ghost == null) CreateGhost();
            _ghost.SetActive(false);
            foreach (var t in Tower.All) t.ShowZone(on, ZoneRadius);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) Toggle();
            if (!Active) return;
            if (!CanEnter || (kb != null && kb.escapeKey.wasPressedThisFrame) || Mouse.current == null || Mouse.current.rightButton.wasPressedThisFrame)
            {
                SetActive(false);
                return;
            }

            var mouse = Mouse.current.position.ReadValue();
            bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed); // Ctrl은 영웅 강림 입력
            if (ctrl || _session.IsOverHud(mouse) || _cam == null || !Physics.Raycast(_cam.ScreenPointToRay(mouse), out var hit) || _map.WorldToTile(hit.point) == null)
            {
                _ghost.SetActive(false);
                return;
            }

            var p = hit.point;
            LastResult = Check(p);
            _ghost.SetActive(true);
            _ghost.transform.position = p + Vector3.up * 0.05f;
            var c = LastResult == PlaceResult.Ok ? Ok : Bad;
            Rings.SetColor(_ghostZone, c);
            Rings.SetColor(_ghostFoot, c);
            Rings.SetRadius(_ghostZone, ZoneRadius);

            if (Mouse.current.leftButton.wasPressedThisFrame) TryPlace(p);
        }

        /// <summary>지면 위 p에 설치를 시도한다. 판정을 통과하면 골드를 내고 포탑을 만든다.</summary>
        public PlaceResult TryPlace(Vector3 p)
        {
            var r = Check(p);
            if (r != PlaceResult.Ok) return r;
            if (!_session.Economy.TrySpend(spec.cost, GoldSource.TowerBuild)) return PlaceResult.NoGold;
            var t = Tower.Create(_session, spec, p, towerPrefab);
            t.ShowZone(Active, ZoneRadius);
            return r;
        }

        private PlaceResult Check(Vector3 p)
        {
            var trees = new List<Circle>();
            foreach (var t in FieldTree.All) trees.Add(t.Footprint);
            var towers = new List<Circle>();
            foreach (var t in Tower.All) towers.Add(new Circle(t.Position.x, t.Position.z, 0f));
            return PlacementRules.Check(p.x, p.z, spec.footprintRadius, ZoneRadius, _session.Gold, spec.cost,
                (x, z) => _map.IsGround(new Vector3(x, 0f, z)), trees, towers);
        }

        private void CreateGhost()
        {
            _ghost = new GameObject("PlacementGhost");
            _ghost.transform.SetParent(transform, false);
            _ghostZone = Rings.Circle(_ghost.transform, Ok, 0.05f);
            _ghostFoot = Rings.Circle(_ghost.transform, Ok, 0.08f);
            Rings.SetRadius(_ghostFoot, spec.footprintRadius);
        }

        public static string Describe(PlaceResult r) => r switch
        {
            PlaceResult.NoGold => "골드 부족",
            PlaceResult.NotGround => "녹색 지대가 아님",
            PlaceResult.Tree => "나무가 있음",
            PlaceResult.TooClose => "다른 포탑과 너무 가까움",
            _ => "설치 가능",
        };
    }
}
