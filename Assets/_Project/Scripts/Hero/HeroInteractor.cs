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
            foreach (var t in Tower.All) if (t.Spec.MaxLevel > 0) Consider(t, ref best, ref bestD); // 업그레이드 없는 포탑은 상호작용 대상 아님
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

        // docs/INGAME_UI.md: 왼쪽 아래 영웅 칸(강림·귀환 쿨타임), 필드 시점 아래 줄(행동 키), 대상 머리 위 안내·진행 막대, 가운데 업그레이드 선택 창
        private void OnGUI()
        {
            if (Session == null || !Session.showHud || Session.Over) return;
            DrawHeroBox();
            if (_hero.State == HeroState.Active) DrawActionBar();

            float cx = Screen.width / 2f;
            if (Time.time < _toastUntil)
            {
                var t = new Rect(cx - 250, Hud.BottomBar.y - 70, 500, 32);
                Hud.Panel(t, 0.6f);
                GUI.Label(t, _toast, Hud.Text(16, TextAnchor.MiddleCenter));
            }

            if (_target != null)
            {
                var p = OverHead(_target);
                var box = new Rect(p.x - 110, p.y - 50, 220, 50);
                Hud.Panel(box);
                string what = _target is FieldTree ? "나무 베기" : $"{BranchNames[(int)_branch]} 업그레이드";
                GUI.Label(new Rect(box.x, box.y + 4, box.width, 22), $"{what} · {_duration - _progress:0.0}초", Hud.Text(15, TextAnchor.MiddleCenter));
                Hud.Bar(new Rect(box.x + 12, box.y + 32, box.width - 24, 8), Progress01, Hud.Gold);
            }
            else if (_choosing != null) DrawUpgradeWindow();
            else if (_near != null)
            {
                var p = OverHead(_near);
                var style = Hud.Text(15, TextAnchor.MiddleCenter);
                var size = style.CalcSize(new GUIContent(_near.Prompt));
                var box = new Rect(p.x - size.x / 2f - 10, p.y - 32, size.x + 20, 30);
                Hud.Panel(box);
                GUI.Label(box, _near.Prompt, style);
            }
        }

        /// <summary>대상 머리 위 GUI 좌표. 가까이 서서 머리가 화면 밖이면 화면 안(위 줄 아래 ~ 아래 줄 위)으로 붙잡는다.</summary>
        private Vector2 OverHead(IHeroInteractable t)
        {
            float h = t is Tower ? 12f : 9f; // 포탑 11m, 나무 6~8m (docs/SCALE.md)
            var p = Hud.ToGui(_hero.cam, t.Position + Vector3.up * h) ?? new Vector2(Screen.width / 2f, Screen.height - 180f);
            return new Vector2(Mathf.Clamp(p.x, 130f, Screen.width - 130f), Mathf.Clamp(p.y, Hud.TopRight.yMax + 60f, Hud.BottomBar.y - 90f));
        }

        private void DrawHeroBox()
        {
            var r = Hud.HeroBox;
            Hud.Panel(r);
            var title = Hud.Text(16);
            var sub = Hud.Text(13);
            var bar = new Rect(r.x + 12, r.y + 32, r.width - 24, 8);
            switch (_hero.State)
            {
                case HeroState.Perched:
                    float left = _hero.DescendCooldownLeft, total = _hero.DescendCooldownTotal;
                    GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, 26), "영웅 강림", title);
                    Hud.Bar(bar, total > 0f ? 1f - left / total : 1f, left > 0f ? Hud.Dim : Hud.Gold);
                    GUI.Label(new Rect(r.x + 12, r.y + 42, r.width - 24, 20), left > 0f ? $"대기 {left:0}초" : "Ctrl+클릭으로 필드에 내려가기", sub);
                    break;
                case HeroState.Descending:
                    GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, 26), "강림 중", title);
                    break;
                case HeroState.Active:
                    float back = _hero.ReturnCooldownLeft;
                    GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, 26), "영웅 · 필드", title);
                    Hud.Bar(bar, 1f - back / Mathf.Max(0.01f, _hero.returnCooldownSec), back > 0f ? Hud.Dim : Hud.Good);
                    GUI.Label(new Rect(r.x + 12, r.y + 42, r.width - 24, 20), back > 0f ? $"귀환 가능까지 {back:0}초" : "T로 절벽에 돌아가기", sub);
                    break;
            }
        }

        /// <summary>필드 시점 아래 줄: 포탑 줄 대신 영웅 행동 키. 쓸 수 없는 키는 흐리게.</summary>
        private void DrawActionBar()
        {
            var r = Hud.BottomBar;
            float back = _hero.ReturnCooldownLeft;
            var slots = new[]
            {
                ("좌클릭", "공격", true),
                ("E", _near is FieldTree ? "나무 베기" : _near is Tower ? "업그레이드" : "상호작용", _near != null && _target == null),
                ("T", back > 0f ? $"귀환 {back:0}초" : "귀환", back <= 0f),
            };
            float w = (r.width - 12f) / slots.Length;
            var style = Hud.Text(15, TextAnchor.MiddleCenter);
            for (int i = 0; i < slots.Length; i++)
            {
                var (key, label, on) = slots[i];
                var s = new Rect(r.x + i * (w + 6f), r.y, w, r.height);
                Hud.Panel(s);
                if (on && i > 0) Hud.Frame(s, Color.white);
                string c = Hud.Hex(on ? Color.white : Hud.Dim);
                GUI.Label(s, $"<color={c}><size=12>{key}</size>\n{label}</color>", style);
            }
        }

        /// <summary>업그레이드 방향 선택 창: 방향마다 바뀌기 전후 수치, 비용, 걸리는 시간. 골드가 모자라면 빨갛게.</summary>
        private void DrawUpgradeWindow()
        {
            var sp = _choosing.Spec;
            var box = new Rect(Screen.width / 2f - 270, Screen.height * 0.55f - 80, 540, 160);
            Hud.Panel(box, 0.9f);
            GUI.Label(new Rect(box.x + 14, box.y + 6, box.width - 28, 26), $"{sp.displayName} 업그레이드", Hud.Text(17));
            GUI.Label(new Rect(box.x + 14, box.y + 6, box.width - 28, 26), $"보유 {Session.Gold}G", Hud.Text(15, TextAnchor.MiddleRight));
            var btn = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };
            for (int i = 0; i < 3; i++)
            {
                var b = (UpgradeBranch)i;
                int lv = _choosing.Level(b), cost = _choosing.NextCost(b);
                string label;
                if (cost < 0) label = $"{i + 1}  {BranchNames[i]}\n최대 단계";
                else
                {
                    string change = b == UpgradeBranch.AtkSpeed ? $"간격 {sp.CooldownAt(lv):0.00} → {sp.CooldownAt(lv + 1):0.00}초"
                        : b == UpgradeBranch.Power ? $"피해 {sp.AtkAt(lv):0} → {sp.AtkAt(lv + 1):0}"
                        : $"독 초당 {sp.poisonDpsPerLevel * lv:0} → {sp.poisonDpsPerLevel * (lv + 1):0}";
                    string price = $"{cost}G · {_choosing.NextSec(b) * Session.Bonuses.UpgradeTimeMul:0}초";
                    if (!Session.Economy.CanAfford(cost)) price = $"<color={Hud.Hex(Hud.Bad)}>{price} (골드 부족)</color>";
                    label = $"{i + 1}  {BranchNames[i]} Lv{lv + 1}\n{change}\n{price}";
                }
                if (GUI.Button(new Rect(box.x + 14 + i * 174, box.y + 38, 164, 84), label, btn)) StartUpgrade(b);
            }
            GUI.Label(new Rect(box.x + 14, box.yMax - 32, box.width - 28, 26), "범위를 벗어나면 취소 · 골드는 완료할 때 냄 · E/ESC 닫기", Hud.Text(13));
        }
    }
}
