using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Shop : MonoBehaviour
{
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
        public PartInfo info;
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
        new PartInfo { type = PartType.ForceHoming, displayName = "강제 유도", description = "화면의 모든 미사일을 바라보는 적에게 보냄", price = 300 },
        new PartInfo { type = PartType.TimeStop, displayName = "시간 정지", description = "잠시 동안 미사일만 멈춤", price = 250 },
        new PartInfo { type = PartType.Reflect, displayName = "공격 반사", description = "닿기 직전에 누르면 미사일을 되돌려 보냄", price = 200 },
        new PartInfo { type = PartType.BlackHole, displayName = "블랙홀", description = "누른 곳에 블랙홀을 만들어 주변 미사일 흡수", price = 350 },
        new PartInfo { type = PartType.SlowField, displayName = "감속장", description = "잠시 동안 내 주변 미사일을 느리게", price = 150 },
        new PartInfo { type = PartType.PositionSwap, displayName = "위치 스왑", description = "가장 가까운 적과 자리를 바꾸고 그 적을 잠시 멈춤", price = 300 },
        new PartInfo { type = PartType.Shockwave, displayName = "충격파", description = "주변 적 탄을 지우고 적을 밀어냄", price = 200 }
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
    private bool isOpen;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        BuildRows();
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

            Row row = new Row { info = info };

            SetLabel(FindChild<TMP_Text>(t, "NameText"), info.displayName);
            SetLabel(FindChild<TMP_Text>(t, "DescText"), info.description);
            row.priceText = FindChild<TMP_Text>(t, "PriceText");
            row.buyButton = FindChild<Button>(t, "BuyButton");
            row.slot1Button = FindChild<Button>(t, "Slot1Button");
            row.slot2Button = FindChild<Button>(t, "Slot2Button");
            if (row.buyButton != null) row.buyLabel = row.buyButton.GetComponentInChildren<TMP_Text>();

            if (row.buyButton != null) row.buyButton.onClick.AddListener(() => Buy(row));
            if (row.slot1Button != null) row.slot1Button.onClick.AddListener(() => Equip(row, 0));
            if (row.slot2Button != null) row.slot2Button.onClick.AddListener(() => Equip(row, 1));

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
        SetLabel(noticeText, "");
        Refresh();

        while (isOpen) yield return null;

        Time.timeScale = 1f;
        if (player != null) player.enabled = true;
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
            SetLabel(noticeText, $"{row.info.displayName} 구매! 퀵슬롯에 장착하세요");
        }

        Refresh();
    }

    void Equip(Row row, int slot)
    {
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
            SetLabel(noticeText, $"{row.info.displayName} → {slot + 1}번 슬롯");
        }

        Refresh();
    }

    void Refresh()
    {
        DataManager data = DataManager.Instance;
        int money = data != null ? data.money : 0;

        SetLabel(moneyText, $"보유 파편 {money:N0}");

        foreach (Row row in rows)
        {
            bool owned = data != null && data.ownedParts.Contains(row.info.type);

            SetLabel(row.priceText, owned ? "보유 중" : $"{row.info.price:N0}");
            if (row.buyButton != null) row.buyButton.interactable = !owned;
            SetLabel(row.buyLabel, owned ? "구매 완료" : "구매");

            SetSlotButton(row.slot1Button, data, owned, 0, row.info.type);
            SetSlotButton(row.slot2Button, data, owned, 1, row.info.type);
        }

        SetLabel(slotText, $"퀵슬롯  [{slot1KeyName}] {SlotName(data, 0)}    [{slot2KeyName}] {SlotName(data, 1)}");
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