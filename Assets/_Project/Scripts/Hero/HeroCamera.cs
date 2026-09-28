using TowerDefense.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense.Hero
{
    /// <summary>
    /// 절벽 위 오버더숄더 부감 시점과 영웅 뒤를 따르는 3인칭 추적 시점을 상태에 따라 전환한다.
    /// 3인칭 시점은 마우스 이동으로만 회전, 이동키는 시점에 영향 없음.
    /// 두 시점 사이는 위치는 SmoothDamp, 회전은 지수 감쇠 Slerp로 부드럽게 보간된다.
    /// 3인칭에서 영웅과 카메라 사이를 포탑·지형이 막으면 카메라를 막힌 곳 앞으로 바로 당기고, 풀리면 천천히 되돌린다.
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

        [Header("가림 처리")]
        public float collisionRadius = 0.35f;  // 카메라를 공으로 보고 이만큼 떨어뜨린다 (근평면보다 크게)
        public float minDistance = 0.8f;
        public float returnSpeed = 6f;         // 가림이 풀린 뒤 되돌아가는 속도 (m/s)

        [Header("전환")]
        public float smoothTime = 0.35f;

        [Header("강림 착지 흔들림")]
        public float shakeAmp = 0.6f;
        public float shakeSec = 0.35f;

        public float Yaw => _yaw;

        Vector3 _velocity;
        Vector3 _pos;       // 흔들림을 뺀 카메라 위치 (보간은 이 값으로)
        float _shakeUntil;
        float _yaw;
        float _pitch;
        float _camDist = float.MaxValue; // 가림 때문에 줄어든 영웅~카메라 거리
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
                _shakeUntil = Time.time + shakeSec; // 운석 착지
                _yaw = hero.transform.eulerAngles.y;
                _pitch = initialPitch;
                _camDist = float.MaxValue;
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
            _pos = perchView.position;
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
            Vector3 pivot = Vector3.zero;

            if (hero.State == HeroState.Active)
            {
                // 스테이지가 끝나면(클리어·실패) 정산 창 버튼을 누를 수 있게 커서를 풀고 시점 회전을 멈춘다
                bool over = hero.session != null && hero.session.Over;
                if (over && Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (!over && Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    _yaw += delta.x * mouseSensitivity;
                    _pitch -= delta.y * mouseSensitivity;
                    _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
                }

                pivot = hero.transform.position + Vector3.up * lookHeight;
                targetPosition = pivot + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * followDistance;

                Vector3 lookDir = pivot - targetPosition;
                targetRotation = lookDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(lookDir)
                    : transform.rotation;

                targetPosition = ClampAboveGround(targetPosition);
            }
            else if (hero.State == HeroState.Descending)
            {
                // 운석 강림: 절벽 자리에서 영웅을 눈으로 좇는다 (솟구쳤다 내리꽂히는 모습이 화면 밖으로 나가지 않게)
                targetPosition = perchView.position;
                Vector3 toHero = hero.transform.position - transform.position;
                if (toHero.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toHero), 1f - Mathf.Exp(-12f * Time.deltaTime));
                targetRotation = transform.rotation;
            }
            else
            {
                targetPosition = perchView.position;
                targetRotation = perchView.rotation;
            }

            if (_pos == Vector3.zero) _pos = transform.position;
            _pos = Vector3.SmoothDamp(_pos, targetPosition, ref _velocity, smoothTime);
            // 가림은 보간이 끝난 위치에 건다: 보간을 기다리면 그동안 포탑 안이 보인다
            Vector3 pos = hero.State == HeroState.Active ? AvoidOcclusion(pivot, _pos) : _pos;
            float k = Mathf.Clamp01((_shakeUntil - Time.time) / Mathf.Max(0.0001f, shakeSec));
            transform.position = pos + Random.insideUnitSphere * (shakeAmp * k * k);

            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(smoothTime, 0.0001f));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        /// <summary>영웅 머리(pivot)에서 카메라 쪽으로 막히지 않는 곳까지만 둔다. 막히면 바로 당기고, 풀리면 returnSpeed로 되돌린다.</summary>
        Vector3 AvoidOcclusion(Vector3 pivot, Vector3 desired)
        {
            Vector3 offset = desired - pivot;
            float want = offset.magnitude;
            if (want < 0.0001f)
            {
                return desired;
            }

            Vector3 dir = offset / want;
            float free = Mathf.Max(minDistance, FreeDistance(pivot, dir, want));
            _camDist = free < _camDist ? free : Mathf.Min(free, _camDist + returnSpeed * Time.deltaTime);
            return pivot + dir * Mathf.Min(want, _camDist);
        }

        float FreeDistance(Vector3 pivot, Vector3 dir, float maxDist)
        {
            float free = maxDist;

            // 지형·절벽 (콜라이더가 있는 것)
            if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, maxDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                free = hit.distance;
            }

            // 포탑은 콜라이더가 없으므로 몸체 경계를 세운 원기둥으로 본다
            foreach (Tower tower in Tower.All)
            {
                Bounds b = tower.Body;
                float radius = Mathf.Max(b.extents.x, b.extents.z) + collisionRadius;
                float d = RayCylinder(pivot, dir, b.center, radius, b.min.y - collisionRadius, b.max.y + collisionRadius);
                if (d < free)
                {
                    free = d;
                }
            }

            return free;
        }

        /// <summary>세운 원기둥 옆면에 광선이 처음 닿는 거리. 안 닿거나 시작점이 이미 안이면 무한대.</summary>
        static float RayCylinder(Vector3 origin, Vector3 dir, Vector3 center, float radius, float minY, float maxY)
        {
            float ox = origin.x - center.x;
            float oz = origin.z - center.z;
            float c = ox * ox + oz * oz - radius * radius;
            if (c <= 0f)
            {
                return float.PositiveInfinity; // 영웅이 포탑에 붙어 섰다: 피할 곳이 없으니 두지 않는다
            }

            float a = dir.x * dir.x + dir.z * dir.z;
            float bHalf = ox * dir.x + oz * dir.z;
            if (a < 0.000001f || bHalf >= 0f)
            {
                return float.PositiveInfinity; // 수직이거나 멀어지는 방향
            }

            float disc = bHalf * bHalf - a * c;
            if (disc < 0f)
            {
                return float.PositiveInfinity;
            }

            float t = (-bHalf - Mathf.Sqrt(disc)) / a;
            float y = origin.y + dir.y * t;
            return y >= minY && y <= maxY ? t : float.PositiveInfinity;
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
