using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public enum ControlMode
    {
        [InspectorName("탱크식 - ←→ 회전, ↑ 전진 (기획서)")] Tank,
        [InspectorName("방향식 - 누른 방향으로 머리를 돌리며 전진")] Direction
    }

    [Header("조작 방식")]
    [SerializeField] private ControlMode controlMode = ControlMode.Tank;
    [Tooltip("켜면 키를 누르지 않아도 머리 방향으로 계속 전진")]
    [SerializeField] private bool autoForward = true;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float slowSpeed = 2.5f;

    [Header("회전")]
    [Tooltip("초당 회전 각도")]
    [SerializeField] private float turnSpeed = 300f;
    [Tooltip("감속(Shift) 중 회전 각도. 느리게 움직이는 대신 더 빨리 돎")]
    [SerializeField] private float slowTurnSpeed = 450f;
    [Tooltip("탱크식에서 멈춰 있을 때 회전 배율 (제자리 회전을 더 빠르게)")]
    [SerializeField] private float idleTurnMultiplier = 1.3f;
    [Tooltip("회전 키를 누른 뒤 최고 회전 속도까지 걸리는 시간(초). 짧게 톡 누르면 조금만 돎 (0 = 즉시)")]
    [SerializeField] private float turnResponse = 0.12f;

    [Header("화면")]
    [Tooltip("켜면 화면 끝으로 나가면 반대쪽에서 나옴. 끄면 화면 끝에서 멈춤")]
    [SerializeField] private bool wrapScreen = true;
    [Tooltip("켜짐: 화면 밖으로 이만큼 나가야 반대쪽으로 넘어감 / 꺼짐: 화면 끝에서 안쪽으로 남길 여백")]
    [SerializeField] private float screenMargin = 0.5f;

    [Header("보스전")]
    [Tooltip("보스전 중 올라갈 수 있는 최대 높이 = 보스 중심 높이 - 이 값 (0 = 보스 중심 높이까지)")]
    [SerializeField] private float bossCeilingGap = 0f;

    [Header("조작 키")]
    [SerializeField] private KeyCode slowKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode bombKey = KeyCode.Q;
    [SerializeField] private KeyCode dashHotKey = KeyCode.E;

    [Header("태그")]
    [Tooltip("폭탄이 지우는 탄, 운석 같은 방해물에 붙이는 태그")]
    [SerializeField] private string enemyBulletTag = "EnemyBullet";

    [Header("HP")]
    [SerializeField] private int maxHp = 5;
    [Tooltip("미사일이 아닌 EnemyBullet 태그 물체(운석 등)에 맞았을 때 깎이는 양")]
    [SerializeField] private int damagePerHit = 1;

    [Header("폭탄 (쿨타임 방식)")]
    [SerializeField] private float bombCooldown = 10f;
    [Tooltip("폭탄을 쓸 때 번쩍이는 화면 전체 흰색 이미지 (Raycast Target 끄기)")]
    [SerializeField] private Image bombFlash;
    [Tooltip("번쩍일 때 최대 밝기 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.8f;
    [Tooltip("밝아졌다가 사라지는 시간(초)")]
    [SerializeField] private float flashTime = 0.5f;

    [Header("적 밀어내기")]
    [SerializeField] private float enemyKnockbackDistance = 2f;
    [SerializeField] private float enemyKnockbackDuration = 0.2f;

    [Header("무적 (피격 후)")]
    [SerializeField] private float invincibleTime = 1.5f;
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("게임오버")]
    [Tooltip("켜면 죽은 뒤 같은 스테이지를 다시 시작 (돈·파츠는 유지)")]
    [SerializeField] private bool restartOnDeath = true;
    [SerializeField] private float restartDelay = 2f;
    [SerializeField] private UnityEvent onDeath;

    private Camera mainCam;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;

    // 패시브 계산을 위한 기본 스탯 백업
    private int baseMaxHp;
    private float baseMoveSpeed;
    private float baseSlowSpeed;
    private float baseTurnSpeed;
    private float baseSlowTurnSpeed;
    private float baseBombCooldown;
    private float baseInvincibleTime;
    private bool passiveMaxHpGranted = false;
    private int activeBonusMaxHp = 0;

    private int currentHp;
    private int stageStartScore;
    private float invincibleUntil;
    private Coroutine blinkRoutine;
    private Coroutine flashRoutine;
    private bool isMoving;
    private bool isDead;
    private bool isDefenseUp;
    private float turnVelocity;
    private Vector2 bounceVelocity;
    private float bounceTimer;

    // 대시 상태 변수
    private Vector2 dashVelocity;
    private float dashTimer;

    private float bombTimer = 0f;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    // HUD 참조용 프로퍼티 (폭탄 쿨타임)
    public float BombCooldownRatio => bombCooldown > 0f ? Mathf.Clamp01(bombTimer / bombCooldown) : 0f;
    public bool IsBombReady => bombTimer <= 0f;
    public float BombRemaining => bombTimer;

    public bool IsDead => isDead;
    public bool IsInvincible => CheatInvincible || Time.time < invincibleUntil;
    public bool CheatInvincible { get; set; }

    private void Awake()
    {
        mainCam = Camera.main;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponentInChildren<Collider2D>();

        baseMaxHp = maxHp;
        baseMoveSpeed = moveSpeed;
        baseSlowSpeed = slowSpeed;
        baseTurnSpeed = turnSpeed;
        baseSlowTurnSpeed = slowTurnSpeed;
        baseBombCooldown = bombCooldown;
        baseInvincibleTime = invincibleTime;

        currentHp = maxHp;
        Enemy.KillCount = 0;
    }

    private void Start()
    {
        SetFlashAlpha(0f);

        if (DataManager.Instance != null)
        {
            stageStartScore = DataManager.Instance.currentScore;
        }

        // 게임 시작 및 스테이지 진입 시 보유 중인 패시브 파츠 효과 즉시 적용
        ApplyPassiveUpgrades();

        if (col == null)
        {
            Debug.LogWarning("[Player] Collider 2D가 없어요. Add Component → Circle Collider 2D를 붙여 주세요.");
        }

        if (GetComponentInChildren<Collider>() != null)
        {
            Debug.LogWarning("[Player] 3D Collider가 붙어 있어요. 이름에 2D가 들어간 Collider로 바꿔 주세요.");
        }
    }

    // 상점에서 패시브 파츠를 구매하거나 스테이지가 시작될 때 호출됩니다.
    public void ApplyPassiveUpgrades()
    {
        DataManager data = DataManager.Instance;
        if (data == null) return;

        // 1. 패시브: 복합 장갑 증설 (최대 HP +3)
        bool hasPassiveHp = data.HasPart(PartType.PassiveMaxHp);
        int targetMaxHp = baseMaxHp + (hasPassiveHp ? 3 : 0) + activeBonusMaxHp;
        if (hasPassiveHp && !passiveMaxHpGranted)
        {
            passiveMaxHpGranted = true;
            maxHp = targetMaxHp;
            currentHp = Mathf.Min(maxHp, currentHp + 3);
        }
        else
        {
            maxHp = targetMaxHp;
            currentHp = Mathf.Min(maxHp, currentHp);
        }

        // 2. 패시브: 고출력 추진기 (이동 및 회전 속도 +25%)
        float speedMul = data.HasPart(PartType.PassiveMoveSpeed) ? 1.25f : 1f;
        moveSpeed = baseMoveSpeed * speedMul;
        slowSpeed = baseSlowSpeed * speedMul;
        turnSpeed = baseTurnSpeed * speedMul;
        slowTurnSpeed = baseSlowTurnSpeed * speedMul;

        // 3. 패시브: 급속 냉각 코어 (폭탄 쿨타임 -25%)
        bombCooldown = data.HasPart(PartType.PassiveCooldown) ? baseBombCooldown * 0.75f : baseBombCooldown;

        // 4. 패시브: 비상 방어 필드 (피격 무적 시간 +1초)
        invincibleTime = data.HasPart(PartType.PassiveInvincible) ? baseInvincibleTime + 1.0f : baseInvincibleTime;
    }

    private void Update()
    {
        if (isDead) return;

        HandleMovement();

        if (bombTimer > 0f)
        {
            bombTimer -= Time.deltaTime;
            if (bombTimer < 0f) bombTimer = 0f;
        }

        if (Input.GetKeyDown(bombKey) && IsBombReady)
        {
            UseBomb();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            UseSkill(0);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            UseSkill(1);
        }

        // '부스터 대시' 파츠를 보유 중이면 E키로도 즉시 대시 발동 가능
        if (Input.GetKeyDown(dashHotKey) && DataManager.Instance != null && DataManager.Instance.HasPart(PartType.Dash))
        {
            Item item = GetComponent<Item>();
            if (item != null) item.ExecuteSkill(PartType.Dash);
        }
    }

    private void HandleMovement()
    {
        bool isSlow = Input.GetKey(slowKey);
        float speed = isSlow ? slowSpeed : moveSpeed;
        float turnRate = isSlow ? slowTurnSpeed : turnSpeed;
        float forward;

        if (controlMode == ControlMode.Tank)
        {
            float turn = Input.GetAxisRaw("Horizontal");
            forward = autoForward ? 1f : Mathf.Max(0f, Input.GetAxisRaw("Vertical"));

            if (forward == 0f) turnRate *= idleTurnMultiplier;

            float targetTurn = -turn * turnRate;
            if (turn == 0f || targetTurn * turnVelocity < 0f) turnVelocity = 0f;

            turnVelocity = turnResponse > 0f
                ? Mathf.MoveTowards(turnVelocity, targetTurn, turnRate / turnResponse * Time.deltaTime)
                : targetTurn;

            transform.Rotate(0f, 0f, turnVelocity * Time.deltaTime);
        }
        else
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool hasInput = input.sqrMagnitude > 0.01f;
            forward = hasInput || autoForward ? 1f : 0f;

            if (hasInput)
            {
                float targetAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg - 90f;
                float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnRate * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        isMoving = forward > 0f;

        Vector3 newPos = transform.position + transform.up * (forward * speed * Time.deltaTime);

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            newPos += (Vector3)(dashVelocity * Time.deltaTime);
        }

        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.deltaTime;
            newPos += (Vector3)(bounceVelocity * Time.deltaTime);
        }

        Vector2 edgeMin = mainCam.ViewportToWorldPoint(Vector3.zero);
        Vector2 edgeMax = mainCam.ViewportToWorldPoint(Vector3.one);

        if (wrapScreen)
        {
            Vector2 min = edgeMin - Vector2.one * screenMargin;
            Vector2 max = edgeMax + Vector2.one * screenMargin;

            if (newPos.x > max.x) newPos.x = min.x;
            else if (newPos.x < min.x) newPos.x = max.x;

            if (newPos.y > max.y) newPos.y = min.y;
            else if (newPos.y < min.y) newPos.y = max.y;
        }
        else
        {
            Vector2 min = edgeMin + Vector2.one * screenMargin;
            Vector2 max = edgeMax - Vector2.one * screenMargin;

            newPos.x = Mathf.Clamp(newPos.x, min.x, max.x);
            newPos.y = Mathf.Clamp(newPos.y, min.y, max.y);
        }

        if (Boss.Active != null)
        {
            float ceiling = Boss.Active.transform.position.y - bossCeilingGap;
            if (newPos.y > ceiling) newPos.y = ceiling;
        }

        transform.position = newPos;
    }

    // [신규 액티브] 부스터 대시 실행
    public void PerformDash(float distance, float duration, float invincibleDuration)
    {
        if (isDead) return;

        float safeDuration = Mathf.Max(duration, 0.05f);
        dashVelocity = (Vector2)transform.up * (distance / safeDuration);
        dashTimer = safeDuration;

        // 대시 중에는 깜빡임 없이 무적 판정 부여 (잔상과 어우러지도록)
        MakeInvincible(invincibleDuration, false);
    }

    // [신규 액티브] 나노 HP 증강 실행 (최대 HP 확장 + 즉시 회복)
    public void ActivateHpBoostSkill(int maxHpBonus, int healAmount)
    {
        if (isDead) return;

        if (activeBonusMaxHp < 5)
        {
            activeBonusMaxHp += Mathf.Max(0, maxHpBonus);
        }
        ApplyPassiveUpgrades();
        currentHp = Mathf.Min(maxHp, currentHp + Mathf.Max(1, healAmount));
        Debug.Log($"[나노 HP 증강] 현재 HP: {currentHp}/{maxHp}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead || !other.CompareTag(enemyBulletTag)) return;
        if (other.GetComponentInParent<EnemyBullet>() != null) return;

        Destroy(other.gameObject);
        TakeDamage(damagePerHit);
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        SoundManager.Play(SoundManager.Sfx.ItemPickup);
        Debug.Log($"[아이템] 생명력 +{amount} 회복! 현재 HP: {currentHp}/{maxHp}");
    }

    public void ActivateInvincibleItem(float duration)
    {
        MakeInvincible(duration, true);
        SoundManager.Play(SoundManager.Sfx.ItemPickup);
        Debug.Log($"[아이템] {duration}초 동안 무적 효과 발동!");
    }

    public void ActivateDefenseUpItem(float duration)
    {
        SoundManager.Play(SoundManager.Sfx.ItemPickup);
        StartCoroutine(DefenseUpRoutine(duration));
    }

    private IEnumerator DefenseUpRoutine(float duration)
    {
        isDefenseUp = true;
        Debug.Log($"[아이템] {duration}초 동안 방어력 강화 시작!");
        yield return new WaitForSeconds(duration);
        isDefenseUp = false;
        Debug.Log("[아이템] 방어력 강화 종료!");
    }

    public void Bounce(Vector2 dir, float distance, float duration)
    {
        if (isDead) return;

        bounceVelocity = dir.normalized * (distance / Mathf.Max(duration, 0.01f));
        bounceTimer = duration;

        MakeInvincible(invincibleTime, false);
    }

    public void PushEnemy(Enemy enemy)
    {
        if (isDead) return;

        Vector2 toEnemy = enemy.transform.position - transform.position;
        bool inFront = Vector2.Dot(transform.up, toEnemy) > 0f;
        Vector2 dir = isMoving && inFront ? (Vector2)transform.up : toEnemy;

        enemy.ApplyKnockback(dir, enemyKnockbackDistance, enemyKnockbackDuration);
    }

    public void TakeDamage(int damage)
    {
        if (isDead || IsInvincible) return;

        int finalDamage = isDefenseUp ? Mathf.Max(1, damage / 2) : damage;
        currentHp = Mathf.Max(0, currentHp - finalDamage);

        SoundManager.Play(SoundManager.Sfx.PlayerHit);
        Debug.Log($"피격! HP: {currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        MakeInvincible(invincibleTime, true);
    }

    private void Die()
    {
        isDead = true;
        SoundManager.Play(SoundManager.Sfx.PlayerDeath);

        StopAllCoroutines();
        SetFlashAlpha(0f);

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (col != null) col.enabled = false;

        if (DataManager.Instance != null)
        {
            DataManager.Instance.currentScore = stageStartScore;
        }

        Debug.Log("플레이어 사망 - 선택창 호출");
        onDeath.Invoke();

        if (GameSystemExtension.Instance != null)
        {
            GameSystemExtension.Instance.OpenDeathMenu();
        }
        else if (restartOnDeath)
        {
            SoundManager.Play(SoundManager.Sfx.GameOver);
            Invoke(nameof(Restart), restartDelay);
        }
    }

    private void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void MakeInvincible(float duration, bool blink)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);

        if (!blink) return;

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(BlinkRoutine(duration));
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        float end = Time.time + duration;

        while (Time.time < end)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;

            yield return new WaitForSeconds(blinkInterval);
        }

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        blinkRoutine = null;
    }

    private void UseBomb()
    {
        bombTimer = bombCooldown;
        SoundManager.Play(SoundManager.Sfx.Bomb);

        foreach (GameObject bullet in GameObject.FindGameObjectsWithTag(enemyBulletTag))
        {
            if (IsVisibleOnCamera(bullet.transform.position))
            {
                Destroy(bullet);
            }
        }

        if (bombFlash != null)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private void UseSkill(int slotIndex)
    {
        if (DataManager.Instance == null) return;

        PartType skill = DataManager.Instance.quickSlots[slotIndex];
        if (skill == PartType.None) return;

        Item item = GetComponent<Item>();
        if (item != null)
        {
            item.ExecuteSkill(skill);
        }
    }

    private IEnumerator FlashRoutine()
    {
        for (float t = 0f; t < flashTime; t += Time.unscaledDeltaTime)
        {
            float remain = 1f - t / flashTime;
            SetFlashAlpha(flashAlpha * remain * remain);
            yield return null;
        }

        SetFlashAlpha(0f);
        flashRoutine = null;
    }

    private void SetFlashAlpha(float alpha)
    {
        if (bombFlash == null) return;

        Color c = bombFlash.color;
        c.a = alpha;
        bombFlash.color = c;
    }

    private bool IsVisibleOnCamera(Vector3 worldPos)
    {
        Vector3 viewPos = mainCam.WorldToViewportPoint(worldPos);

        return viewPos.z > 0f &&
               viewPos.x >= 0f && viewPos.x <= 1f &&
               viewPos.y >= 0f && viewPos.y <= 1f;
    }
}