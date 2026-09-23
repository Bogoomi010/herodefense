using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Hero
{
    /// <summary>
    /// 영웅 상호작용 (docs/HERO_INTERACTION.md): 범위 안 가장 가까운 대상 → E → 진행 → 범위를 벗어나면 취소(진행도 0).
    /// 나무는 바로 베기 시작, 포탑은 방향 선택(1/2/3) 후 업그레이드 시작.
    /// 업그레이드 골드는 시작할 때 잔액을 확인하고 완료할 때 낸다 — 취소하면 골드를 잃지 않는다.
    /// </summary>
    [RequireComponent(typeof(Hero))]
    public sealed class HeroInteractor : MonoBehaviour
    {
        private Hero _hero;
        private IHeroInteractable _near;
        private IHeroInteractable _target; // 진행 중인 대상
        private Tower _choosing; // 방향 선택 창을 연 포탑
        private UpgradeBranch _branch;
        private float _progress, _duration;
        private string _toast = "";
        private float _toastUntil;

        private static readonly string[] BranchNames = { "공격속도", "파워", "독" };

        public float Progress01 => _target != null && _duration > 0f ? _progress / _duration : 0f;

        private void Awake() => _hero = GetComponent<Hero>();

        private EnemySpawner Session => _hero.session;

        private void Update()
        {
            if (_hero.State != HeroState.Active || Session == null || Session.Over)
            {
                Cancel(null);
                _near = null;
                return;
            }

            var kb = Keyboard.current;
            if (_target != null)
            {
                if (!_target.IsAlive || !InRange(_target))
                {
                    Cancel("범위를 벗어나 취소됨");
                    return;
                }
                _progress += Time.deltaTime;
                if (_progress >= _duration) Complete();
                return;
            }

            _near = Nearest();
            if (_choosing != null)
            {
                if (_near != (IHeroInteractable)_choosing || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))) { _choosing = null; return; }
                if (kb == null) return;
                if (kb.digit1Key.wasPressedThisFrame) StartUpgrade(UpgradeBranch.AtkSpeed);
                else if (kb.digit2Key.wasPressedThisFrame) StartUpgrade(UpgradeBranch.Power);
                else if (kb.digit3Key.wasPressedThisFrame) StartUpgrade(UpgradeBranch.Poison);
                return;
            }

            if (kb != null && kb.eKey.wasPressedThisFrame) Interact();
        }

        /// <summary>E: 가까운 나무는 베기 시작, 포탑은 방향 선택 창을 연다.</summary>
        public void Interact()
        {
            if (_target != null || _choosing != null) return;
            _near = Nearest();
            if (_near is FieldTree tree) Begin(tree, tree.chopSec);
            else if (_near is Tower tower) _choosing = tower;
        }

        public bool IsBusy => _target != null;
        public bool IsChoosing => _choosing != null;

        private IHeroInteractable Nearest()
        {
            IHeroInteractable best = null;
            float bestD = float.MaxValue;
            foreach (var t in FieldTree.All) Consider(t, ref best, ref bestD);
            foreach (var t in Tower.All) Consider(t, ref best, ref bestD);
            return best;
        }

        private void Consider(IHeroInteractable c, ref IHeroInteractable best, ref float bestD)
        {
            float d = FlatDist2(c.Position);
            if (d <= c.InteractRadius * c.InteractRadius && d < bestD) { best = c; bestD = d; }
        }

        private bool InRange(IHeroInteractable c) => FlatDist2(c.Position) <= c.InteractRadius * c.InteractRadius;

        private float FlatDist2(Vector3 p)
        {
            float dx = p.x - transform.position.x, dz = p.z - transform.position.z;
            return dx * dx + dz * dz;
        }

        /// <summary>선택 창에서 방향을 고른다 (1/2/3).</summary>
        public void StartUpgrade(UpgradeBranch b)
        {
            var tower = _choosing;
            if (tower == null) return;
            _choosing = null;
            int cost = tower.NextCost(b);
            if (cost < 0) { Toast($"{BranchNames[(int)b]}은(는) 최대 단계입니다"); return; }
            if (!Session.Economy.CanAfford(cost)) { Toast($"골드 부족 ({cost}G 필요)"); return; }
            _branch = b;
            Begin(tower, tower.NextSec(b) * Session.Bonuses.UpgradeTimeMul);
        }

        private void Begin(IHeroInteractable target, float duration)
        {
            _target = target;
            _duration = duration;
            _progress = 0f;
        }

        private void Complete()
        {
            var target = _target;
            _target = null;
            if (target is FieldTree tree)
            {
                int exp = Session.AddStageExp(tree.exp);
                Destroy(tree.gameObject);
                Toast($"나무를 베었다  경험치 +{exp}");
            }
            else if (target is Tower tower)
            {
                // 영웅이 필드에 있는 동안 골드는 줄지 않으므로(설치는 절벽 시점에서만) 시작 때 확인한 잔액으로 충분하다
                int cost = tower.NextCost(_branch);
                if (cost < 0 || !Session.Economy.TrySpend(cost, GoldSource.TowerUpgrade)) { Toast("업그레이드 실패"); return; }
                tower.ApplyUpgrade(_branch);
                Toast($"{BranchNames[(int)_branch]} {tower.Level(_branch)}단계 완료");
            }
        }

        private void Cancel(string why)
        {
            _choosing = null;
            if (_target == null) return;
            _target = null;
            _progress = 0f;
            if (why != null) Toast(why);
        }

        private void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.time + 2.5f;
        }

        // ---------- HUD ----------

        private void OnGUI()
        {
            if (Session == null || !Session.showHud) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            float cx = Screen.width / 2f, y = Screen.height - 150f;

            string status = _hero.State == HeroState.Perched
                ? (_hero.DescendCooldownLeft > 0f ? $"강림 대기 {_hero.DescendCooldownLeft:0}s" : "Ctrl+클릭: 강림")
                : _hero.State == HeroState.Active
                    ? (_hero.ReturnCooldownLeft > 0f ? $"귀환 가능까지 {_hero.ReturnCooldownLeft:0}s" : "T: 귀환")
                    : "";
            GUI.Label(new Rect(cx - 200, Screen.height - 40, 400, 30), status, style);

            if (Time.time < _toastUntil) GUI.Label(new Rect(cx - 300, y - 40, 600, 30), _toast, style);

            if (_target != null)
            {
                var bar = new Rect(cx - 150, y, 300, 22);
                GUI.Box(bar, GUIContent.none);
                GUI.Box(new Rect(bar.x, bar.y, bar.width * Progress01, bar.height), GUIContent.none);
                string what = _target is FieldTree ? "나무 베는 중" : $"{BranchNames[(int)_branch]} 업그레이드 중";
                GUI.Label(new Rect(cx - 200, y + 24, 400, 30), $"{what}  {_duration - _progress:0.0}s", style);
            }
            else if (_choosing != null)
            {
                var btn = new GUIStyle(GUI.skin.button) { fontSize = 15 };
                var box = new Rect(cx - 230, y - 20, 460, 110);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x, box.y + 4, box.width, 26), $"업그레이드 방향 (보유 {Session.Gold}G)", style);
                for (int i = 0; i < 3; i++)
                {
                    var b = (UpgradeBranch)i;
                    int cost = _choosing.NextCost(b);
                    string label = cost < 0 ? $"{i + 1}. {BranchNames[i]}\n최대" : $"{i + 1}. {BranchNames[i]} Lv{_choosing.Level(b) + 1}\n{cost}G · {_choosing.NextSec(b) * Session.Bonuses.UpgradeTimeMul:0}s";
                    GUI.enabled = cost >= 0 && Session.Economy.CanAfford(cost);
                    if (GUI.Button(new Rect(box.x + 10 + i * 150, box.y + 36, 140, 60), label, btn)) StartUpgrade(b);
                    GUI.enabled = true;
                }
            }
            else if (_near != null)
            {
                GUI.Label(new Rect(cx - 200, y, 400, 30), _near.Prompt, style);
            }
        }
    }
}
