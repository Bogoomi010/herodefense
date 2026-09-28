using System;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    /// <summary>플레이어 스킬 효과 종류. 값은 비율(0.1 = 10%) 또는 골드.</summary>
    public enum SkillEffect
    {
        None,
        ZoneShrinkPct,   // 포탑 구역 기준값의 %만큼 축소
        UpgradeTimePct,  // 포탑 업그레이드 시간 %만큼 단축
        DescentCdPct,    // 영웅 강림 쿨타임 %만큼 단축
        ExpPct,          // 플레이어 경험치 획득량 %만큼 증가
        StartGold,       // 스테이지 시작 골드 +값
        GoldPct,         // 골드 획득량 %만큼 증가
        DescentDamage,   // 영웅 강림 피해 +값 (강림 지역 크립 전부, docs/HERO.md). 에셋이 정수로 저장하므로 새 값은 끝에만 붙인다
    }

    /// <summary>스킬트리 노드 하나. 개발자가 SkillTreeDef 에셋에서 편집한다.</summary>
    [Serializable]
    public sealed class SkillNode
    {
        public string id;
        public string title;
        public SkillEffect effect;
        public float value;
        /// <summary>핵심 노드(큰 마름모)</summary>
        public bool keystone;
        /// <summary>트리 가운데의 기본 스킬. 처음부터 찍혀 있다. 트리에 하나.</summary>
        public bool start;
        /// <summary>화면 위치 (칸 단위, 시작 노드 = 0,0)</summary>
        public float x, y;
        /// <summary>선으로 이어진 노드 id. 한쪽에만 적어도 양방향으로 본다.</summary>
        public List<string> links = new List<string>();
    }

    public enum NodeState { Taken, Open, Locked }

    /// <summary>
    /// 스킬트리 규칙 (docs/PLAYER_SKILL_TREE.md): 찍힌 노드와 이어진 노드만 열리고, 노드 하나에 1포인트.
    /// 초기화는 무료이며 시작 노드만 남기고 포인트를 돌려준다.
    /// </summary>
    public sealed class SkillTree
    {
        public const int NodeCost = 1;

        private readonly Dictionary<string, SkillNode> _byId = new Dictionary<string, SkillNode>();
        private readonly Dictionary<string, HashSet<string>> _adj = new Dictionary<string, HashSet<string>>();

        public IReadOnlyCollection<SkillNode> Nodes => _byId.Values;

        public SkillTree(IEnumerable<SkillNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                _byId[n.id] = n;
                if (!_adj.ContainsKey(n.id)) _adj[n.id] = new HashSet<string>();
            }
            foreach (var n in _byId.Values)
                foreach (var l in n.links)
                {
                    if (!_byId.ContainsKey(l) || l == n.id) continue;
                    _adj[n.id].Add(l);
                    _adj[l].Add(n.id);
                }
        }

        public SkillNode Get(string id) => id != null && _byId.TryGetValue(id, out var n) ? n : null;

        public bool IsTaken(PlayerProfile p, string id) => Get(id) is SkillNode n && (n.start || p.nodes.Contains(id));

        public NodeState StateOf(PlayerProfile p, string id)
        {
            if (IsTaken(p, id)) return NodeState.Taken;
            foreach (var l in _adj[id]) if (IsTaken(p, l)) return NodeState.Open;
            return NodeState.Locked;
        }

        public bool CanTake(PlayerProfile p, string id) =>
            Get(id) != null && p.skillPoints >= NodeCost && StateOf(p, id) == NodeState.Open;

        public bool Take(PlayerProfile p, string id)
        {
            if (!CanTake(p, id)) return false;
            p.skillPoints -= NodeCost;
            p.nodes.Add(id);
            return true;
        }

        /// <summary>무료 초기화: 찍은 노드를 모두 되돌리고 포인트를 돌려준다.</summary>
        public void Reset(PlayerProfile p)
        {
            p.skillPoints += p.nodes.Count * NodeCost;
            p.nodes.Clear();
        }

        /// <summary>찍힌 노드(시작 노드 포함) 효과 합산.</summary>
        public PlayerBonuses Bonuses(PlayerProfile p)
        {
            var b = new PlayerBonuses();
            foreach (var n in _byId.Values)
                if (IsTaken(p, n.id)) b.Add(n.effect, n.value);
            return b;
        }
    }

    /// <summary>찍은 플레이어 스킬의 합. 스테이지 시작 시 한 번 계산해 세션이 들고 있는다.</summary>
    public sealed class PlayerBonuses
    {
        public float ZoneShrink, UpgradeTimeCut, DescentCdCut, ExpBonus, GoldBonus, DescentDamage;
        public int StartGold;

        /// <summary>포탑 구역 반지름 배율. 하한(바닥 반지름)은 쓰는 쪽에서 건다.</summary>
        public float ZoneMul => Math.Max(0f, 1f - ZoneShrink);
        // ponytail: 시간 단축은 최대 90%까지, 수치 설계가 끝나면 하한을 다시 본다
        public float UpgradeTimeMul => Math.Max(0.1f, 1f - UpgradeTimeCut);
        public float DescentCdMul => Math.Max(0.1f, 1f - DescentCdCut);
        public float ExpMul => 1f + ExpBonus;
        public float GoldMul => 1f + GoldBonus;

        public void Add(SkillEffect e, float v)
        {
            switch (e)
            {
                case SkillEffect.ZoneShrinkPct: ZoneShrink += v; break;
                case SkillEffect.UpgradeTimePct: UpgradeTimeCut += v; break;
                case SkillEffect.DescentCdPct: DescentCdCut += v; break;
                case SkillEffect.ExpPct: ExpBonus += v; break;
                case SkillEffect.StartGold: StartGold += (int)Math.Round(v); break;
                case SkillEffect.GoldPct: GoldBonus += v; break;
                case SkillEffect.DescentDamage: DescentDamage += v; break;
            }
        }
    }
}
