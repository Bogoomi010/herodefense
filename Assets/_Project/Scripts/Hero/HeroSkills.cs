using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Hero
{
    /// <summary>
    /// 영웅 기술 Q·E (docs/SKILL_MENU.md, docs/HERO_GUNNER.md). 장착·단계는 저장 슬롯에서 읽는다(메뉴에서만 바꾼다).
    /// 필드 시점(Active)에서만 쓴다. 쿨타임은 실제 시간(배속 영향 없음), 스테이지 시작 때 0, 귀환해도 계속 돈다.
    /// </summary>
    [RequireComponent(typeof(Hero))]
    public sealed class HeroSkills : MonoBehaviour
    {
        public static readonly string[] Keys = { "Q", "E" };

        private Hero _hero;
        private readonly HeroSkillDef[] _slots = new HeroSkillDef[2];
        private readonly int[] _levels = new int[2];
        private readonly float[] _readyAt = new float[2];

        public HeroSkillDef Slot(int i) => _slots[i];
        public float CooldownLeft(int i) => Mathf.Max(0f, _readyAt[i] - Time.time);

        private EnemySpawner Session => _hero.session;

        private void Start()
        {
            _hero = GetComponent<Hero>();
            var p = Session != null ? Session.Profile : null;
            if (p == null) return;
            var l = Heroes.Loadout(p, _hero.Kind);
            string[] ids = { l.q, l.e };
            for (int i = 0; i < 2; i++)
            {
                var s = Heroes.Skill(ids[i]);
                if (s == null || s.hero != _hero.Kind || Heroes.LevelOf(p, s) == 0) continue;
                _slots[i] = s;
                _levels[i] = Heroes.LevelOf(p, s);
            }
        }

        private void Update()
        {
            TickPulses();
            TickBombs();
            if (_hero == null || _hero.State != HeroState.Active || Session == null || Session.Over) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.qKey.wasPressedThisFrame) TryCast(0);
            if (kb.eKey.wasPressedThisFrame) TryCast(1);
        }

        private void TryCast(int i)
        {
            var s = _slots[i];
            if (s == null || Time.time < _readyAt[i]) return;
            float v = s.ValueAt(_levels[i]);
            float cd = s.cooldownSec;
            Cast(s.id, v, _levels[i], ref cd);
            _readyAt[i] = Time.time + cd;
        }

        private void Cast(string id, float v, int level, ref float cd)
        {
            var here = transform.position;
            var aim = _hero.AimFlat;
            var list = Session.Enemies;
            switch (id)
            {
                case "sword.whirl": // 주변 3m 피해
                    for (int k = list.Count - 1; k >= 0; k--)
                        if (Flat2(list[k].transform.position, here) <= 3f * 3f) Session.Damage(list[k], v, _hero.dmgType);
                    Pulse(here, 3f, new Color(1f, 0.45f, 0.35f), 0.25f);
                    break;

                case "sword.dash": // 앞으로 8m, 지나간 크립 피해 + 옆으로 밀침
                    if (level >= 3) cd -= 2f;
                    for (int k = list.Count - 1; k >= 0; k--)
                    {
                        var e = list[k];
                        if (!Hero.OnLine(e, here, aim, 8f, 1.5f, out float along)) continue;
                        var onPath = here + aim * along;
                        if (!Session.Damage(e, v, _hero.dmgType)) e.Knockback(onPath, 7f);
                    }
                    _hero.Dash(aim, 8f, 0.2f);
                    break;

                case "sword.cry": // 주변 10m 포탑 공격 간격 −25%
                    foreach (var t in Tower.All)
                        if (Flat2(t.Position, here) <= 10f * 10f) t.RallyUntil = Time.time + v;
                    Pulse(here, 10f, Hud.Gold, 0.4f);
                    break;

                case "sword.spear": // 처음 맞은 크립 하나 (20m)
                case "gun.pierce":  // 일직선 40m, 맞은 크립 전부
                {
                    bool pierce = id == "gun.pierce";
                    float range = pierce ? 40f : 20f;
                    var from = here + Vector3.up * 0.4f;
                    if (pierce)
                    {
                        for (int k = list.Count - 1; k >= 0; k--)
                            if (Hero.OnLine(list[k], from, aim, range, 1.2f, out _)) Session.Damage(list[k], v, _hero.dmgType);
                    }
                    else
                    {
                        var target = _hero.FirstAlong(from, aim, range, 1.2f);
                        if (target != null) { range = Vector3.Distance(from, target.transform.position); Session.Damage(target, v, _hero.dmgType); }
                    }
                    _hero.Beam(from, from + aim * range, pierce ? new Color(0.6f, 0.85f, 1f) : new Color(0.9f, 0.9f, 0.9f), 0.2f);
                    break;
                }

                case "gun.rapid":
                    _hero.RapidUntil = Time.time + v;
                    break;

                case "gun.shotgun": // 앞쪽 부채꼴 8m 60°, 피해 + 뒤로 밀침
                    for (int k = list.Count - 1; k >= 0; k--)
                    {
                        var e = list[k];
                        var d = e.transform.position - here;
                        d.y = 0f;
                        if (d.sqrMagnitude > 8f * 8f || Vector3.Angle(aim, d) > 30f) continue;
                        if (!Session.Damage(e, v, _hero.dmgType)) e.Knockback(here, 8f);
                    }
                    Pulse(here + aim * 4f, 4f, new Color(1f, 0.8f, 0.4f), 0.15f);
                    break;

                case "gun.bomb": // 조준 방향 앞 땅(최대 25m)에 1초 뒤 4m 폭발
                {
                    var at = here + aim * 12f;
                    if (_hero.cam != null && Physics.Raycast(_hero.cam.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var hit, 60f)
                        && Flat2(hit.point, here) <= 25f * 25f) at = hit.point;
                    at.y = Session.GroundY(at);
                    var ring = Rings.Circle(null, new Color(1f, 0.4f, 0.2f), 0.15f);
                    ring.transform.position = at + Vector3.up * 0.15f;
                    Rings.SetRadius(ring, 4f);
                    _bombs.Add((at, Time.time + 1f, v, ring));
                    break;
                }
            }
        }

        private static float Flat2(Vector3 a, Vector3 b) => (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z);

        // ---------- 화약 폭탄: 표시 원 → 1초 뒤 폭발 ----------

        private readonly List<(Vector3 at, float when, float dmg, LineRenderer ring)> _bombs = new List<(Vector3, float, float, LineRenderer)>();

        private void TickBombs()
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                var b = _bombs[i];
                if (Time.time < b.when) continue;
                _bombs.RemoveAt(i);
                Destroy(b.ring.gameObject);
                if (Session == null || Session.Over) continue;
                var list = Session.Enemies;
                for (int k = list.Count - 1; k >= 0; k--)
                    if (Flat2(list[k].transform.position, b.at) <= 4f * 4f) Session.Damage(list[k], b.dmg, _hero.dmgType);
                Pulse(b.at, 4f, new Color(1f, 0.5f, 0.2f), 0.3f);
            }
        }

        // ---------- 퍼지는 바닥 원 (기술 효과 표시) ----------

        private readonly List<(LineRenderer ring, float start, float radius, float sec)> _pulses = new List<(LineRenderer, float, float, float)>();

        private void Pulse(Vector3 at, float radius, Color c, float sec)
        {
            var ring = Rings.Circle(null, c, 0.3f);
            ring.transform.position = new Vector3(at.x, Session.GroundY(at) + 0.15f, at.z);
            _pulses.Add((ring, Time.time, radius, sec));
        }

        private void TickPulses()
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                float u = (Time.time - p.start) / p.sec;
                if (u >= 1f) { Destroy(p.ring.gameObject); _pulses.RemoveAt(i); continue; }
                Rings.SetRadius(p.ring, Mathf.Lerp(0.3f, p.radius, 1f - (1f - u) * (1f - u)));
                p.ring.widthMultiplier = 1f - u;
            }
        }
    }
}
