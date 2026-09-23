using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>영웅이 곁에 머물며 작업하는 대상 (docs/HERO_INTERACTION.md). 구현체: 포탑(업그레이드), 나무(베기).</summary>
    public interface IHeroInteractable
    {
        Vector3 Position { get; }
        float InteractRadius { get; }
        /// <summary>안내 문구 (예: "E: 나무 베기")</summary>
        string Prompt { get; }
        bool IsAlive { get; }
    }
}
