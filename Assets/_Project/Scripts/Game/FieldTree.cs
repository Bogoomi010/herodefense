using System.Collections.Generic;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 개발자가 필드에 직접 배치하는 나무 (docs/TREE.md). 포탑 설치를 막고, 영웅이 베어 없애면 플레이어 경험치를 준다.
    /// 나무 프리팹(Prefabs/Trees)에 붙어 있다.
    /// </summary>
    public sealed class FieldTree : MonoBehaviour, IHeroInteractable
    {
        private static readonly List<FieldTree> _all = new List<FieldTree>();
        public static IReadOnlyList<FieldTree> All => _all;

        [Tooltip("포탑 설치를 막는 바닥 원 반지름 (m)")]
        public float footprintRadius = 1f;
        [Tooltip("영웅 상호작용 반지름 (m)")]
        public float interactRadius = 3f;
        public float chopSec = 30f;
        [Tooltip("베었을 때 플레이어 경험치 (스킬 배율 적용 전)")]
        public int exp = 20;

        public Vector3 Position => transform.position;
        public float InteractRadius => interactRadius;
        public string Prompt => "E: 나무 베기";
        public bool IsAlive => this != null && isActiveAndEnabled;
        public Circle Footprint => new Circle(transform.position.x, transform.position.z, footprintRadius);

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            DrawCircle(footprintRadius);
            Gizmos.color = Color.cyan;
            DrawCircle(interactRadius);
        }

        private void DrawCircle(float r)
        {
            var c = transform.position + Vector3.up * 0.05f;
            for (int i = 0; i < 32; i++)
            {
                float a0 = i * Mathf.PI * 2f / 32, a1 = (i + 1) * Mathf.PI * 2f / 32;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r);
            }
        }
    }
}
