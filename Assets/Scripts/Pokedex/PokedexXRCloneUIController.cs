using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PokedexXRCloneUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PokedexDatabase database;
    [SerializeField] private string initialEntryId;
    [SerializeField] private bool showFirstEntryOnStart = true;

    [Header("Placement")]
    [SerializeField] private bool buildUIOnAwake = true;
    [SerializeField] private bool followCamera = true;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, -0.18f, 1.35f);
    [SerializeField] private bool enforceSingleAudioListener = true;
    [SerializeField] private bool startCollapsed = true;

    [Header("Style")]
    [SerializeField] private Vector2 panelSize = new Vector2(1040f, 580f);
    [SerializeField] private Vector2 collapsedPanelSize = new Vector2(600f, 92f);
    [SerializeField] private Vector2 anchoredScreenOffset = new Vector2(18f, -18f);
    [SerializeField] private Color backgroundColor = new Color(0.13f, 0.13f, 0.14f, 0.82f);
    [SerializeField] private Color panelColor = new Color(0.22f, 0.22f, 0.23f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.023529f, 0.180392f, 0.023529f, 1f);
    [SerializeField] private Color accentMutedColor = new Color(0.023529f, 0.180392f, 0.023529f, 0.75f);
    [SerializeField] private Color holoAccentColor = new Color(0.023529f, 0.180392f, 0.023529f, 1f);
    [SerializeField] private Color holoGlowColor = new Color(0.023529f, 0.180392f, 0.023529f, 0.10f);
    [SerializeField] private float holoBorderThickness = 6f;
    [SerializeField] private bool enableDiscoveryToast = true;
    [SerializeField] private float discoveryToastDuration = 2.8f;
    [Header("Assets")]
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite gridSprite;
    [SerializeField] private Sprite iconLeafSprite;
    [SerializeField] private Sprite iconPawSprite;
    [SerializeField] private Sprite iconCategorySprite;
    [SerializeField] private Sprite iconHeightSprite;
    [SerializeField] private Sprite iconWeightSprite;
    [SerializeField] private Sprite iconDietSprite;
    [SerializeField] private Sprite unknownSlotSprite;
    [SerializeField] private Material uiGlowMaterial;
    [SerializeField] private Color textColor = new Color(0.95f, 0.97f, 0.96f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.82f, 0.86f, 0.83f, 0.96f);
    [SerializeField] private Color lockedTextColor = new Color(0.62f, 0.67f, 0.64f, 0.85f);

    private Canvas canvas;
    private RectTransform rootRect;
    private RectTransform frameRect;
    private Image frameImage;
    private RectTransform entryListRect;
    private readonly Dictionary<string, Button> entryButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

    private TMP_Text titleText;
    private TMP_Text countText;
    private TMP_Text subtitleText;
    private TMP_Text entryNameText;
    private TMP_Text entryScientificText;
    private TMP_Text categoryValueText;
    private TMP_Text heightValueText;
    private TMP_Text weightValueText;
    private TMP_Text dietValueText;
    private TMP_Text habitatText;
    private TMP_Text behaviorText;
    private TMP_Text funFactText;
    private TMP_Text footerText;
    private TMP_Text footerCloseText;
    private TMP_Text listEmptyText;
    private TMP_Text expandText;
    private RawImage entryIcon;
    private RectTransform silhouetteFrameRect;
    private TMP_Text silhouetteLabelText;
    private GameObject emptyStatePanel;
    private readonly List<Image> speciesIconSlots = new List<Image>();

    private PokedexEntryData currentEntry;
    private bool uiBuilt;
    private bool isExpanded;
    private static TMP_FontAsset fallbackFontAsset;
    private RectTransform discoveryToastRect;
    private TMP_Text discoveryToastTitle;
    private TMP_Text discoveryToastSubtitle;
    private int lastDiscoveredCount = 0;

    public PokedexDatabase Database => database;

    private void Awake()
    {
        if (buildUIOnAwake)
        {
            BuildUIIfNeeded();
        }

        if (enforceSingleAudioListener)
        {
            EnsureSingleAudioListener();
        }

        BindDatabase(database);
    }

    private void Start()
    {
        if (!uiBuilt)
        {
            BuildUIIfNeeded();
        }

        SetExpanded(!startCollapsed);

        if (!showFirstEntryOnStart)
        {
            RefreshAll();
            return;
        }

        if (database != null && string.IsNullOrWhiteSpace(initialEntryId))
        {
            var entries = database.Entries;
            if (entries.Count > 0)
            {
                ShowEntry(entries[0], true);
            }
        }
        else if (!string.IsNullOrWhiteSpace(initialEntryId))
        {
            ShowEntryById(initialEntryId, true);
        }

        RefreshAll();
        lastDiscoveredCount = database != null ? database.DiscoveredCount : 0;
    }

    private void Update()
    {
        if (enforceSingleAudioListener)
        {
            EnsureSingleAudioListener();
        }
    }

    private void LateUpdate()
    {
        if (!followCamera || canvas == null)
        {
            return;
        }
    }

    private void OnDestroy()
    {
        if (database != null)
        {
            database.DatabaseChanged -= HandleDatabaseChanged;
        }
    }

    public void BindDatabase(PokedexDatabase newDatabase)
    {
        if (database == newDatabase)
        {
            return;
        }

        if (database != null)
        {
            database.DatabaseChanged -= HandleDatabaseChanged;
        }

        database = newDatabase;

        if (database != null)
        {
            database.DatabaseChanged += HandleDatabaseChanged;
        }

        RefreshAll();
    }

    public bool ShowEntryById(string entryId, bool markDiscovered = true)
    {
        if (database == null)
        {
            return false;
        }

        var entry = database.GetById(entryId);
        if (entry == null)
        {
            return false;
        }

        ShowEntry(entry, markDiscovered);
        return true;
    }

    public void ShowEntry(PokedexEntryData entry, bool markDiscovered = true)
    {
        currentEntry = entry;

        if (currentEntry != null && markDiscovered && database != null)
        {
            database.MarkDiscovered(currentEntry.EntryId);
        }

        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshHeader();
        RefreshList();
        RefreshDetail();
        var discovered = database != null ? database.DiscoveredCount : 0;
        if (enableDiscoveryToast && discovered > lastDiscoveredCount)
        {
            ShowDiscoveryToast();
        }
        lastDiscoveredCount = discovered;
        RefreshFooterIcons();
    }

    private void RefreshFooterIcons()
    {
        if (speciesIconSlots == null || speciesIconSlots.Count == 0 || database == null) return;
        var entries = database.Entries;
        for (int i = 0; i < speciesIconSlots.Count; i++)
        {
            var img = speciesIconSlots[i];
            if (i < entries.Count)
            {
                var entry = entries[i];
                if (database.IsDiscovered(entry.EntryId))
                {
                    img.color = Color.white;
                    img.sprite = entry.Icon != null ? SpriteFromTexture(entry.Icon) : img.sprite;
                }
                else
                {
                    img.color = new Color(0f, 0f, 0f, 0.12f);
                    if (unknownSlotSprite != null) img.sprite = unknownSlotSprite;
                }
            }
            else
            {
                img.color = new Color(0f, 0f, 0f, 0.12f);
                if (unknownSlotSprite != null) img.sprite = unknownSlotSprite;
            }
        }
    }

    private Sprite SpriteFromTexture(Texture tex)
    {
        if (tex == null) return null;
        try
        {
            var tex2D = tex as Texture2D;
            if (tex2D == null) return null;
            return Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f));
        }
        catch
        {
            return null;
        }
    }

    private int GetEntryIndex(PokedexEntryData entry)
    {
        if (database == null || entry == null) return -1;
        var list = database.Entries;
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].EntryId, entry.EntryId, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }
        return -1;
    }

    private void HandleDatabaseChanged()
    {
        RefreshAll();
    }

    private void ToggleExpanded()
    {
        SetExpanded(!isExpanded);
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (rootRect != null)
        {
            rootRect.sizeDelta = expanded ? panelSize : collapsedPanelSize;
        }

        if (frameRect != null)
        {
            frameRect.sizeDelta = expanded ? panelSize : collapsedPanelSize;
        }

        if (frameImage != null)
        {
            frameImage.color = expanded ? backgroundColor : Color.clear;
        }

        if (entryListRect != null)
        {
            entryListRect.gameObject.SetActive(expanded);
        }

        if (titleText != null) titleText.gameObject.SetActive(true);
        if (countText != null) countText.gameObject.SetActive(expanded);
        if (subtitleText != null) subtitleText.gameObject.SetActive(expanded);
        if (expandText != null) expandText.gameObject.SetActive(true);
        if (entryNameText != null) entryNameText.gameObject.SetActive(expanded);
        if (entryScientificText != null) entryScientificText.gameObject.SetActive(expanded);
        if (categoryValueText != null) categoryValueText.gameObject.SetActive(expanded);
        if (heightValueText != null) heightValueText.gameObject.SetActive(expanded);
        if (weightValueText != null) weightValueText.gameObject.SetActive(expanded);
        if (dietValueText != null) dietValueText.gameObject.SetActive(expanded);
        if (habitatText != null) habitatText.gameObject.SetActive(expanded);
        if (behaviorText != null) behaviorText.gameObject.SetActive(expanded);
        if (funFactText != null) funFactText.gameObject.SetActive(expanded);
        if (entryIcon != null) entryIcon.gameObject.SetActive(expanded);
        if (silhouetteFrameRect != null) silhouetteFrameRect.gameObject.SetActive(expanded);
        if (footerText != null) footerText.gameObject.SetActive(expanded);
        if (footerCloseText != null) footerCloseText.gameObject.SetActive(expanded);
        if (emptyStatePanel != null) emptyStatePanel.SetActive(expanded && currentEntry == null);

        if (subtitleText != null)
        {
            subtitleText.text = "XR Device Simulator style clone";
        }

        if (expandText != null)
        {
            expandText.text = expanded ? "Close" : "Open";
        }
    }

    private void BuildUIIfNeeded()
    {
        if (uiBuilt)
        {
            return;
        }

        EnsurePlaceholderSprites();

        var root = new GameObject("PokedexXRCloneUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        canvas.worldCamera = null;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.35f;

        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredScreenOffset;
        rootRect.sizeDelta = startCollapsed ? collapsedPanelSize : panelSize;
        rootRect.localScale = Vector3.one;

        EnsureEventSystemExists();

        var frame = CreatePanel(rootRect, "Frame", backgroundColor, frameSprite, true);
        frameRect = frame;
        frameImage = frame.GetComponent<Image>();
        Anchor(frameRect, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        frameRect.pivot = new Vector2(0f, 1f);
        frameRect.sizeDelta = startCollapsed ? collapsedPanelSize : panelSize;
        AddShadow(frame);
        AddHoloBorder(frame);

        if (enableDiscoveryToast)
        {
            CreateDiscoveryToast(rootRect);
        }

        BuildHeader(frame);
        BuildBody(frame);
        BuildFooter(frame);

        SetExpanded(!startCollapsed);

        uiBuilt = true;
    }

    private void BuildHeader(RectTransform parent)
    {
        var header = CreatePanel(parent, "Header", panelColor, panelSprite, true);
        Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-4f, -4f));

        titleText = CreateText(header, "Title", 14, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, holoAccentColor);
        Anchor(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(10f, 8f), new Vector2(-4f, -4f));
        titleText.text = "SCAN COMPLETE";

        countText = CreateText(header, "Count", 12, FontStyles.Bold, TextAlignmentOptions.MidlineRight, holoAccentColor);
        Anchor(countText.rectTransform, new Vector2(0.58f, 0.48f), new Vector2(0.84f, 1f), new Vector2(2f, 2f), new Vector2(-4f, -2f));

        subtitleText = CreateText(header, "Subtitle", 10, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(subtitleText.rectTransform, new Vector2(0.58f, 0f), new Vector2(0.84f, 0.48f), new Vector2(2f, 0f), new Vector2(-4f, -2f));
        subtitleText.text = "XR Device Simulator style clone";

        var togglePanel = CreatePanel(header, "ToggleButton", accentMutedColor);
        Anchor(togglePanel, new Vector2(0.84f, 0.10f), new Vector2(1f, 0.90f), new Vector2(0f, 0f), new Vector2(-4f, 0f));

        var toggleButton = togglePanel.gameObject.AddComponent<Button>();
        toggleButton.targetGraphic = togglePanel.GetComponent<Image>();
        toggleButton.onClick.AddListener(ToggleExpanded);

        expandText = CreateText(togglePanel, "ToggleText", 11, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(expandText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 1f), new Vector2(-2f, -1f));
        expandText.text = "Open";
    }

    private void BuildBody(RectTransform parent)
    {
        var body = new GameObject("Body", typeof(RectTransform));
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.SetParent(parent, false);
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(8f, 14f);
        bodyRect.offsetMax = new Vector2(-8f, -24f);

        var listPanel = CreatePanel(bodyRect, "ListPanel", panelColor, panelSprite, true);
        Anchor(listPanel, new Vector2(0f, 0f), new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero);

        var listTitle = CreateText(listPanel, "ListTitle", 13, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, accentColor);
        Anchor(listTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -18f));
        listTitle.text = "Entries";

        var listHint = CreateText(listPanel, "ListHint", 11, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(listHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -14f), new Vector2(-8f, -24f));
        listHint.text = "Select one";

        var scrollView = CreateScrollView(listPanel, out entryListRect);
        Anchor(scrollView.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 4f), new Vector2(-6f, -22f));

        listEmptyText = CreateText(entryListRect, "Empty", 12, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, lockedTextColor);
        Anchor(listEmptyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -8f), new Vector2(-6f, -8f));
        listEmptyText.text = "No entries";

        var detailPanel = CreatePanel(bodyRect, "DetailPanel", panelColor, panelSprite, true);
        Anchor(detailPanel, new Vector2(0.22f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        entryNameText = CreateText(detailPanel, "Name", 19, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(entryNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -16f));
        entryNameText.text = "No animal selected";

        entryScientificText = CreateText(detailPanel, "Scientific", 11, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(entryScientificText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -16f), new Vector2(-8f, -28f));
        entryScientificText.text = "Target an animal to fill this panel.";

        var silhouetteCard = CreatePanel(detailPanel, "SilhouetteCard", new Color(0f, 0f, 0f, 0.08f), panelSprite, true);
        Anchor(silhouetteCard, new Vector2(0f, 0.16f), new Vector2(0.36f, 1f), new Vector2(8f, 0f), new Vector2(-6f, -6f));

        silhouetteFrameRect = CreatePanel(silhouetteCard, "SilhouetteFrame", new Color(0f, 0f, 0f, 0.06f), panelSprite, true);
        Anchor(silhouetteFrameRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));

        var silhouetteBox = CreatePanel(silhouetteFrameRect, "SilhouetteBox", Color.clear);
        Anchor(silhouetteBox, new Vector2(0f, 0.06f), new Vector2(1f, 0.92f), new Vector2(10f, 6f), new Vector2(-10f, -6f));

        var silhouetteGuide = CreateText(silhouetteBox, "SilhouetteGuide", 11, FontStyles.Bold, TextAlignmentOptions.Center, holoAccentColor);
        Anchor(silhouetteGuide.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
        silhouetteGuide.text = "WOLF SILHOUETTE\nPNG PLACEHOLDER";

        silhouetteLabelText = CreateText(silhouetteCard, "SilhouetteLabel", 10, FontStyles.Normal, TextAlignmentOptions.Center, mutedTextColor);
        Anchor(silhouetteLabelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.10f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        silhouetteLabelText.text = "Add your PNG here";

        var infoColumn = CreatePanel(detailPanel, "InfoColumn", new Color(0f, 0f, 0f, 0f));
        Anchor(infoColumn, new Vector2(0.36f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-6f, 0f));

        var categoryCard = CreatePanel(infoColumn, "CategoryCard", panelColor, panelSprite, true);
        Anchor(categoryCard, new Vector2(0f, 0.76f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -6f));
        var categoryTitle = CreateText(categoryCard, "CategoryTitle", 12, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(categoryTitle.rectTransform, new Vector2(0.18f, 0.54f), new Vector2(1f, 1f), new Vector2(4f, -2f), new Vector2(-8f, -4f));
        categoryTitle.text = "CATEGORY";
        categoryValueText = CreateText(categoryCard, "CategoryValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(categoryValueText.rectTransform, new Vector2(0.18f, 0f), new Vector2(1f, 0.55f), new Vector2(4f, 2f), new Vector2(-8f, -2f));
        categoryValueText.text = "Pack Hunter";
        var categoryIcon = CreatePanel(categoryCard, "CategoryIcon", holoAccentColor);
        Anchor(categoryIcon, new Vector2(0f, 0f), new Vector2(0.14f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));

        var statsCard = CreatePanel(infoColumn, "StatsCard", panelColor, panelSprite, true);
        Anchor(statsCard, new Vector2(0f, 0.36f), new Vector2(1f, 0.72f), new Vector2(0f, 0f), new Vector2(0f, -6f));

        var heightLabel = CreateText(statsCard, "HeightLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(heightLabel.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.42f, 1f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        heightLabel.text = "HEIGHT";
        heightValueText = CreateText(statsCard, "HeightValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(heightValueText.rectTransform, new Vector2(0.42f, 0.68f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        heightValueText.text = "0.80 m";

        var weightLabel = CreateText(statsCard, "WeightLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(weightLabel.rectTransform, new Vector2(0.04f, 0.42f), new Vector2(0.42f, 0.68f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        weightLabel.text = "WEIGHT";
        weightValueText = CreateText(statsCard, "WeightValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(weightValueText.rectTransform, new Vector2(0.42f, 0.42f), new Vector2(1f, 0.68f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        weightValueText.text = "45 - 55 kg";

        var dietLabel = CreateText(statsCard, "DietLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(dietLabel.rectTransform, new Vector2(0.04f, 0.16f), new Vector2(0.42f, 0.42f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        dietLabel.text = "DIET";
        dietValueText = CreateText(statsCard, "DietValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(dietValueText.rectTransform, new Vector2(0.42f, 0.16f), new Vector2(1f, 0.42f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        dietValueText.text = "Carnivore";

        var bottomLeftCard = CreatePanel(infoColumn, "BottomLeftCard", panelColor, panelSprite, true);
        Anchor(bottomLeftCard, new Vector2(0f, 0.0f), new Vector2(0.49f, 0.34f), new Vector2(0f, 0f), new Vector2(-3f, -4f));
        var habitatLabel = CreateText(bottomLeftCard, "HabitatLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(habitatLabel.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        habitatLabel.text = "HABITAT";
        habitatText = CreateText(bottomLeftCard, "HabitatText", 10, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(habitatText.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.62f), new Vector2(8f, 0f), new Vector2(-8f, -2f));
        habitatText.text = "Forests, mountains and northern regions.";

        var behaviorLabel = CreateText(bottomLeftCard, "BehaviorLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(behaviorLabel.rectTransform, new Vector2(0f, 0.00f), new Vector2(1f, 0.30f), new Vector2(8f, 2f), new Vector2(-8f, -2f));
        behaviorLabel.text = "BEHAVIOR";
        behaviorText = CreateText(bottomLeftCard, "BehaviorText", 10, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(behaviorText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.30f), new Vector2(8f, 0f), new Vector2(-8f, -2f));
        behaviorText.text = "Highly social. Lives and hunts in packs.";

        var bottomRightCard = CreatePanel(infoColumn, "BottomRightCard", panelColor, panelSprite, true);
        Anchor(bottomRightCard, new Vector2(0.51f, 0.0f), new Vector2(1f, 0.34f), new Vector2(3f, 0f), new Vector2(0f, -4f));
        var funFactLabel = CreateText(bottomRightCard, "FunFactLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(funFactLabel.rectTransform, new Vector2(0f, 0.60f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        funFactLabel.text = "FUN FACT";
        funFactText = CreateText(bottomRightCard, "FunFactText", 10, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(funFactText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.60f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        funFactText.text = "A wolf's howl can be heard up to 10 kilometers away.";

        emptyStatePanel = CreatePanel(detailPanel, "EmptyState", new Color(0f, 0f, 0f, 0.10f)).gameObject;
        Anchor(emptyStatePanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
        var emptyLabel = CreateText(emptyStatePanel.transform, "EmptyLabel", 14, FontStyles.Bold, TextAlignmentOptions.Center, lockedTextColor);
        Anchor(emptyLabel.rectTransform, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.66f), Vector2.zero, Vector2.zero);
        emptyLabel.text = "NO ANIMAL SELECTED";
    }

    private void BuildFooter(RectTransform parent)
    {
        var footer = CreatePanel(parent, "Footer", backgroundColor, panelSprite, true);
        Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 28f));

        footerText = CreateText(footer, "FooterText", 10, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(footerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.46f, 1f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        footerText.text = "SPECIES DISCOVERED";

        var closeButton = CreatePanel(footer, "CloseButton", accentMutedColor);
        Anchor(closeButton, new Vector2(0.90f, 0.14f), new Vector2(1f, 0.86f), new Vector2(0f, 0f), new Vector2(-8f, 0f));
        var closeButtonGraphic = closeButton.GetComponent<Image>();
        var closeButtonBehaviour = closeButton.gameObject.AddComponent<Button>();
        closeButtonBehaviour.targetGraphic = closeButtonGraphic;
        closeButtonBehaviour.onClick.AddListener(ToggleExpanded);

        footerCloseText = CreateText(closeButton, "CloseText", 14, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(footerCloseText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        footerCloseText.text = "X";

        // Species discovered icon row
        var iconRow = new GameObject("SpeciesRow", typeof(RectTransform));
        var iconRowRect = iconRow.GetComponent<RectTransform>();
        iconRowRect.SetParent(footer, false);
        iconRowRect.anchorMin = new Vector2(0f, 0f);
        iconRowRect.anchorMax = new Vector2(1f, 0f);
        iconRowRect.pivot = new Vector2(0f, 0f);
        iconRowRect.anchoredPosition = new Vector2(8f, 5f);
        iconRowRect.sizeDelta = new Vector2(0f, 32f);

        var layout = iconRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(4, 4, 4, 2);

        int slotCount = 8;
        for (int i = 0; i < slotCount; i++)
        {
            var slotObj = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slotObj.transform.SetParent(iconRowRect, false);
            var img = slotObj.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.12f);
            if (unknownSlotSprite != null)
            {
                img.sprite = unknownSlotSprite;
                img.type = Image.Type.Sliced;
            }
            var rt = slotObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(36f, 28f);
            speciesIconSlots.Add(img);
        }
    }

    private void AddHoloBorder(RectTransform target)
    {
        if (target == null) return;

        var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(target, false);
        var glowRect = glow.GetComponent<RectTransform>();
        glowRect.SetAsFirstSibling();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-holoBorderThickness, -holoBorderThickness);
        glowRect.offsetMax = new Vector2(holoBorderThickness, holoBorderThickness);
        var glowImg = glow.GetComponent<Image>();
        glowImg.color = holoGlowColor;
        glowImg.raycastTarget = false;

        var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(target, false);
        var borderRect = border.GetComponent<RectTransform>();
        borderRect.SetAsLastSibling();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        var borderImg = border.GetComponent<Image>();
        borderImg.color = new Color(holoAccentColor.r, holoAccentColor.g, holoAccentColor.b, 0.12f);
        borderImg.raycastTarget = false;
        border.gameObject.AddComponent<Outline>().effectColor = holoAccentColor;
    }

    private void EnsurePlaceholderSprites()
    {
        if (frameSprite == null)
        {
            frameSprite = CreatePlaceholderSlicedSprite("pf_frame", 64, 64, new Color(0f, 0.05f, 0.06f, 0.9f), 10, holoAccentColor);
        }

        if (panelSprite == null)
        {
            panelSprite = CreatePlaceholderSlicedSprite("pf_panel", 48, 48, new Color(0f, 0.06f, 0.07f, 0.8f), 8, new Color(0f, 0.05f, 0.06f, 0.6f));
        }

        if (gridSprite == null)
        {
            gridSprite = CreatePlaceholderSlicedSprite("pf_grid", 32, 32, new Color(0f, 0f, 0f, 0f), 4, holoAccentColor * 0.35f);
        }

        if (unknownSlotSprite == null)
        {
            unknownSlotSprite = CreatePlaceholderSlicedSprite("pf_slot", 32, 24, new Color(0f, 0f, 0f, 0.12f), 4, new Color(0f,0f,0f,0f));
        }

        if (iconLeafSprite == null) iconLeafSprite = CreatePlaceholderIcon("pf_leaf", 32, 32, holoAccentColor);
        if (iconPawSprite == null) iconPawSprite = CreatePlaceholderIcon("pf_paw", 32, 32, holoAccentColor * 0.9f);
        if (iconCategorySprite == null) iconCategorySprite = CreatePlaceholderIcon("pf_cat", 32, 32, holoAccentColor * 0.85f);
    }

    private Sprite CreatePlaceholderSlicedSprite(string name, int w, int h, Color fill, int borderPx, Color borderColor)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, fill);
            }
        }

        // draw border
        for (int i = 0; i < borderPx; i++)
        {
            for (int x = i; x < w - i; x++)
            {
                tex.SetPixel(x, i, borderColor);
                tex.SetPixel(x, h - 1 - i, borderColor);
            }
            for (int y = i; y < h - i; y++)
            {
                tex.SetPixel(i, y, borderColor);
                tex.SetPixel(w - 1 - i, y, borderColor);
            }
        }

        tex.Apply();
        var border = new Vector4(borderPx, borderPx, borderPx, borderPx);
        var spr = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
        spr.name = name;
        return spr;
    }

    private Sprite CreatePlaceholderIcon(string name, int w, int h, Color color)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var bg = new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, bg);
            }
        }

        // simple center glyph: small rectangle
        int pad = Mathf.Max(2, w / 6);
        for (int y = pad; y < h - pad; y++)
        {
            for (int x = pad; x < w - pad; x++)
            {
                if (x % 3 == 0 && y % 3 == 0) // slight pattern
                    tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        spr.name = name;
        return spr;
    }

    private void CreateDiscoveryToast(Transform root)
    {
        var toast = CreatePanel(root, "DiscoveryToast", new Color(0f, 0f, 0f, 0.0f));
        discoveryToastRect = toast;
        discoveryToastRect.anchorMin = new Vector2(0f, 1f);
        discoveryToastRect.anchorMax = new Vector2(0f, 1f);
        discoveryToastRect.pivot = new Vector2(0f, 1f);
        discoveryToastRect.anchoredPosition = new Vector2(0f, -48f);
        discoveryToastRect.sizeDelta = new Vector2(260f, 64f);

        var panel = CreatePanel(discoveryToastRect, "ToastPanel", panelColor, panelSprite, true);
        Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        panel.gameObject.AddComponent<Outline>().effectColor = holoAccentColor;

        discoveryToastTitle = CreateText(panel, "ToastTitle", 13, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, holoAccentColor);
        Anchor(discoveryToastTitle.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(12f, 6f), new Vector2(-8f, -6f));
        discoveryToastTitle.text = "NEW SPECIES\nDISCOVERED";

        discoveryToastSubtitle = CreateText(panel, "ToastSub", 10, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(discoveryToastSubtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(12f, 2f), new Vector2(-8f, 4f));
        discoveryToastSubtitle.text = "DATABASE UPDATED";

        discoveryToastRect.gameObject.SetActive(false);
    }

    private void ShowDiscoveryToast()
    {
        if (discoveryToastRect == null) return;
        discoveryToastRect.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HideToastAfter(discoveryToastDuration));
    }

    private IEnumerator HideToastAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (discoveryToastRect != null)
        {
            discoveryToastRect.gameObject.SetActive(false);
        }
    }

    private void RefreshHeader()
    {
        if (countText != null)
        {
            if (currentEntry != null)
            {
                var idx = GetEntryIndex(currentEntry);
                countText.text = idx > 0 ? $"NO. {idx.ToString("D3")}" : string.Empty;
            }
            else
            {
                var discovered = database != null ? database.DiscoveredCount : 0;
                var total = database != null ? database.TotalEntries : 0;
                countText.text = $"{discovered}/{total}";
            }
        }

        if (subtitleText != null)
        {
            subtitleText.text = currentEntry == null ? "Waiting for discovery" : "Entry open";
        }
    }

    private void RefreshList()
    {
        if (entryListRect == null)
        {
            return;
        }

        foreach (Transform child in entryListRect)
        {
            if (listEmptyText != null && child == listEmptyText.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        entryButtons.Clear();

        if (database == null || database.Entries.Count == 0)
        {
            if (listEmptyText != null)
            {
                listEmptyText.gameObject.SetActive(true);
            }
            return;
        }

        if (listEmptyText != null)
        {
            listEmptyText.gameObject.SetActive(false);
        }

        foreach (var entry in database.Entries)
        {
            CreateEntryButton(entry);
        }

        HighlightCurrentEntry();
    }

    private void RefreshDetail()
    {
        bool hasEntry = currentEntry != null;
        if (emptyStatePanel != null)
        {
            emptyStatePanel.SetActive(!hasEntry);
        }

        if (!hasEntry)
        {
            if (entryNameText != null) entryNameText.text = "No animal selected";
            if (entryScientificText != null) entryScientificText.text = "Target an animal to open its profile.";
            if (categoryValueText != null) categoryValueText.text = string.Empty;
            if (heightValueText != null) heightValueText.text = string.Empty;
            if (weightValueText != null) weightValueText.text = string.Empty;
            if (dietValueText != null) dietValueText.text = string.Empty;
            if (habitatText != null) habitatText.text = string.Empty;
            if (behaviorText != null) behaviorText.text = string.Empty;
            if (funFactText != null) funFactText.text = string.Empty;
            if (entryIcon != null)
            {
                entryIcon.texture = null;
                entryIcon.color = accentMutedColor;
            }
            if (silhouetteLabelText != null) silhouetteLabelText.text = "Add your PNG here";
            if (footerText != null) footerText.text = "Target an animal to fill the detail panel.";
            return;
        }

        if (entryNameText != null) entryNameText.text = currentEntry.CommonName;
        if (entryScientificText != null) entryScientificText.text = currentEntry.ScientificName;
        if (categoryValueText != null) categoryValueText.text = currentEntry.Category;
        if (heightValueText != null) heightValueText.text = string.IsNullOrWhiteSpace(currentEntry.Height) ? "Unknown" : currentEntry.Height;
        if (weightValueText != null) weightValueText.text = string.IsNullOrWhiteSpace(currentEntry.Weight) ? "Unknown" : currentEntry.Weight;
        if (dietValueText != null) dietValueText.text = currentEntry.Diet;
        if (habitatText != null) habitatText.text = string.IsNullOrWhiteSpace(currentEntry.Habitat) ? "Unknown habitat." : currentEntry.Habitat;
        if (behaviorText != null) behaviorText.text = BuildBehaviorText(currentEntry);
        if (funFactText != null) funFactText.text = BuildFunFactText(currentEntry);
        if (entryIcon != null)
        {
            entryIcon.texture = currentEntry.Icon;
            entryIcon.color = currentEntry.Icon != null ? Color.white : accentMutedColor;
        }
        if (silhouetteLabelText != null) silhouetteLabelText.text = currentEntry.CommonName.ToUpperInvariant();
        if (footerText != null) footerText.text = "Future laser-pointer hook: call ShowEntry(...) when an animal is targeted.";

        HighlightCurrentEntry();
    }

    private void HighlightCurrentEntry()
    {
        foreach (var pair in entryButtons)
        {
            var isSelected = currentEntry != null && string.Equals(pair.Key, currentEntry.EntryId, StringComparison.OrdinalIgnoreCase);
            var isDiscovered = database != null && database.IsDiscovered(pair.Key);
            var buttonImage = pair.Value.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? accentColor : isDiscovered ? panelColor : backgroundColor;
            }

            var text = pair.Value.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.color = isSelected ? Color.white : isDiscovered ? textColor : lockedTextColor;
            }
        }
    }

    private void CreateEntryButton(PokedexEntryData entry)
    {
        var buttonObject = new GameObject($"Entry_{entry.EntryId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(entryListRect, false);

        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = backgroundColor;

        var layout = buttonObject.GetComponent<LayoutElement>();
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;

        var title = CreateText(buttonObject.transform, "Label", 12, FontStyles.Bold, TextAlignmentOptions.Left, textColor);
        Anchor(title.rectTransform, new Vector2(0f, 0.36f), new Vector2(0.82f, 0.88f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        title.text = entry.CommonName;

        var subtitle = CreateText(buttonObject.transform, "SubLabel", 10, FontStyles.Normal, TextAlignmentOptions.Left, mutedTextColor);
        Anchor(subtitle.rectTransform, new Vector2(0f, 0.08f), new Vector2(0.82f, 0.44f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        subtitle.text = database != null && database.IsDiscovered(entry.EntryId) ? entry.Category : "Unknown";

        var status = CreatePanel(buttonObject.transform, "StatusChip", accentMutedColor);
        Anchor(status, new Vector2(0.84f, 0.16f), new Vector2(1f, 0.84f), new Vector2(0f, 0f), new Vector2(-8f, 0f));
        var statusText = CreateText(status, "Status", 9, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 2f), new Vector2(-4f, -2f));
        statusText.text = database != null && database.IsDiscovered(entry.EntryId) ? "Seen" : "Locked";

        var localEntry = entry;
        buttonObject.GetComponent<Button>().onClick.AddListener(() => ShowEntry(localEntry, false));

        entryButtons[entry.EntryId] = buttonObject.GetComponent<Button>();
    }

    private string BuildBehaviorText(PokedexEntryData entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.BehaviorNotes))
        {
            return entry.BehaviorNotes;
        }

        if (!string.IsNullOrWhiteSpace(entry.ObservationTips))
        {
            return entry.ObservationTips;
        }

        return "Behavior details unavailable.";
    }

    private string BuildFunFactText(PokedexEntryData entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.FunFact))
        {
            return entry.FunFact;
        }

        if (entry.Facts != null && entry.Facts.Count > 0)
        {
            foreach (var fact in entry.Facts)
            {
                if (!string.IsNullOrWhiteSpace(fact))
                {
                    return fact.Trim();
                }
            }
        }

        return "No fun fact available.";
    }

    private ScrollRect CreateScrollView(RectTransform parent, out RectTransform contentRoot)
    {
        var scrollObject = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        var scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.SetParent(parent, false);

        scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.14f);
        scrollObject.GetComponent<Mask>().showMaskGraphic = false;

        var scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 12f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.SetParent(scrollObject.transform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentRoot = content.GetComponent<RectTransform>();
        contentRoot.SetParent(viewportRect, false);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.offsetMin = new Vector2(4f, 4f);
        contentRoot.offsetMax = new Vector2(-4f, -4f);

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = viewportRect;
        scroll.content = contentRoot;
        return scroll;
    }

    private TMP_Text CreateText(Transform parent, string objectName, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = GetFallbackFontAsset();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_FontAsset GetFallbackFontAsset()
    {
        if (fallbackFontAsset != null)
        {
            return fallbackFontAsset;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            fallbackFontAsset = TMP_Settings.defaultFontAsset;
            return fallbackFontAsset;
        }

        var builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (builtinFont != null)
        {
            fallbackFontAsset = TMP_FontAsset.CreateFontAsset(builtinFont);
            return fallbackFontAsset;
        }

        return null;
    }

    private RectTransform CreatePanel(Transform parent, string objectName, Color color, Sprite sprite = null, bool sliced = false)
    {
        var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        var img = panelObject.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            if (sliced)
            {
                img.type = Image.Type.Sliced;
            }
        }
        if (uiGlowMaterial != null)
        {
            img.material = uiGlowMaterial;
        }
        return panelObject.GetComponent<RectTransform>();
    }

    private RawImage CreateRawImage(Transform parent, string objectName, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<RawImage>();
        image.color = color;
        return image;
    }

    private static void AddShadow(RectTransform rectTransform)
    {
        var shadow = rectTransform.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(5f, -5f);
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            eventSystemObject.AddComponent(inputSystemModuleType);
        }
        else
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }

    private static void EnsureSingleAudioListener()
    {
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listeners.Length <= 1)
        {
            return;
        }

        var preferred = Camera.main != null ? Camera.main.GetComponent<AudioListener>() : null;
        if (preferred == null)
        {
            preferred = listeners[0];
        }

        foreach (var listener in listeners)
        {
            if (listener != preferred)
            {
                listener.enabled = false;
            }
        }
    }
}
