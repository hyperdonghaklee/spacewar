using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum PartType
{
    None = 0,          // 장착 안 됨 / 없음
    ForceHoming = 1,   // [액티브] 강제 유도
    TimeStop = 2,      // [액티브] 시간 정지
    Reflect = 3,       // [액티브] 공격 반사
    BlackHole = 4,     // [액티브] 블랙홀
    SlowField = 5,     // [액티브] 감속장
    PositionSwap = 6,  // [액티브] 위치 스왑
    Shockwave = 7,     // [액티브] 충격파

    // --- 신규 액티브(Active) 파츠 ---
    Dash = 8,          // [액티브] 부스터 대시 (전방 고속 돌진 + 무적)
    HpBoostActive = 9, // [액티브] 나노 HP 증강 (최대 HP +1 증가 및 체력 +2 즉시 회복)
    EnergyShield = 10, // [액티브] 에너지 실드 (일정 시간 무적 보호막 + 주변 적 탄 소멸)

    // --- 신규 패시브(Passive) 파츠 (100번대) ---
    PassiveMaxHp = 101,      // [패시브] 복합 장갑 증설 (최대 HP +3 영구 증가)
    PassiveMoveSpeed = 102,  // [패시브] 고출력 추진기 (이동 및 회전 속도 25% 영구 증가)
    PassiveCooldown = 103,   // [패시브] 급속 냉각 코어 (폭탄 및 액티브 스킬 쿨타임 25% 감소)
    PassiveInvincible = 104  // [패시브] 비상 방어 필드 (피격 후 무적 시간 +1초 연장)
}

[Serializable]
public class RankEntry
{
    public string playerName;
    public int score;
}

[Serializable]
public class RankListWrapper
{
    public List<RankEntry> rankings = new List<RankEntry>();
}

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("현재 플레이 데이터 (사망 및 스테이지 전환 시 유지)")]
    public int currentStage = 1;
    public int currentScore = 0;
    public int money = 0;

    [Header("파츠 보유 및 퀵슬롯 (최대 2개 제한)")]
    public List<PartType> ownedParts = new List<PartType>();
    public PartType[] quickSlots = new PartType[2] { PartType.None, PartType.None };

    [Header("랭킹 데이터 (Top 5 내림차순)")]
    public List<RankEntry> rankList = new List<RankEntry>();

    private const string SAVE_KEY = "SpaceShooter_SaveData";
    private const string RANK_KEY = "SpaceShooter_RankData";

    // 파츠가 패시브인지 액티브인지 판별하는 헬퍼 메서드
    public static bool IsPassivePart(PartType part) => (int)part >= 100;
    public static bool IsActivePart(PartType part) => part != PartType.None && (int)part < 100;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadRankings();
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    public bool HasPart(PartType part) => ownedParts != null && ownedParts.Contains(part);

    public bool BuyPart(PartType part, int price)
    {
        if (ownedParts.Contains(part)) return false;
        if (money < price) return false;

        money -= price;
        ownedParts.Add(part);

        Debug.Log($"{part} 파츠 구매 완료! 남은 돈: {money}");
        return true;
    }

    public void EquipPartToSlot(int slotIndex, PartType part)
    {
        if (slotIndex < 0 || slotIndex >= 2) return;
        if (IsPassivePart(part)) return; // 패시브 파츠는 퀵슬롯 장착 불필요 (상시 적용)
        if (!ownedParts.Contains(part) && part != PartType.None) return;

        int otherSlot = slotIndex == 0 ? 1 : 0;

        if (part != PartType.None && quickSlots[otherSlot] == part)
        {
            quickSlots[otherSlot] = PartType.None;
        }

        quickSlots[slotIndex] = part;
        Debug.Log($"퀵슬롯 [{slotIndex + 1}]에 {part} 장착!");
    }

    public bool HasSaveFile() => PlayerPrefs.HasKey(SAVE_KEY);

    public void SaveGame()
    {
        string ownedStr = string.Join(",", ownedParts.Select(p => (int)p));
        string saveData = $"{currentStage}|{currentScore}|{money}|{(int)quickSlots[0]}|{(int)quickSlots[1]}|{ownedStr}";

        PlayerPrefs.SetString(SAVE_KEY, saveData);
        PlayerPrefs.Save();
        Debug.Log("게임 세이브 완료!");
    }

    public void LoadGame()
    {
        if (!HasSaveFile()) return;

        string[] data = PlayerPrefs.GetString(SAVE_KEY).Split('|');

        currentStage = int.Parse(data[0]);
        currentScore = int.Parse(data[1]);
        money = int.Parse(data[2]);

        quickSlots[0] = (PartType)int.Parse(data[3]);
        quickSlots[1] = (PartType)int.Parse(data[4]);

        ownedParts.Clear();

        if (data.Length > 5 && !string.IsNullOrEmpty(data[5]))
        {
            foreach (var p in data[5].Split(','))
            {
                if (int.TryParse(p, out int parsed))
                {
                    ownedParts.Add((PartType)parsed);
                }
            }
        }
        Debug.Log($"세이브 로드 완료! 스테이지: {currentStage}, 보유 돈: {money}");
    }

    public bool IsTop5Score(int score)
    {
        if (rankList.Count < 5) return true;
        return score > rankList.Last().score;
    }

    public void AddRanking(string playerName, int score)
    {
        rankList.Add(new RankEntry { playerName = playerName, score = score });
        rankList = rankList.OrderByDescending(r => r.score).Take(5).ToList();

        RankListWrapper wrapper = new RankListWrapper { rankings = rankList };
        PlayerPrefs.SetString(RANK_KEY, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();

        Debug.Log($"랭킹 등록 완료: {playerName} - {score}점");
    }

    private void LoadRankings()
    {
        if (PlayerPrefs.HasKey(RANK_KEY))
        {
            RankListWrapper wrapper = JsonUtility.FromJson<RankListWrapper>(PlayerPrefs.GetString(RANK_KEY));
            if (wrapper != null) rankList = wrapper.rankings;
        }
    }
}