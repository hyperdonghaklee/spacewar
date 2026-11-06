using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("스킬 기본 설정")]
    public float skillCooldown = 5f;
    private Dictionary<PartType, float> cooldownEnd = new Dictionary<PartType, float>();
    private Dictionary<PartType, float> lastCooldownDuration = new Dictionary<PartType, float>();

    [Header("스킬 세부 파라미터")]
    [Tooltip("시간 정지 지속 시간")]
    public float timeStopDuration = 3f;
    [Tooltip("감속장 범위 및 비율")]
    public float slowRadius = 5f;
    public float slowFactor = 0.2f;
    public float slowDuration = 4f;
    [Tooltip("반사/블랙홀 반응 범위")]
    public float reflectRadius = 4f;
    public float blackHoleRadius = 6f;
    public float blackHoleDuration = 3f;

    [Header("위치 스왑")]
    [Tooltip("스왑 당한 적이 멈춰 있는 시간(초)")]
    public float swapStunTime = 1f;
    [Tooltip("스왑 직후 플레이어 무적 시간(초)")]
    public float swapInvincibleTime = 0.6f;

    [Header("충격파")]
    public float shockwaveRadius = 4f;
    [Tooltip("적이 밀려나는 거리")]
    public float shockwaveKnockback = 3f;
    public float shockwaveKnockbackDuration = 0.25f;

    [Header("신규 액티브 1: 부스터 대시 (Dash)")]
    [Tooltip("대시 이동 거리")]
    public float dashDistance = 3.8f;
    [Tooltip("대시 이동 시간(초)")]
    public float dashDuration = 0.22f;
    [Tooltip("대시 무적 시간(초)")]
    public float dashInvincibleTime = 0.45f;
    [Tooltip("대시 기본 쿨타임(초)")]
    public float dashCooldown = 3.5f;

    [Header("신규 액티브 2: 나노 HP 증강 (HpBoostActive)")]
    [Tooltip("사용 시 영구히 늘어나는 최대 HP 양 (스테이지당 최대 +5 제한)")]
    public int activeMaxHpBonus = 1;
    [Tooltip("사용 시 즉시 회복되는 HP 양")]
    public int activeHealAmount = 2;
    [Tooltip("HP 증강 기본 쿨타임(초)")]
    public float hpBoostCooldown = 12f;

    [Header("신규 액티브 3: 에너지 실드 (EnergyShield)")]
    [Tooltip("실드 지속 시간(초)")]
    public float energyShieldDuration = 3.5f;
    [Tooltip("실드 반경")]
    public float energyShieldRadius = 1.8f;
    [Tooltip("에너지 실드 기본 쿨타임(초)")]
    public float energyShieldCooldown = 8f;

    // 패시브 '급속 냉각 코어(PassiveCooldown)' 보유 시 쿨타임 25% 감소 적용
    public float GetSkillMaxCooldown(PartType skill)
    {
        float baseCd = skillCooldown;
        if (skill == PartType.Dash) baseCd = dashCooldown;
        else if (skill == PartType.HpBoostActive) baseCd = hpBoostCooldown;
        else if (skill == PartType.EnergyShield) baseCd = energyShieldCooldown;

        if (DataManager.Instance != null && DataManager.Instance.HasPart(PartType.PassiveCooldown))
        {
            baseCd *= 0.75f;
        }
        return Mathf.Max(0.5f, baseCd);
    }

    public float Remaining(PartType skill)
    {
        return cooldownEnd.TryGetValue(skill, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }

    public float CooldownRatio(PartType skill)
    {
        float remain = Remaining(skill);
        if (remain <= 0f) return 0f;
        float total = lastCooldownDuration.TryGetValue(skill, out float dur) && dur > 0f ? dur : GetSkillMaxCooldown(skill);
        return Mathf.Clamp01(remain / total);
    }

    // 퀵슬롯 및 전용 키 스킬 실행 (Player.cs에서 호출)
    public bool ExecuteSkill(PartType skill)
    {
        if (skill == PartType.None || DataManager.IsPassivePart(skill)) return false;

        if (cooldownEnd.TryGetValue(skill, out float end) && Time.time < end)
        {
            SoundManager.Play(SoundManager.Sfx.Deny);
            Debug.Log($"{skill} 재사용 대기 중 (남은 시간: {end - Time.time:F1}초)");
            return false;
        }

        bool success = false;
        switch (skill)
        {
            case PartType.ForceHoming: success = ExecuteForcedGuidance(); break;
            case PartType.TimeStop: success = ExecuteTimeStop(); break;
            case PartType.Reflect: success = ExecuteReflection(); break;
            case PartType.BlackHole: success = ExecuteBlackHole(); break;
            case PartType.SlowField: success = ExecuteSlowField(); break;
            case PartType.PositionSwap: success = ExecutePositionSwap(); break;
            case PartType.Shockwave: success = ExecuteShockwave(); break;
            case PartType.Dash: success = ExecuteDash(); break;
            case PartType.HpBoostActive: success = ExecuteHpBoostActive(); break;
            case PartType.EnergyShield: success = ExecuteEnergyShield(); break;
        }

        if (success)
        {
            float cd = GetSkillMaxCooldown(skill);
            lastCooldownDuration[skill] = cd;
            cooldownEnd[skill] = Time.time + cd;

            if ((int)skill <= 7)
            {
                SoundManager.PlaySkill((int)skill);
            }
            else
            {
                SoundManager.Play(SoundManager.Sfx.ItemPickup);
            }
            Debug.Log($"[스킬 발동 성공] {skill} (쿨타임 {cd:F1}초)");
        }
        return success;
    }

    #region 액티브 스킬 실질 기능 구현

    // 1. 강제 유도
    private bool ExecuteForcedGuidance()
    {
        Transform targetTransform = FindFacingEnemy();
        if (targetTransform == null) return false;

        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in bullets)
        {
            Vector2 dir = (targetTransform.position - bullet.transform.position).normalized;
            bullet.isOverriddenBySkill = true;

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = dir * 12f;
                bullet.transform.up = dir;
            }
        }
        return true;
    }

    private Transform FindFacingEnemy()
    {
        Transform best = null;
        float bestScore = float.MinValue;

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            Vector2 to = enemy.transform.position - transform.position;
            float dist = Mathf.Max(to.magnitude, 0.01f);
            float score = Vector2.Dot(transform.up, to / dist) * 10f - dist;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy.transform;
            }
        }
        return best;
    }

    // 2. 시간 정지
    private bool ExecuteTimeStop()
    {
        StartCoroutine(TimeStopRoutine(timeStopDuration));
        return true;
    }

    private IEnumerator TimeStopRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
            foreach (var bullet in bullets)
            {
                bullet.isOverriddenBySkill = true;
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            yield return null;
        }

        EnemyBullet[] remainBullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var bullet in remainBullets)
        {
            bullet.isOverriddenBySkill = false;
        }
    }

    // 3. 공격 반사
    private bool ExecuteReflection()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, reflectRadius);
        int reflectedCount = 0;

        foreach (var col in cols)
        {
            EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
            if (bullet != null)
            {
                bullet.isOverriddenBySkill = true;
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = -rb.linearVelocity * 1.5f;
                    bullet.transform.up = -bullet.transform.up;
                    reflectedCount++;
                }
            }
        }
        return reflectedCount > 0;
    }

    // 4. 블랙홀
    private bool ExecuteBlackHole()
    {
        StartCoroutine(BlackHoleRoutine(transform.position, blackHoleDuration));
        return true;
    }

    private IEnumerator BlackHoleRoutine(Vector3 center, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            Collider2D[] cols = Physics2D.OverlapCircleAll(center, blackHoleRadius);

            foreach (var col in cols)
            {
                EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
                if (bullet != null)
                {
                    bullet.isOverriddenBySkill = true;
                    Vector2 pullDir = (center - bullet.transform.position);

                    if (pullDir.magnitude < 0.8f)
                    {
                        Destroy(bullet.gameObject);
                    }
                    else
                    {
                        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.linearVelocity = pullDir.normalized * 8f;
                    }
                }
            }
            yield return null;
        }
    }

    // 5. 감속장
    private bool ExecuteSlowField()
    {
        StartCoroutine(SlowFieldRoutine(slowDuration));
        return true;
    }

    private IEnumerator SlowFieldRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, slowRadius);

            foreach (var col in cols)
            {
                EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
                if (bullet != null)
                {
                    bullet.isOverriddenBySkill = true;
                    Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = rb.linearVelocity.normalized * (2f * slowFactor);
                }
            }
            yield return null;
        }
    }

    // 6. 위치 스왑
    private bool ExecutePositionSwap()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Enemy nearest = null;
        float best = float.MaxValue;

        foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            Vector3 v = cam.WorldToViewportPoint(e.transform.position);
            if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;

            float d = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = e;
            }
        }

        if (nearest == null) return false;

        Vector3 playerPos = transform.position;
        Vector3 enemyPos = nearest.transform.position;

        transform.position = new Vector3(enemyPos.x, enemyPos.y, playerPos.z);

        Vector3 newEnemyPos = new Vector3(playerPos.x, playerPos.y, enemyPos.z);
        nearest.transform.position = newEnemyPos;
        Rigidbody2D erb = nearest.GetComponent<Rigidbody2D>();
        if (erb != null)
        {
            erb.position = newEnemyPos;
            erb.linearVelocity = Vector2.zero;
        }

        nearest.ApplyKnockback(Vector2.up, 0f, swapStunTime);

        Player player = GetComponent<Player>();
        if (player != null) player.ActivateInvincibleItem(swapInvincibleTime);

        return true;
    }

    // 7. 충격파
    private bool ExecuteShockwave()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius);
        HashSet<Object> handled = new HashSet<Object>();
        int affected = 0;

        foreach (var col in cols)
        {
            EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
            if (bullet != null)
            {
                if (handled.Add(bullet))
                {
                    Destroy(bullet.gameObject);
                    affected++;
                }
                continue;
            }

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && handled.Add(enemy))
            {
                Vector2 dir = enemy.transform.position - transform.position;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
                enemy.ApplyKnockback(dir, shockwaveKnockback, shockwaveKnockbackDuration);
                affected++;
            }
        }

        StartCoroutine(RingEffectRoutine(transform.position, shockwaveRadius, new Color(0.5f, 0.9f, 1f, 1f), 0.3f));
        return affected > 0;
    }

    // 8. [신규 액티브] 부스터 대시: 바라보는 방향으로 순간 돌진 + 무적 + 대시 충격파
    private bool ExecuteDash()
    {
        Player player = GetComponent<Player>();
        if (player == null) return false;

        Vector3 startPos = transform.position;
        player.PerformDash(dashDistance, dashDuration, dashInvincibleTime);
        StartCoroutine(RingEffectRoutine(startPos, 1.6f, new Color(0f, 1f, 0.85f, 1f), 0.25f));
        return true;
    }

    // 9. [신규 액티브] 나노 HP 증강: 최대 HP +1 확장 및 현재 HP +2 즉시 회복
    private bool ExecuteHpBoostActive()
    {
        Player player = GetComponent<Player>();
        if (player == null) return false;

        player.ActivateHpBoostSkill(activeMaxHpBonus, activeHealAmount);
        StartCoroutine(RingEffectRoutine(transform.position, 2.2f, new Color(0.2f, 1f, 0.4f, 1f), 0.4f));
        return true;
    }

    // 10. [신규 액티브] 에너지 실드: 플레이어를 따라다니는 보호막이 적 탄막을 지우고 무적 부여
    private bool ExecuteEnergyShield()
    {
        Player player = GetComponent<Player>();
        if (player != null)
        {
            player.ActivateInvincibleItem(energyShieldDuration);
        }
        StartCoroutine(EnergyShieldRoutine(energyShieldDuration));
        return true;
    }

    private IEnumerator EnergyShieldRoutine(float duration)
    {
        GameObject go = new GameObject("EnergyShieldRing");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.startWidth = 0.09f;
        lr.endWidth = 0.09f;
        lr.sortingOrder = 100;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lr.material = new Material(shader);

        const int segments = 36;
        lr.positionCount = segments;

        float timer = 0f;
        while (timer < duration && this != null)
        {
            timer += Time.deltaTime;
            Vector3 center = transform.position;

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f + timer * 4f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * energyShieldRadius);
            }

            Color c = new Color(0.3f, 0.85f, 1f, Mathf.Lerp(0.9f, 0.3f, timer / duration));
            lr.startColor = c;
            lr.endColor = c;

            // 보호막 반경 내 적 탄막 제거
            Collider2D[] cols = Physics2D.OverlapCircleAll(center, energyShieldRadius);
            foreach (var col in cols)
            {
                EnemyBullet bullet = col.GetComponentInParent<EnemyBullet>();
                if (bullet != null) Destroy(bullet.gameObject);
            }

            yield return null;
        }

        if (go != null) Destroy(go);
    }

    private IEnumerator RingEffectRoutine(Vector3 center, float radius, Color baseColor, float duration)
    {
        GameObject go = new GameObject("SkillEffectRing");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.sortingOrder = 100;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lr.material = new Material(shader);

        const int segments = 40;
        lr.positionCount = segments;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float r = Mathf.Lerp(0.25f, radius, p);

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
            }

            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - p);
            lr.startColor = c;
            lr.endColor = c;
            yield return null;
        }

        Destroy(go);
    }
    #endregion

    public void HitEnemy(GameObject enemy, float reverseHitDamage)
    {
        Boss boss = enemy.GetComponent<Boss>();
        if (boss != null)
        {
            boss.TakeDamage(Mathf.RoundToInt(reverseHitDamage));
        }
    }
}