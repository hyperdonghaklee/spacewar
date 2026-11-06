using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("스킬 기본 설정")]
    public float skillCooldown = 5f;
    private Dictionary<PartType, float> cooldownEnd = new Dictionary<PartType, float>();

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

    public float Remaining(PartType skill)
    {
        return cooldownEnd.TryGetValue(skill, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }

    public float CooldownRatio(PartType skill)
    {
        return skillCooldown > 0f ? Remaining(skill) / skillCooldown : 0f;
    }

    // 퀵슬롯 스킬 실행 (Player.cs에서 호출)
    public bool ExecuteSkill(PartType skill)
    {
        // 쿨타임 검사: 대기 시간 중이면 거절 효과음 재생 후 취소
        if (cooldownEnd.TryGetValue(skill, out float end) && Time.time < end)
        {
            SoundManager.Play(SoundManager.Sfx.Deny); // [사운드 연동] 쿨타임 진행 중 거절음
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
        }

        // 실제로 스킬 발동에 성공했을 때만 파츠별 전용 사운드 재생
        if (success)
        {
            cooldownEnd[skill] = Time.time + skillCooldown;
            SoundManager.PlaySkill((int)skill); // [사운드 연동] 1~5번 파츠별 맞춤 효과음 자동 분기
            Debug.Log($"[스킬 발동 성공] {skill}");
        }
        return success;
    }

    #region 5대 스킬 실질 기능 구현

    // 1. 강제 유도: 화면 내 적 투사체들을 가장 가까운 적에게 방향 전환
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

    // 2. 시간 정지: 일정 시간 동안 적의 투사체 속도를 0으로 고정
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

    // 3. 공격 반사: 주변 투사체의 진행 방향을 반대로 역전
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

    // 4. 블랙홀: 주변 투사체들을 플레이어 위치(또는 지정 위치)로 끌어당겨 소멸
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

    // 5. 감속장: 플레이어 주변 투사체의 속도를 대폭 감소
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

    // 6. 위치 스왑: 화면 안에서 가장 가까운 적과 자리를 바꾸고, 그 적을 잠시 멈춤
    private bool ExecutePositionSwap()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Enemy nearest = null;
        float best = float.MaxValue;

        foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            Vector3 v = cam.WorldToViewportPoint(e.transform.position);
            if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue; // 화면 밖 제외

            float d = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = e;
            }
        }

        if (nearest == null) return false; // 대상이 없으면 쿨타임 소모 안 함

        Vector3 playerPos = transform.position;
        Vector3 enemyPos = nearest.transform.position;

        transform.position = new Vector3(enemyPos.x, enemyPos.y, playerPos.z);

        // 적은 Rigidbody2D로 움직이므로 rb.position도 같이 바꿔야 함
        Vector3 newEnemyPos = new Vector3(playerPos.x, playerPos.y, enemyPos.z);
        nearest.transform.position = newEnemyPos;
        Rigidbody2D erb = nearest.GetComponent<Rigidbody2D>();
        if (erb != null)
        {
            erb.position = newEnemyPos;
            erb.linearVelocity = Vector2.zero;
        }

        // 거리 0 넉백 = 속도 0으로 멈추고 pushTimer 동안 이동/사격 정지
        nearest.ApplyKnockback(Vector2.up, 0f, swapStunTime);

        // 스왑 직후 잠깐 무적 (Player와 같은 오브젝트에 Item이 붙어 있음)
        Player player = GetComponent<Player>();
        if (player != null) player.ActivateInvincibleItem(swapInvincibleTime);

        return true;
    }

    // 7. 충격파: 주변 적 탄을 지우고 적을 밀어냄 (보스는 밀리지 않음)
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

        StartCoroutine(ShockwaveRingRoutine(transform.position, shockwaveRadius));
        return affected > 0;
    }

    // 충격파 연출: 퍼져 나가는 원 (이미지 없이 LineRenderer로 생성)
    private IEnumerator ShockwaveRingRoutine(Vector3 center, float radius)
    {
        GameObject go = new GameObject("ShockwaveRing");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.sortingOrder = 100;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lr.material = new Material(shader);

        const int segments = 40;
        const float duration = 0.3f;
        lr.positionCount = segments;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float r = Mathf.Lerp(0.3f, radius, p);

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
            }

            Color c = new Color(0.5f, 0.9f, 1f, 1f - p);
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