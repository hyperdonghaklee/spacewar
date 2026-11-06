// =========================================================================================
// [1. 네임스페이스(Namespace) 불러오기]
// 네임스페이스란? 연관된 도구(클래스/메서드)들을 묶어놓은 '서류 가방'입니다.
// =========================================================================================
using System;                   // C# 기본 핵심 도구 모음 (Serializable, Type 변환 등)
using System.Collections.Generic; // 가변형 자료구조 도구 모음 (List<T> 등)
using System.Linq;              // LINQ (데이터 정렬, 필터링, 가공용 쿼리문 기능)
using UnityEngine;              // 유니티 엔진 핵심 기능 (MonoBehaviour, PlayerPrefs, Debug 등)

// =========================================================================================
// [2. 열거형 (enum : Enumeration)]
// - 개념: 숫자에 '직관적인 이름'을 붙여주는 C#의 기본 데이터 타입입니다.
// - 이유: 코드 내에서 숫자 1, 2 대신 PartType.ForceHoming 처럼 작성하여 가독성을 높이고 실수를 방지합니다.
// =========================================================================================
public enum PartType
{
    None = 0,        // 장착 안 됨 / 없음 (0번으로 기본값 지정)
    ForceHoming = 1, // 강제 유도 (1번)
    TimeStop = 2,    // 시간 정지 (2번)
    Reflect = 3,     // 공격 반사 (3번)
    BlackHole = 4,   // 블랙홀 (4번)
    SlowField = 5,    // 감속장 (5번)
    PositionSwap = 6, // 위치 스왑
    Shockwave = 7     // 충격파
}

// =========================================================================================
// [3. 데이터 클래스 & 어트리뷰트(Attribute)]
// - 클래스(Class): 관련 데이터(이름, 점수)와 기능을 묶는 설계도입니다.
// - [Serializable] (어트리뷰트): C# 객체 데이터를 유니티 인스펙터(Inspector) 창에 노출시키고,
//   텍스트(JSON)로 변환할 수 있도록 "직렬화"를 허가하는 표식입니다.
// =========================================================================================
[Serializable]
public class RankEntry
{
    public string playerName; // 플레이어 이름 (string: 텍스트 문자열 타입)
    public int score;         // 플레이어 점수 (int: 정수 숫자 타입)
}

// =========================================================================================
// [4. 래퍼 클래스 (Wrapper Class) 패턴]
// - 유니티 JsonUtility의 한계: List<T> 데이터는 단독으로 최상위에 있으면 JSON 변환을 못합니다.
// - 해결법: 리스트를 일반 클래스로 한 번 감싸서(Wrap) 저장 가능한 형태로 만드는 테크닉입니다.
// =========================================================================================
[Serializable]
public class RankListWrapper
{
    // List<RankEntry>: RankEntry 객체들을 담을 수 있는 가변 길이의 리스트
    public List<RankEntry> rankings = new List<RankEntry>();
}

// =========================================================================================
// [5. DataManager 메인 클래스]
// - MonoBehaviour 상속: 이 클래스가 유니티 게임 오브젝트에 컴포넌트로 붙을 수 있게 만들어 줍니다.
// =========================================================================================
public class DataManager : MonoBehaviour
{
    // -------------------------------------------------------------------------------------
    // [싱글톤 패턴 (Singleton Pattern)]
    // - 개념: 게임 내에서 이 객체의 인스턴스(복사본)가 오직 '하나만' 존재하도록 보장하는 디자인 패턴.
    // - static: 메모리의 '정적 영역'에 할당되어 DataManager.Instance 로 전역 어디서나 접근 가능.
    // - 프로퍼티 { get; private set; }: 읽기는 외부(public) 허용, 값 수정은 내부(private)로 보호.
    // -------------------------------------------------------------------------------------
    public static DataManager Instance { get; private set; }

    // [Header("...")] : 유니티 인스펙터 창에서 변수들을 시각적으로 그룹화해주는 어트리뷰트
    [Header("현재 플레이 데이터 (사망 및 스테이지 전환 시 유지)")]
    public int currentStage = 1; // 현재 진행 스테이지
    public int currentScore = 0; // 현재 누적 점수
    public int money = 0;        // 보유 재화(골드)

    [Header("파츠 보유 및 퀵슬롯 (최대 2개 제한)")]
    // List<T> (가변 리스트): 크기가 자유롭게 늘어나고 줄어드는 리스트 (보유 파츠 관리용)
    public List<PartType> ownedParts = new List<PartType>();

    // Array (고정 배열 : T[]): 크기가 딱 2개로 고정된 공간 (퀵슬롯 1번, 2번 전용)
    public PartType[] quickSlots = new PartType[2] { PartType.None, PartType.None };

    [Header("랭킹 데이터 (Top 5 내림차순)")]
    public List<RankEntry> rankList = new List<RankEntry>();

    // const (상수): 한 번 정하면 절대로 값이 바뀌지 않는 컴파일 타임 변수 (오타 방지용 Key 값)
    private const string SAVE_KEY = "SpaceShooter_SaveData";
    private const string RANK_KEY = "SpaceShooter_RankData";

    // -------------------------------------------------------------------------------------
    // [Awake() : 유니티 생명주기(Life Cycle) 함수]
    // - 스크립트가 실행될 때 Start()보다 먼저, 딱 1번 실행되는 초기화 타임라인 함수입니다.
    // -------------------------------------------------------------------------------------
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
            Destroy(this); // gameObject 전체를 폭파하지 않고 중복 컴포넌트만 제거!
        }
    }

    // -------------------------------------------------------------------------------------
    // [파츠 상점 로직]
    // - bool: 참(true) 또는 거짓(false)을 반환하는 조건형 리턴 타입
    // -------------------------------------------------------------------------------------
    public bool BuyPart(PartType part, int price)
    {
        // Early Exit 패턴: 조건에 맞지 않으면 즉시 함수를 종료하여 불필요한 계산을 방지
        if (ownedParts.Contains(part)) return false; // List.Contains(): 리스트에 특정 요소가 있는지 검사
        if (money < price) return false;             // 돈이 부족하면 구매 불가

        money -= price;       // 복합 대입 연산자: money = money - price 와 동일
        ownedParts.Add(part); // List.Add(): 리스트 맨 뒤에 요소 추가

        // 문자열 보간 ($"..."): 텍스트 내에 {변수}를 직접 삽입하는 C#의 편리한 문법
        Debug.Log($"{part} 파츠 구매 완료! 남은 돈: {money}");
        return true;          // 구매 성공 반환
    }

    // -------------------------------------------------------------------------------------
    // [퀵슬롯 파츠 장착 로직]
    // -------------------------------------------------------------------------------------
    public void EquipPartToSlot(int slotIndex, PartType part)
    {
        // 예외 처리: 배열의 범위를 벗어난 인덱스가 들어오면 무시 (0, 1번 슬롯만 허용)
        if (slotIndex < 0 || slotIndex >= 2) return;

        // 논리 연산자 (!, &&): 보유 중이 아니면서(&&) 장착 해제(None) 목적도 아닌(!) 경우 차단
        if (!ownedParts.Contains(part) && part != PartType.None) return;

        // 삼항 연산자 (조건식 ? 참일때 : 거짓일때)
        // slotIndex가 0이면 상대방 슬롯은 1, slotIndex가 1이면 상대방 슬롯은 0
        int otherSlot = slotIndex == 0 ? 1 : 0;

        // 중복 장착 방지: 상대방 슬롯에 내가 장착하려는 파츠가 이미 있다면 그쪽 슬롯을 해제(None)
        if (quickSlots[otherSlot] == part)
        {
            quickSlots[otherSlot] = PartType.None;
        }

        // 선택한 슬롯에 파츠 장착
        quickSlots[slotIndex] = part;
        Debug.Log($"퀵슬롯 [{slotIndex + 1}]에 {part} 장착!");
    }

    // -------------------------------------------------------------------------------------
    // [세이브 / 로드 로직]
    // -------------------------------------------------------------------------------------

    // 표현식 본문 (Expression-bodied member, =>): 중괄호{ return ...; }를 줄여 쓰는 한 줄 표현법
    // PlayerPrefs.HasKey(): 해당 키 이름으로 저장된 데이터가 저장소에 존재하는지 검사
    public bool HasSaveFile() => PlayerPrefs.HasKey(SAVE_KEY);

    public void SaveGame()
    {
        // 1. LINQ의 Select(): ownedParts 내 enum 요소들을 하나씩 꺼내 int 정수로 변환(캐스팅)
        // 2. string.Join(",", ...): 변환된 정수들을 반점(,)을 구분자로 이어붙여 문자열 생성 (예: "1,3")
        string ownedStr = string.Join(",", ownedParts.Select(p => (int)p));

        // 파이프(|) 기호를 구분자(Delimiter)로 활용하여 세이브 데이터를 하나의 단일 텍스트로 합성
        // 예: "1|100|500|1|3|1,3" (스테이지|점수|돈|슬롯0|슬롯1|보유파츠)
        string saveData = $"{currentStage}|{currentScore}|{money}|{(int)quickSlots[0]}|{(int)quickSlots[1]}|{ownedStr}";

        // PlayerPrefs: 유니티에서 제공하는 레지스트리 기반 간단한 로컬 데이터 저장소 (Key-Value)
        PlayerPrefs.SetString(SAVE_KEY, saveData);
        PlayerPrefs.Save(); // 디스크 하드에 즉시 반영
        Debug.Log("게임 세이브 완료!");
    }

    public void LoadGame()
    {
        if (!HasSaveFile()) return; // 저장 파일이 없으면 로드 중단

        // string.Split('|'): 파이프(|) 구분자를 기준으로 텍스트를 자르고 string[] 배열로 분할 반환
        string[] data = PlayerPrefs.GetString(SAVE_KEY).Split('|');

        // int.Parse(): 문자열 형태의 숫자 "100"을 실제 계산 가능한 정수 100 으로 형변환(Parsing)
        currentStage = int.Parse(data[0]);
        currentScore = int.Parse(data[1]);
        money = int.Parse(data[2]);

        // (PartType)int.Parse(...): 정수를 다시 열거형(Enum)으로 명시적 형변환(Casting)
        quickSlots[0] = (PartType)int.Parse(data[3]);
        quickSlots[1] = (PartType)int.Parse(data[4]);

        ownedParts.Clear(); // 기존 보유 리스트 비우기

        // string.IsNullOrEmpty(): 문자열이 비어있거나 null인지 검사
        if (!string.IsNullOrEmpty(data[5]))
        {
            // foreach 반복문: 구분자 반점(,)으로 잘라낸 데이터 배열을 하나씩 순회
            foreach (var p in data[5].Split(','))
            {
                ownedParts.Add((PartType)int.Parse(p));
            }
        }
        Debug.Log($"세이브 로드 완료! 스테이지: {currentStage}, 보유 돈: {money}");
    }

    // -------------------------------------------------------------------------------------
    // [랭킹 시스템 로직]
    // -------------------------------------------------------------------------------------

    public bool IsTop5Score(int score)
    {
        // 랭킹 리스트 개수(Count)가 5개 미만이면 무조건 랭킹 진입
        if (rankList.Count < 5) return true;

        // LINQ의 Last(): 내림차순 정렬되어 있으므로 리스트의 맨 마지막(5위) 점수를 가져옴
        return score > rankList.Last().score;
    }

    public void AddRanking(string playerName, int score)
    {
        // 새로운 랭킹 항목(객체) 생성 후 추가
        rankList.Add(new RankEntry { playerName = playerName, score = score });

        // [LINQ 체이닝 기법]
        // 1. OrderByDescending(r => r.score) : 점수 기준 내림차순 정렬 (높은 점수 -> 낮은 점수)
        // 2. Take(5)                         : 정렬된 목록 중 맨 위에서 5개만 추출
        // 3. ToList()                        : 결과를 다시 List<RankEntry> 형태로 변환
        rankList = rankList.OrderByDescending(r => r.score).Take(5).ToList();

        // [JSON 직렬화]
        // RankListWrapper에 담은 후 JsonUtility.ToJson()으로 클래스 객체를 JSON 텍스트 문자열로 변환
        RankListWrapper wrapper = new RankListWrapper { rankings = rankList };
        PlayerPrefs.SetString(RANK_KEY, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();

        Debug.Log($"랭킹 등록 완료: {playerName} - {score}점");
    }

    private void LoadRankings()
    {
        if (PlayerPrefs.HasKey(RANK_KEY))
        {
            // JsonUtility.FromJson<T>(): 저장된 JSON 문자열을 원래 C# 객체 형태로 복원 (역직렬화)
            RankListWrapper wrapper = JsonUtility.FromJson<RankListWrapper>(PlayerPrefs.GetString(RANK_KEY));

            if (wrapper != null) rankList = wrapper.rankings;
        }
    }
}