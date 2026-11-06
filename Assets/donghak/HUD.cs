using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [System.Serializable]
    public class CooldownSlot
    {
        [Tooltip("이 칸 전체 묶음. 아이템 칸은 퀵슬롯이 비면 자동으로 꺼짐")]
        public GameObject root;
        [Tooltip("폭탄·아이템 그림")]
        public Image icon;
        [Tooltip("그림 위를 덮는 어두운 이미지. 쿨타임이 줄어드는 만큼 시계처럼 사라짐")]
        public Image cover;
        [Tooltip("남은 초 숫자")]
        public TMP_Text timeText;
        [HideInInspector] public TMP_Text labelText;
    }

    [System.Serializable]
    public class PartIcon
    {
        public PartType type;
        public Sprite icon;
    }

    [Header("플레이어")]
    [Tooltip("HP 바 (채워지는 쪽 이미지). Pivot X를 0으로 두면 왼쪽부터 줄어듦")]
    [SerializeField] private Image hpBar;
    [SerializeField] private TMP_Text hpText;

    [Header("폭탄 UI (쿨타임 게이지)")]
    [Tooltip("(선택) 쿨타임이 찰수록 늘어나는 게이지")]
    [SerializeField] private Image bombGaugeFill;

    [Header("쿨타임 칸 (직접 만든 UI 연결)")]
    [SerializeField] private CooldownSlot bombSlot = new CooldownSlot();
    [Tooltip("위에서부터 퀵슬롯 1번, 2번")]
    [SerializeField] private CooldownSlot[] skillSlots = { new CooldownSlot(), new CooldownSlot() };
    [Tooltip("파츠 종류별 아이콘 그림")]
    [SerializeField]
    private PartIcon[] partIcons =
    {
        new PartIcon { type = PartType.ForceHoming },
        new PartIcon { type = PartType.TimeStop },
        new PartIcon { type = PartType.Reflect },
        new PartIcon { type = PartType.BlackHole },
        new PartIcon { type = PartType.SlowField },
        new PartIcon { type = PartType.PositionSwap },
        new PartIcon { type = PartType.Shockwave },
        new PartIcon { type = PartType.Dash },
        new PartIcon { type = PartType.HpBoostActive },
        new PartIcon { type = PartType.EnergyShield }
    };

    [Header("쿨타임 칸 자동 생성")]
    [Tooltip("켜면 게임 시작 때 HP바 밑에 칸을 자동으로 만듦")]
    [SerializeField] private bool autoBuildSlots = true;
    [SerializeField] private Sprite bombIcon;
    [Tooltip("화면 왼쪽 위 모서리에서 칸 줄까지 거리 (1920x1080 기준 픽셀)")]
    [SerializeField] private Vector2 autoBarPosition = new Vector2(15f, -55f);
    [SerializeField] private float autoSlotSize = 68f;
    [SerializeField] private float autoSlotSpacing = 10f;
    [SerializeField] private float autoIconPadding = 6f;
    [SerializeField] private float autoKeyFontSize = 15f;
    [SerializeField] private float autoTimeFontSize = 28f;
    [SerializeField] private Color autoSlotBackColor = new Color(0.1f, 0.12f, 0.2f, 0.85f);
    [SerializeField] private Color autoCoverColor = new Color(0f, 0f, 0f, 0.72f);

    [SerializeField] private Color iconReadyColor = Color.white;
    [SerializeField] private Color iconCoolingColor = new Color(0.55f, 0.55f, 0.55f);

    [Header("기록")]
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("보스 등장까지 남은 처치 수 (처치 3/8)")]
    [SerializeField] private TMP_Text killText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text moneyText;

    [Header("보스")]
    [Tooltip("보스가 있을 때만 켜지는 묶음 (보스 HP 바의 부모)")]
    [SerializeField] private GameObject bossGroup;
    [SerializeField] private Image bossBar;

    [Header("색")]
    [SerializeField] private Color hpColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color hpLowColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("HP가 이 비율 이하면 경고 색")]
    [SerializeField, Range(0f, 1f)] private float lowHpRatio = 0.4f;

    private CooldownSlot dashDedicatedSlot;
    private TMP_Text passiveSummaryText;
    private static Sprite fallbackWhiteSprite;

    private Player player;
    private Item item;
    private Boss boss;
    private float bossSearchTimer;

    // Image.Type.Filled(Radial360)가 스프라이트 미연결 시에도 100% 작동하도록 기본 흰색 스프라이트를 생성합니다.
    private static Sprite GetFallbackSprite()
    {
        if (fallbackWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] cols = new Color[16 * 16];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            fallbackWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }
        return fallbackWhiteSprite;
    }

    void Start()
    {
        if (bossGroup != null) bossGroup.SetActive(false);

        if (autoBuildSlots) BuildSlots();

        PrepareCover(bombSlot);
        for (int i = 0; i < skillSlots.Length; i++)
        {
            PrepareCover(skillSlots[i]);
        }
        if (dashDedicatedSlot != null)
        {
            PrepareCover(dashDedicatedSlot);
        }
    }

    void Update()
    {
        UpdatePlayer();
        UpdateCooldowns();
        UpdateRecord();
        UpdateBoss();
    }

    void BuildSlots()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        GameObject bar = new GameObject("AutoSkillBar", typeof(RectTransform));
        RectTransform barRt = bar.GetComponent<RectTransform>();
        barRt.SetParent(canvas.rootCanvas.transform, false);
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(0f, 1f);
        barRt.pivot = new Vector2(0f, 1f);
        barRt.anchoredPosition = autoBarPosition;
        barRt.sizeDelta = new Vector2(autoSlotSize * 4f + autoSlotSpacing * 3f, autoSlotSize);

        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = autoSlotSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_FontAsset font = hpText != null ? hpText.font : null;

        bombSlot = CreateSlot(barRt, "BombSlot", "Q", bombIcon, "폭탄", new Color(1f, 0.45f, 0.25f), font);
        skillSlots = new[]
        {
            CreateSlot(barRt, "Slot1", "1", null, "슬롯1", new Color(0.25f, 0.8f, 1f), font),
            CreateSlot(barRt, "Slot2", "2", null, "슬롯2", new Color(0.25f, 0.8f, 1f), font)
        };
        // 부스터 대시 구매 시 E키 전용 슬롯 (1, 2번 슬롯에 중복 장착되지 않았을 때 자동 표시)
        dashDedicatedSlot = CreateSlot(barRt, "DashSlot", "E", IconOf(PartType.Dash), "대시", new Color(0f, 1f, 0.75f), font);

        // 스킬 바 아래에 현재 적용 중인 패시브 파츠 요약 텍스트 생성
        GameObject passiveObj = new GameObject("PassiveSummaryText", typeof(RectTransform));
        RectTransform passiveRt = passiveObj.GetComponent<RectTransform>();
        passiveRt.SetParent(canvas.rootCanvas.transform, false);
        passiveRt.anchorMin = new Vector2(0f, 1f);
        passiveRt.anchorMax = new Vector2(0f, 1f);
        passiveRt.pivot = new Vector2(0f, 1f);
        passiveRt.anchoredPosition = new Vector2(autoBarPosition.x + 2f, autoBarPosition.y - autoSlotSize - 8f);
        passiveRt.sizeDelta = new Vector2(500f, 36f);

        passiveSummaryText = passiveObj.AddComponent<TextMeshProUGUI>();
        if (font != null) passiveSummaryText.font = font;
        passiveSummaryText.fontSize = 20f;
        passiveSummaryText.color = new Color(0.5f, 1f, 0.75f, 0.95f);
        passiveSummaryText.alignment = TextAlignmentOptions.TopLeft;
        passiveSummaryText.raycastTarget = false;
    }

    CooldownSlot CreateSlot(RectTransform parent, string slotName, string key, Sprite sprite, string defaultLabel, Color fallbackTint, TMP_FontAsset font)
    {
        GameObject go = new GameObject(slotName, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(autoSlotSize, autoSlotSize);
        Image back = go.GetComponent<Image>();
        back.color = autoSlotBackColor;
        back.raycastTarget = false;

        Image icon = CreateImage("Icon", rt);
        icon.sprite = sprite != null ? sprite : GetFallbackSprite();
        icon.color = sprite != null ? Color.white : fallbackTint;
        icon.preserveAspect = true;

        // 슬롯 하단 스킬 이름 표시 라벨
        TMP_Text label = CreateText("LabelText", rt, font, 14f, TextAlignmentOptions.Bottom, 4f);
        label.text = defaultLabel;

        Image cover = CreateImage("Cover", rt);
        cover.sprite = GetFallbackSprite();
        cover.color = autoCoverColor;

        TMP_Text keyText = CreateText("KeyText", rt, font, autoKeyFontSize, TextAlignmentOptions.TopLeft, 3f);
        keyText.text = key;
        keyText.color = new Color(1f, 0.9f, 0.3f, 1f);

        TMP_Text time = CreateText("TimeText", rt, font, autoTimeFontSize, TextAlignmentOptions.Center, 0f);
        time.fontStyle = FontStyles.Bold;
        time.text = "";

        return new CooldownSlot { root = go, icon = icon, cover = cover, timeText = time, labelText = label };
    }

    Image CreateImage(string objName, RectTransform parent)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, autoIconPadding);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    TMP_Text CreateText(string objName, RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, float padding)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, padding);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    void UpdatePlayer()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
            if (player != null) item = player.GetComponent<Item>();
        }
        if (player == null) return;

        SetBar(hpBar, player.HpRatio);
        if (hpBar != null) hpBar.color = player.HpRatio <= lowHpRatio ? hpLowColor : hpColor;
        SetText(hpText, $"HP {player.CurrentHp}/{player.MaxHp}");

        if (bombGaugeFill != null)
        {
            bombGaugeFill.fillAmount = 1f - player.BombCooldownRatio;
        }
    }

    void UpdateCooldowns()
    {
        if (player == null) return;

        // 1. 폭탄 슬롯 갱신
        SetCooldown(bombSlot, player.BombRemaining, player.BombCooldownRatio, bombIcon != null ? Color.white : new Color(1f, 0.45f, 0.25f));

        DataManager data = DataManager.Instance;

        // 2. 퀵슬롯 1, 2번 갱신
        for (int i = 0; i < skillSlots.Length; i++)
        {
            CooldownSlot slot = skillSlots[i];
            PartType type = data != null && i < data.quickSlots.Length ? data.quickSlots[i] : PartType.None;
            bool show = DataManager.IsActivePart(type);

            if (slot.root != null && slot.root.activeSelf != show) slot.root.SetActive(show);
            if (!show) continue;

            Sprite customSprite = IconOf(type);
            SetIconAndLabel(slot, customSprite, ShortNameOf(type));

            float remain = item != null ? item.Remaining(type) : 0f;
            float ratio = item != null ? item.CooldownRatio(type) : 0f;
            Color readyCol = customSprite != null ? iconReadyColor : ColorOf(type);
            SetCooldown(slot, remain, ratio, readyCol);
        }

        // 3. E키 부스터 대시 전용 슬롯 갱신 (대시를 보유 중이고 1·2번 슬롯에 없을 때 표시)
        if (dashDedicatedSlot != null && dashDedicatedSlot.root != null)
        {
            bool ownsDash = data != null && data.HasPart(PartType.Dash);
            bool equippedInQuickSlot = data != null && (data.quickSlots[0] == PartType.Dash || data.quickSlots[1] == PartType.Dash);
            bool showDashSlot = ownsDash && !equippedInQuickSlot;

            if (dashDedicatedSlot.root.activeSelf != showDashSlot)
                dashDedicatedSlot.root.SetActive(showDashSlot);

            if (showDashSlot)
            {
                Sprite dashSp = IconOf(PartType.Dash);
                SetIconAndLabel(dashDedicatedSlot, dashSp, "대시");
                float remain = item != null ? item.Remaining(PartType.Dash) : 0f;
                float ratio = item != null ? item.CooldownRatio(PartType.Dash) : 0f;
                SetCooldown(dashDedicatedSlot, remain, ratio, dashSp != null ? iconReadyColor : ColorOf(PartType.Dash));
            }
        }

        // 4. 보유 중인 패시브 파츠 요약 텍스트 갱신
        if (passiveSummaryText != null && data != null)
        {
            List<string> pNames = new List<string>();
            if (data.HasPart(PartType.PassiveMaxHp)) pNames.Add("HP+3");
            if (data.HasPart(PartType.PassiveMoveSpeed)) pNames.Add("기동+25%");
            if (data.HasPart(PartType.PassiveCooldown)) pNames.Add("쿨감-25%");
            if (data.HasPart(PartType.PassiveInvincible)) pNames.Add("무적+1s");

            SetText(passiveSummaryText, pNames.Count > 0 ? $"패시브: {string.Join(" / ", pNames)}" : "");
        }
    }

    Sprite IconOf(PartType type)
    {
        if (partIcons == null) return null;
        foreach (PartIcon p in partIcons)
        {
            if (p.type == type) return p.icon;
        }
        return null;
    }

    string ShortNameOf(PartType type)
    {
        switch (type)
        {
            case PartType.ForceHoming: return "강제유도";
            case PartType.TimeStop: return "시간정지";
            case PartType.Reflect: return "공격반사";
            case PartType.BlackHole: return "블랙홀";
            case PartType.SlowField: return "감속장";
            case PartType.PositionSwap: return "위치스왑";
            case PartType.Shockwave: return "충격파";
            case PartType.Dash: return "대시";
            case PartType.HpBoostActive: return "HP증강";
            case PartType.EnergyShield: return "실드";
            default: return "";
        }
    }

    Color ColorOf(PartType type)
    {
        switch (type)
        {
            case PartType.ForceHoming: return new Color(1f, 0.55f, 0.2f);
            case PartType.TimeStop: return new Color(0.3f, 0.85f, 1f);
            case PartType.Reflect: return new Color(1f, 0.85f, 0.2f);
            case PartType.BlackHole: return new Color(0.65f, 0.35f, 1f);
            case PartType.SlowField: return new Color(0.3f, 0.6f, 1f);
            case PartType.PositionSwap: return new Color(0.95f, 0.4f, 0.8f);
            case PartType.Shockwave: return new Color(0.4f, 0.95f, 1f);
            case PartType.Dash: return new Color(0f, 1f, 0.75f);
            case PartType.HpBoostActive: return new Color(0.25f, 0.95f, 0.4f);
            case PartType.EnergyShield: return new Color(0.2f, 0.75f, 1f);
            default: return Color.white;
        }
    }

    void SetIconAndLabel(CooldownSlot slot, Sprite customSprite, string labelStr)
    {
        if (slot.labelText != null) SetText(slot.labelText, labelStr);

        Sprite targetSprite = customSprite != null ? customSprite : GetFallbackSprite();
        if (slot.icon != null && slot.icon.sprite != targetSprite)
        {
            slot.icon.sprite = targetSprite;
            PrepareCover(slot);
        }
    }

    void PrepareCover(CooldownSlot slot)
    {
        if (slot == null || slot.cover == null) return;

        if (slot.icon != null && slot.icon.sprite == null)
        {
            slot.icon.sprite = GetFallbackSprite();
        }

        slot.cover.sprite = (slot.icon != null && slot.icon.sprite != null) ? slot.icon.sprite : GetFallbackSprite();
        slot.cover.preserveAspect = slot.icon != null && slot.icon.preserveAspect;
        slot.cover.type = Image.Type.Filled;
        slot.cover.fillMethod = Image.FillMethod.Radial360;
        slot.cover.fillOrigin = (int)Image.Origin360.Top;
        slot.cover.fillClockwise = false;
        slot.cover.raycastTarget = false;
        slot.cover.fillAmount = 0f;
    }

    void SetCooldown(CooldownSlot slot, float remain, float ratio, Color readyColor)
    {
        if (slot == null) return;
        bool cooling = remain > 0f;

        if (slot.cover != null)
        {
            if (slot.cover.sprite == null) PrepareCover(slot);
            slot.cover.fillAmount = cooling ? Mathf.Clamp01(ratio) : 0f;
        }
        if (slot.icon != null)
        {
            slot.icon.color = cooling ? iconCoolingColor : readyColor;
        }
        SetText(slot.timeText, cooling ? Mathf.CeilToInt(remain).ToString() : "");
    }

    void UpdateRecord()
    {
        DataManager data = DataManager.Instance;
        int score = data != null ? data.currentScore : 0;
        int money = data != null ? data.money : 0;
        int seconds = Mathf.FloorToInt(StageManager.StageTime);

        SetText(stageText, $"STAGE {StageManager.CurrentStage}");
        SetText(timeText, $"{seconds / 60:00}:{seconds % 60:00}");

        int kills = Mathf.Min(Enemy.KillCount, StageManager.KillsForBoss);
        SetText(killText, StageManager.BossPhase ? "보스전" : $"처치 {kills}/{StageManager.KillsForBoss}");
        SetText(scoreText, $"점수 {score:N0}");
        SetText(moneyText, Enemy.StageMoney > 0 ? $"파편 {money:N0} (+{Enemy.StageMoney:N0})" : $"파편 {money:N0}");
    }

    void UpdateBoss()
    {
        if (boss == null)
        {
            bossSearchTimer -= Time.deltaTime;
            if (bossSearchTimer <= 0f)
            {
                bossSearchTimer = 0.5f;
                boss = FindAnyObjectByType<Boss>();
            }
        }

        bool show = boss != null;
        if (bossGroup != null && bossGroup.activeSelf != show) bossGroup.SetActive(show);
        if (show) SetBar(bossBar, boss.HpRatio);
    }

    void SetBar(Image bar, float ratio)
    {
        if (bar == null) return;

        ratio = Mathf.Clamp01(ratio);

        if (bar.type == Image.Type.Filled) bar.fillAmount = ratio;
        else bar.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
    }

    void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }
}