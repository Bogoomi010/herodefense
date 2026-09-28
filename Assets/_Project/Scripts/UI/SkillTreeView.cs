using System.Collections.Generic;
using TowerDefense.Core;
using TowerDefense.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.UI
{
    /// <summary>
    /// 메뉴의 스킬트리 화면 (docs/PLAYER_SKILL_TREE.md). 노드는 SkillNode.x/y 칸 위치에 놓이고, 찍으면 바로 프로필에 저장된다.
    /// 끌어서 이동, 휠로 확대·축소.
    /// </summary>
    public sealed class SkillTreeView
    {
        private const float Spacing = 110f;
        /// <summary>노드 층 크기의 절반. 노드 층은 크기가 있어야 선이 잘리지 않는다 — 원점(시작 노드)을 층 가운데에 둔다.</summary>
        private const float Half = 1000f;

        private readonly SkillTree _tree;
        private readonly PlayerProfile _profile;
        private readonly VisualElement _content;
        private readonly Label _info, _desc;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private Vector2 _pan;
        private float _zoom = 1f;
        private bool _dragging;
        private Vector2 _dragStart, _panStart;

        private static readonly Color LineTaken = new Color(1f, 0.78f, 0.35f), LineOther = new Color(0.35f, 0.3f, 0.27f);

        public SkillTreeView(VisualElement viewport, Label info, Label desc, PlayerProfile profile)
        {
            _tree = SkillTreeDef.LoadOrEmpty();
            _profile = profile;
            _info = info;
            _desc = desc;

            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.style.position = Position.Absolute;
            _content.style.left = Length.Percent(50);
            _content.style.top = Length.Percent(50);
            _content.style.width = _content.style.height = Half * 2f;
            _content.style.marginLeft = _content.style.marginTop = -Half;
            _content.generateVisualContent += DrawLinks;
            viewport.Add(_content);

            foreach (var n in _tree.Nodes)
            {
                var node = n;
                var b = new Button(() => Take(node.id)) { text = node.title };
                b.AddToClassList("skill-node");
                if (node.keystone) b.AddToClassList("keystone");
                float size = node.keystone ? 84f : 64f;
                b.style.left = Half + node.x * Spacing - size / 2f;
                b.style.top = Half - node.y * Spacing - size / 2f; // y는 위쪽이 +
                b.RegisterCallback<PointerEnterEvent>(_ => _desc.text = Describe(node));
                _content.Add(b);
                _buttons[node.id] = b;
            }

            viewport.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _dragStart = e.position; _panStart = _pan; }, TrickleDown.TrickleDown);
            viewport.RegisterCallback<PointerMoveEvent>(e => { if (_dragging) { _pan = _panStart + (Vector2)e.position - _dragStart; Apply(); } });
            viewport.RegisterCallback<PointerUpEvent>(_ => _dragging = false);
            viewport.RegisterCallback<PointerLeaveEvent>(_ => _dragging = false);
            viewport.RegisterCallback<GeometryChangedEvent>(e => FitOnce(e.newRect.size));
            viewport.RegisterCallback<WheelEvent>(e => { _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.5f, 2f); Apply(); e.StopPropagation(); });

            Refresh();
            Apply();
        }

        private bool _fitted;

        /// <summary>처음 보일 때 트리 전체가 들어오도록 확대 배율을 맞춘다.</summary>
        private void FitOnce(Vector2 view)
        {
            if (_fitted || view.x < 1f || view.y < 1f) return;
            _fitted = true;
            float maxX = 0f, maxY = 0f;
            foreach (var n in _tree.Nodes) { maxX = Mathf.Max(maxX, Mathf.Abs(n.x)); maxY = Mathf.Max(maxY, Mathf.Abs(n.y)); }
            const float pad = 120f; // 노드 크기 + 여백
            _zoom = Mathf.Clamp(Mathf.Min(view.x / (maxX * 2f * Spacing + pad), view.y / (maxY * 2f * Spacing + pad)), 0.5f, 1f);
            Apply();
        }

        public void ResetSkills()
        {
            _tree.Reset(_profile);
            Save();
            Refresh();
        }

        private void Take(string id)
        {
            if (!_tree.Take(_profile, id)) return;
            Save();
            Refresh();
        }

        private void Save()
        {
            try { ProfileStore.SaveCurrent(_profile); }
            catch (System.Exception e) { Debug.LogError($"[SkillTreeView] 프로필 저장 실패: {e}"); }
        }

        public void Refresh()
        {
            _info.text = $"Lv {_profile.level}  ·  스킬 포인트 {_profile.skillPoints}";
            foreach (var kv in _buttons)
            {
                var b = kv.Value;
                var state = _tree.StateOf(_profile, kv.Key);
                b.EnableInClassList("taken", state == NodeState.Taken);
                b.EnableInClassList("open", state == NodeState.Open);
                b.EnableInClassList("locked", state == NodeState.Locked);
            }
            _content.MarkDirtyRepaint();
        }

        private void Apply()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector2(_zoom, _zoom));
        }

        private void DrawLinks(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineWidth = 4f;
            foreach (var n in _tree.Nodes)
                foreach (var l in n.links)
                {
                    var m = _tree.Get(l);
                    if (m == null) continue;
                    bool both = _tree.IsTaken(_profile, n.id) && _tree.IsTaken(_profile, m.id);
                    p.strokeColor = both ? LineTaken : LineOther;
                    p.BeginPath();
                    p.MoveTo(new Vector2(Half + n.x * Spacing, Half - n.y * Spacing));
                    p.LineTo(new Vector2(Half + m.x * Spacing, Half - m.y * Spacing));
                    p.Stroke();
                }
        }

        private string Describe(SkillNode n)
        {
            string effect = n.effect switch
            {
                SkillEffect.ZoneShrinkPct => $"포탑 구역 -{n.value:P0}",
                SkillEffect.UpgradeTimePct => $"업그레이드 시간 -{n.value:P0}",
                SkillEffect.DescentCdPct => $"강림 쿨타임 -{n.value:P0}",
                SkillEffect.ExpPct => $"경험치 획득 +{n.value:P0}",
                SkillEffect.StartGold => $"시작 골드 +{n.value:0}",
                SkillEffect.GoldPct => $"골드 획득 +{n.value:P0}",
                _ => "효과 없음",
            };
            string state = _tree.StateOf(_profile, n.id) switch
            {
                NodeState.Taken => "찍음",
                NodeState.Open => $"찍을 수 있음 ({SkillTree.NodeCost}포인트)",
                _ => "잠김 — 이어진 노드를 먼저 찍는다",
            };
            return $"{n.title}: {effect}  ·  {state}";
        }
    }
}
