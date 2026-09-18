using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Hero
{
    /// <summary>
    /// 절벽 위 오버더숄더 부감 시점과 영웅 뒤를 따르는 3인칭 추적 시점을 상태에 따라 전환한다.
    /// 3인칭 시점은 마우스 이동으로만 회전, 이동키는 시점에 영향 없음.
    /// 두 시점 사이는 위치는 SmoothDamp, 회전은 지수 감쇠 Slerp로 부드럽게 보간된다.
    /// </summary>
    public sealed class HeroCamera : MonoBehaviour
    {
        [Header("참조")]
        public Hero hero;
        public Transform perchView;

        [Header("3인칭")]
        public float followDistance = 5f;
        public float lookHeight = 1.2f;

        [Header("마우스 룩")]
        public float mouseSensitivity = 0.12f;   // 픽셀당 도
        public float pitchMin = -15f;
        public float pitchMax = 60f;
        public float initialPitch = 20f;

        [Header("전환")]
        public float smoothTime = 0.35f;

        public float Yaw => _yaw;

        Vector3 _velocity;
        float _yaw;
        float _pitch;
        bool _subscribed;

        void Awake()
        {
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", mouseSensitivity); // 설정 메뉴 값
            if (hero == null)
            {
                hero = FindFirstObjectByType<Hero>();
            }
        }

        void OnEnable()
        {
            TrySubscribe();
        }

        void OnDisable()
        {
            if (hero != null)
            {
                hero.StateChanged -= OnHeroStateChanged;
                _subscribed = false;
            }
        }

        void Start()
        {
            if (hero == null)
            {
                hero = FindFirstObjectByType<Hero>();
            }

            TrySubscribe();
        }

        void TrySubscribe()
        {
            if (_subscribed || hero == null)
            {
                return;
            }

            hero.StateChanged += OnHeroStateChanged;
            _subscribed = true;
            OnHeroStateChanged(hero.State);
        }

        void OnHeroStateChanged(HeroState state)
        {
            if (state == HeroState.Active)
            {
                _yaw = hero.transform.eulerAngles.y;
                _pitch = initialPitch;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SnapToPerch()
        {
            if (perchView == null)
            {
                return;
            }

            transform.SetPositionAndRotation(perchView.position, perchView.rotation);
            _velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            if (hero == null || perchView == null)
            {
                return;
            }

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (hero.State == HeroState.Active)
            {
                if (Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    _yaw += delta.x * mouseSensitivity;
                    _pitch -= delta.y * mouseSensitivity;
                    _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
                }

                Vector3 pivot = hero.transform.position + Vector3.up * lookHeight;
                targetPosition = pivot + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * followDistance;

                Vector3 lookDir = pivot - targetPosition;
                targetRotation = lookDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(lookDir)
                    : transform.rotation;

                targetPosition = ClampAboveGround(targetPosition);
            }
            else
            {
                targetPosition = perchView.position;
                targetRotation = perchView.rotation;
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, smoothTime);

            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(smoothTime, 0.0001f));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        static Vector3 ClampAboveGround(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
            {
                float minY = hit.point.y + 0.5f;
                if (position.y < minY)
                {
                    position.y = minY;
                }
            }

            return position;
        }
    }
}
