using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Shop : MonoBehaviour
{
    public enum ShopTab
    {
        Active,
        Passive
    }

    [System.Serializable]
    public class PartInfo
    {
        public PartType type;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public int price;
    }

    private class Row
    {
        public GameObject rootObj;
        public PartInfo info;
        public bool isPassive;
        public TMP_Text priceText;
        public Button buyButton;
        public TMP_Text buyLabel;
        public Button slot1Button;
        public Button slot2Button;
    }

    [Header("판매 파츠 (이름·설명·가격 자유롭게)")]
    [SerializeField]
    private PartInfo[] parts =
    {
        // --- 액티브 파츠 ---
        new PartInfo { type = PartType.ForceHoming,   displayName = "강제 유도",     description = "[액티브] 화면의 모든 미사일을 바라보는 적에게 보냄", price = 300 },
        new PartInfo { type = PartType.TimeStop,      displayName = "시간 정지",     description = "[액티브] 잠시 동안 적 미사일만 멈춤", price = 250 },
        new PartInfo { type = PartType.Reflect,       displayName = "공격 반사",     description = "[액티브] 주변 미사일을 적에게 되돌려 보냄", price = 200 },
        new PartInfo { type = PartType.BlackHole,     displayName = "블랙홀",       description = "[액티브] 블랙홀을 생성해 주변 미사일 흡수 소멸", price = 350 },
        new PartInfo { type = PartType.SlowField,     displayName = "감속장",       description = "[액티브] 잠시 동안 내 주변 미사일을 느리게 만듦", price = 150 },
        new PartInfo { type = PartType.PositionSwap,  displayName = "위치 스왑",     description = "[액티브] 가장 가까운 적과 자리를 바꾸고 적을 잠시 멈춤", price = 300 },
        new PartInfo { type = PartType.Shockwave,     displayName = "충격파",       description = "[액티브] 주변 적 탄을 지우고 적을 강하게 밀어냄", price = 200 },
        new PartInfo { type = PartType.Dash,          displayName = "부스터 대시",   description = "[액티브] 바라보는 방향으로 무적 고속 돌진 (슬롯 장착 또는 E키 발동)", price = 200 },
        new PartInfo { type = PartType.HpBoostActive, displayName = "나노 HP 증강", description = "[액티브] 사용 시 최대 HP +1 확장 및 현재 체력 +2 즉시 회복", price = 250 },
        new PartInfo { type = PartType.EnergyShield,  displayName = "에너지 실드",   description = "[액티브] 3.5초 동안 무적 보호막 전개 및 접촉한 적 탄 소멸", price = 300 },

        // --- 패시브 파츠 ---
        new PartInfo { type = PartType.PassiveMaxHp,      displayName = "복합 장갑 증설",   description = "[패시브] 구매 즉시 최대 HP +3 영구 증가 및 체력 +3 회복", price = 250 },
        new PartInfo { type = PartType.PassiveMoveSpeed,  displayName = "고출력 추진기",   description = "[패시브] 우주선 이동 속도 및 회전 속도 25% 영구 증가", price = 200 },
        new PartInfo { type = PartType.PassiveCooldown,   displayName = "급속 냉각 코어",   description = "[패시브] 비상 폭탄(Q) 및 모든 액티브 스킬 쿨타임 25% 감소", price = 300 },
        new PartInfo { type = PartType.PassiveInvincible, displayName = "비상 방어 필드",   description = "[패시브] 피격 후 무적 지속 시간 +1.0초 영구 연장", price = 200 }
    };

    [Header("화면 연결")]
    [Tooltip("상점 전체 창 (평소엔 꺼져 있음)")]
    [SerializeField] private GameObject panel;
    [Tooltip("파츠 한 줄 견본. 자식 이름: NameText, DescText, PriceText, BuyButton, Slot1Button, Slot2Button")]
    [SerializeField] private Transform rowTemplate;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text noticeText;
    [Tooltip("다음 스테이지로 가는 버튼")]
    [SerializeField] private Button closeButton;

    [Header("퀵슬롯 키 이름 (안내 글자용)")]
    [SerializeField] private string slot1KeyName = "1";
    [SerializeField] private string slot2KeyName = "2";

    private readonly List<Row> rows = new List<Row>();
    private ShopTab currentTab = ShopTab.Active;
    private Button activeTabBtn;
    private Button passiveTabBtn;
    private Image activeTabImg;
    private Image passiveTabImg;
    private bool isOpen;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        EnsureAllPartsRegistered();
        CreateCategoryTabsIfNeeded();
        BuildRows();
    }

    // 씬 파일에 이전 파츠 목록만 직렬화되어 있을 경우 신규 액티브/패시브 파츠를 자동 병합합니다.
    void EnsureAllPartsRegistered()
    {
        PartInfo[] defaults =
        {
            new PartInfo { type = PartType.Dash,              displayName = "부스터 대시",   description = "[액티브] 바라보는 방향으로 무적 고속 돌진 (슬롯 장착 또는 E키 발동)", price = 200 },
            new PartInfo { type = PartType.HpBoostActive,     displayName = "나노 HP 증강", description = "[액티브] 사용 시 최대 HP +1 확장 및 현재 체력 +2 즉시 회복", price = 250 },
            new PartInfo { type = PartType.EnergyShield,      displayName = "에너지 실드",   description = "[액티브] 3.5초 동안 무적 보호막 전개 및 접촉한 적 탄 소멸", price = 300 },
            new PartInfo { type = PartType.PassiveMaxHp,      displayName = "복합 장갑 증설", description = "[패시브] 구매 즉시 최대 HP +3 영구 증가 및 체력 +3 회복", price = 250 },
            new PartInfo { type = PartType.PassiveMoveSpeed,  displayName = "고출력 추진기", description = "[패시브] 우주선 이동 속도 및 회전 속도 25% 영구 증가", price = 200 },
            new PartInfo { type = PartType.PassiveCooldown,   displayName = "급속 냉각 코어", description = "[패시브] 비상 폭탄(Q) 및 모든 액티브 스킬 쿨타임 25% 감소", price = 300 },
            new PartInfo { type = PartType.PassiveInvincible, displayName = "비상 방어 필드", description = "[패시브] 피격 후 무적 지속 시간 +1.0초 영구 연장", price = 200 }
        };

        List<PartInfo> list = new List<PartInfo>(parts ?? new PartInfo[0]);
        foreach (PartInfo def in defaults)
        {
            bool exists = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].type == def.type)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists) list.Add(def);
        }
        parts = list.ToArray();
    }

    // 에디터 수정 없이도 상점 상단에 [액티브 파츠] / [패시브 파츠] 탭 버튼을 자동 배치합니다.
    void CreateCategoryTabsIfNeeded()
    {
        if (panel == null) return;

        Transform scrollTrans = panel.transform.Find("ShopScrollView");
        if (scrollTrans != null && scrollTrans.TryGetComponent(out RectTransform scrollRt))
        {
            scrollRt.anchoredPosition = new Vector2(0f, -95f);
            scrollRt.sizeDelta = new Vector2(1400f, 520f);
        }

        Sprite btnSprite = null;
        TMP_FontAsset font = moneyText != null ? moneyText.font : null;
        if (closeButton != null && closeButton.TryGetComponent(out Image cImg))
        {
            btnSprite = cImg.sprite;
        }

        activeTabBtn = CreateTabButton("ActiveTabButton", "[ 액티브 파츠 (스킬) ]", new Vector2(-200f, -215f), btnSprite, font, () => SwitchTab(ShopTab.Active), out activeTabImg);
        passiveTabBtn = CreateTabButton("PassiveTabButton", "[ 패시브 파츠 (강화) ]", new Vector2(200f, -215f), btnSprite, font, () => SwitchTab(ShopTab.Passive), out passiveTabImg);
    }

    Button CreateTabButton(string objName, string title, Vector2 anchoredPos, Sprite sprite, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick, out Image bgImage)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(panel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(360f, 54f);

        bgImage = go.GetComponent<Image>();
        bgImage.sprite = sprite;
        if (sprite != null) bgImage.type = Image.Type.Sliced;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(() =>
        {
            SoundManager.Play(SoundManager.Sfx.Click);
            onClick?.Invoke();
        });

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(go.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = title;
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    public void SwitchTab(ShopTab tab)
    {
        currentTab = tab;
        SetLabel(noticeText, tab == ShopTab.Active
            ? "액티브 파츠는 구매 후 1번 또는 2번 슬롯에 장착하여 사용합니다."
            : "패시브 파츠는 구매 즉시 영구적으로 우주선 성능이 강화됩니다.");
        Refresh();
    }

    void BuildRows()
    {
        if (rowTemplate == null)
        {
            Debug.LogWarning("[Shop] Row Template 칸이 비어 있어요.");
            return;
        }

        rowTemplate.gameObject.SetActive(false);

        foreach (PartInfo info in parts)
        {
            Transform t = Instantiate(rowTemplate, rowTemplate.parent);
            t.gameObject.SetActive(true);
            t.name = info.displayName;

            bool passive = DataManager.IsPassivePart(info.type);
            Row row = new Row
            {
                rootObj = t.gameObject,
                info = info,
                isPassive = passive
            };

            SetLabel(FindChild<TMP_Text>(t, "NameText"), info.displayName);
            SetLabel(FindChild<TMP_Text>(t, "DescText"), info.description);
            row.priceText = FindChild<TMP_Text>(t, "PriceText");
            row.buyButton = FindChild<Button>(t, "BuyButton");
            row.slot1Button = FindChild<Button>(t, "Slot1Button");
            row.slot2Button = FindChild<Button>(t, "Slot2Button");
            if (row.buyButton != null) row.buyLabel = row.buyButton.GetComponentInChildren<TMP_Text>();

            if (row.buyButton != null) row.buyButton.onClick.AddListener(() => Buy(row));

            if (passive)
            {
                // 패시브 파츠는 슬롯 장착 버튼 숨김
                if (row.slot1Button != null) row.slot1Button.gameObject.SetActive(false);
                if (row.slot2Button != null) row.slot2Button.gameObject.SetActive(false);
            }
            else
            {
                if (row.slot1Button != null) row.slot1Button.onClick.AddListener(() => Equip(row, 0));
                if (row.slot2Button != null) row.slot2Button.onClick.AddListener(() => Equip(row, 1));
            }

            rows.Add(row);
        }
    }

    public IEnumerator OpenRoutine()
    {
        if (panel == null) yield break;

        isOpen = true;
        panel.SetActive(true);

        Player player = FindAnyObjectByType<Player>();
        if (player != null) player.enabled = false;

        Time.timeScale = 0f;
        currentTab = ShopTab.Active;
        SetLabel(noticeText, "액티브 파츠와 패시브 파츠 탭을 선택해 구매할 수 있습니다.");
        Refresh();

        while (isOpen) yield return null;

        Time.timeScale = 1f;
        if (player != null)
        {
            player.enabled = true;
            player.ApplyPassiveUpgrades();
        }
        panel.SetActive(false);
    }

    public void Close()
    {
        if (isOpen) SoundManager.Play(SoundManager.Sfx.Click);
        isOpen = false;
    }

    void Buy(Row row)
    {
        DataManager data = DataManager.Instance;
        if (data == null)
        {
            SetLabel(noticeText, "DataManager가 없어요 (MainMenu에서 시작하세요)");
            return;
        }

        if (data.ownedParts.Contains(row.info.type)) return;

        if (data.money < row.info.price)
        {
            SoundManager.Play(SoundManager.Sfx.Deny);
            SetLabel(noticeText, $"파편이 {row.info.price - data.money:N0} 부족해요");
            return;
        }

        if (data.BuyPart(row.info.type, row.info.price))
        {
            SoundManager.Play(SoundManager.Sfx.Buy);

            if (row.isPassive)
            {
                // 패시브 구매 시 플레이어에게 즉시 적용
                Player player = FindAnyObjectByType<Player>();
                if (player != null) player.ApplyPassiveUpgrades();
                SetLabel(noticeText, $"[패시브] {row.info.displayName} 구매 완료! 효과가 즉시 적용되었습니다.");
            }
            else
            {
                // 빈 퀵슬롯이 있으면 자동 장착 편의 기능 제공
                if (data.quickSlots[0] == PartType.None)
                {
                    data.EquipPartToSlot(0, row.info.type);
                    SetLabel(noticeText, $"{row.info.displayName} 구매 및 1번 슬롯에 자동 장착되었습니다!");
                }
                else if (data.quickSlots[1] == PartType.None)
                {
                    data.EquipPartToSlot(1, row.info.type);
                    SetLabel(noticeText, $"{row.info.displayName} 구매 및 2번 슬롯에 자동 장착되었습니다!");
                }
                else
                {
                    SetLabel(noticeText, $"{row.info.displayName} 구매 완료! 원하시는 퀵슬롯에 장착하세요.");
                }
            }
        }

        Refresh();
    }

    void Equip(Row row, int slot)
    {
        if (row.isPassive) return;

        DataManager data = DataManager.Instance;
        if (data == null || !data.ownedParts.Contains(row.info.type)) return;

        SoundManager.Play(SoundManager.Sfx.Equip);

        if (data.quickSlots[slot] == row.info.type)
        {
            data.EquipPartToSlot(slot, PartType.None);
            SetLabel(noticeText, $"{slot + 1}번 슬롯 해제");
        }
        else
        {
            data.EquipPartToSlot(slot, row.info.type);
            SetLabel(noticeText, $"{row.info.displayName} → {slot + 1}번 슬롯 장착");
        }

        Refresh();
    }

    void Refresh()
    {
        DataManager data = DataManager.Instance;
        int money = data != null ? data.money : 0;

        SetLabel(moneyText, $"보유 파편 {money:N0}");

        // 탭 버튼 하이라이트 색상 갱신
        if (activeTabImg != null)
            activeTabImg.color = currentTab == ShopTab.Active ? new Color(0f, 0.9f, 1f, 1f) : new Color(0.35f, 0.4f, 0.5f, 0.85f);
        if (passiveTabImg != null)
            passiveTabImg.color = currentTab == ShopTab.Passive ? new Color(0.2f, 1f, 0.55f, 1f) : new Color(0.35f, 0.4f, 0.5f, 0.85f);

        int passiveCount = 0;
        if (data != null)
        {
            foreach (PartType pt in data.ownedParts)
            {
                if (DataManager.IsPassivePart(pt)) passiveCount++;
            }
        }

        foreach (Row row in rows)
        {
            bool showRow = (currentTab == ShopTab.Active && !row.isPassive) || (currentTab == ShopTab.Passive && row.isPassive);
            if (row.rootObj != null && row.rootObj.activeSelf != showRow)
            {
                row.rootObj.SetActive(showRow);
            }

            if (!showRow) continue;

            bool owned = data != null && data.ownedParts.Contains(row.info.type);

            if (row.isPassive)
            {
                SetLabel(row.priceText, owned ? "상시 적용 중" : $"{row.info.price:N0}");
                if (row.buyButton != null) row.buyButton.interactable = !owned;
                SetLabel(row.buyLabel, owned ? "적용 완료" : "구매");
            }
            else
            {
                SetLabel(row.priceText, owned ? "보유 중" : $"{row.info.price:N0}");
                if (row.buyButton != null) row.buyButton.interactable = !owned;
                SetLabel(row.buyLabel, owned ? "구매 완료" : "구매");

                SetSlotButton(row.slot1Button, data, owned, 0, row.info.type);
                SetSlotButton(row.slot2Button, data, owned, 1, row.info.type);
            }
        }

        string dashInfo = (data != null && data.HasPart(PartType.Dash)) ? "   |   [E] 부스터 대시 보유" : "";
        SetLabel(slotText, $"퀵슬롯 [{slot1KeyName}] {SlotName(data, 0)}   [{slot2KeyName}] {SlotName(data, 1)}   |   패시브 강화: {passiveCount}종{dashInfo}");
    }

    void SetSlotButton(Button button, DataManager data, bool owned, int slot, PartType type)
    {
        if (button == null) return;

        button.interactable = owned;

        bool equipped = data != null && data.quickSlots[slot] == type;
        SetLabel(button.GetComponentInChildren<TMP_Text>(), equipped ? $"{slot + 1}번 해제" : $"{slot + 1}번 장착");
    }

    string SlotName(DataManager data, int slot)
    {
        if (data == null || data.quickSlots[slot] == PartType.None) return "비어 있음";

        foreach (PartInfo info in parts)
        {
            if (info.type == data.quickSlots[slot]) return info.displayName;
        }

        return data.quickSlots[slot].ToString();
    }

    T FindChild<T>(Transform root, string childName) where T : Component
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t.GetComponent<T>();
        }

        Debug.LogWarning($"[Shop] 견본 줄에 '{childName}' 이름의 오브젝트가 없어요.");
        return null;
    }

    void SetLabel(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }
}