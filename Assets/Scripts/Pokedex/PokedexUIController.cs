using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PokedexUIController : MonoBehaviour
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
    [SerializeField] private Vector2 anchoredScreenOffset = new Vector2(24f, -24f);

    [Header("Style")]
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 320f);
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.10f, 0.08f, 0.92f);
    [SerializeField] private Color panelColor = new Color(0.11f, 0.16f, 0.13f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.35f, 0.88f, 0.56f, 1f);
    [SerializeField] private Color accentMutedColor = new Color(0.22f, 0.52f, 0.34f, 1f);
    [SerializeField] private Color textColor = new Color(0.93f, 0.97f, 0.94f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.76f, 0.84f, 0.78f, 0.94f);
    [SerializeField] private Color lockedTextColor = new Color(0.63f, 0.70f, 0.65f, 0.72f);

    private Canvas canvas;
    private RectTransform canvasRoot;
    private RectTransform catalogContent;
    private readonly Dictionary<string, Button> entryButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

    private TMP_Text headerCountText;
    private TMP_Text headerHintText;
    private TMP_Text detailNameText;
    private TMP_Text detailScientificText;
    private TMP_Text detailStatusText;
    private TMP_Text detailMetaText;
    private TMP_Text detailDescriptionText;
    private TMP_Text detailBehaviorText;
    private TMP_Text detailTipsText;
    private TMP_Text detailFactsText;
    private TMP_Text catalogEmptyText;
    private RawImage detailIconImage;
    private GameObject noSelectionPanel;

    private PokedexEntryData currentEntry;
    private bool uiBuilt;

    public PokedexDatabase Database => database;

    private void Awake()
    {
        if (buildUIOnAwake)
        {
            EnsureUIBuilt();
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
            EnsureUIBuilt();
        }

        if (database != null && string.IsNullOrWhiteSpace(initialEntryId))
        {
            var entries = database.Entries;
            if (entries.Count > 0)
            {
                currentEntry = entries[0];
                database.MarkDiscovered(currentEntry.EntryId);
            }
        }

        RefreshCatalog();

        if (currentEntry == null)
        {
            if (!string.IsNullOrWhiteSpace(initialEntryId))
            {
                ShowEntryById(initialEntryId, false);
            }
            else if (showFirstEntryOnStart && database != null)
            {
                var entries = database.Entries;
                if (entries.Count > 0)
                {
                    ShowEntry(entries[0], false);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!followCamera || canvas == null || canvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        var transformCache = canvas.transform;
        if (transformCache.parent != mainCamera.transform)
        {
            transformCache.SetParent(mainCamera.transform, false);
            transformCache.localPosition = cameraOffset;
            transformCache.localRotation = Quaternion.identity;
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
        RefreshCatalog();
        RefreshDetail();
    }

    public void RefreshCatalog()
    {
        if (!uiBuilt)
        {
            return;
        }

        ClearCatalogButtons();

        if (database == null || database.Entries.Count == 0)
        {
            if (catalogEmptyText != null)
            {
                catalogEmptyText.gameObject.SetActive(true);
                catalogEmptyText.text = "Add animal entries to a PokedexDatabase asset to populate this list.";
            }

            HighlightCurrentSelection();
            return;
        }

        if (catalogEmptyText != null)
        {
            catalogEmptyText.gameObject.SetActive(false);
        }

        foreach (var entry in database.Entries)
        {
            var button = CreateCatalogButton(entry);
            entryButtons[entry.EntryId] = button;
        }

        HighlightCurrentSelection();
    }

    private void RefreshHeader()
    {
        if (headerCountText != null)
        {
            var discovered = database != null ? database.DiscoveredCount : 0;
            var total = database != null ? database.TotalEntries : 0;
            headerCountText.text = $"Discovered {discovered}/{total}";
        }

        if (headerHintText != null)
        {
            headerHintText.text = currentEntry == null
                ? "Target an animal to reveal its entry."
                : "Entry locked in. Use the list to review discoveries.";
        }
    }

    private void RefreshDetail()
    {
        if (!uiBuilt)
        {
            return;
        }

        var hasEntry = currentEntry != null;
        if (noSelectionPanel != null)
        {
            noSelectionPanel.SetActive(!hasEntry);
        }

        if (!hasEntry)
        {
            SetText(detailNameText, "No animal selected");
            SetText(detailScientificText, "Target an animal to open its profile.");
            SetText(detailStatusText, "Waiting for discovery");
            SetText(detailMetaText, string.Empty);
            SetText(detailDescriptionText, string.Empty);
            SetText(detailBehaviorText, string.Empty);
            SetText(detailTipsText, string.Empty);
            SetText(detailFactsText, string.Empty);

            if (detailIconImage != null)
            {
                detailIconImage.texture = null;
                detailIconImage.color = accentMutedColor;
            }

            return;
        }

        SetText(detailNameText, currentEntry.CommonName);
        SetText(detailScientificText, currentEntry.ScientificName);
        SetText(detailStatusText, database != null && database.IsDiscovered(currentEntry.EntryId) ? "Discovered" : "New discovery");
        SetText(detailMetaText, BuildMetaLine(currentEntry));
        SetText(detailDescriptionText, currentEntry.ShortDescription);
        SetText(detailBehaviorText, currentEntry.BehaviorNotes);
        SetText(detailTipsText, currentEntry.ObservationTips);
        SetText(detailFactsText, BuildFactsText(currentEntry));

        if (detailIconImage != null)
        {
            detailIconImage.texture = currentEntry.Icon;
            detailIconImage.color = currentEntry.Icon != null ? Color.white : accentMutedColor;
        }

        HighlightCurrentSelection();
    }

    private void HighlightCurrentSelection()
    {
        foreach (var pair in entryButtons)
        {
            var isSelected = currentEntry != null && string.Equals(pair.Key, currentEntry.EntryId, StringComparison.OrdinalIgnoreCase);
            var isDiscovered = database != null && database.IsDiscovered(pair.Key);

            var image = pair.Value.GetComponent<Image>();
            if (image != null)
            {
                image.color = isSelected ? accentColor : isDiscovered ? panelColor : backgroundColor;
            }

            var text = pair.Value.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.color = isSelected ? Color.white : isDiscovered ? textColor : lockedTextColor;
            }
        }
    }

    private void HandleDatabaseChanged()
    {
        RefreshAll();
    }

    private void EnsureUIBuilt()
    {
        if (uiBuilt)
        {
            return;
        }

        var existingCanvas = GetComponentInChildren<Canvas>(true);
        if (existingCanvas != null)
        {
            canvas = existingCanvas;
            canvasRoot = canvas.transform as RectTransform;
            uiBuilt = true;
            return;
        }

        var rootObject = new GameObject("PokedexUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        rootObject.transform.SetParent(transform, false);

        canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.overrideSorting = true;
        canvas.worldCamera = null;

        var scaler = rootObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot = rootObject.GetComponent<RectTransform>();
        canvasRoot.sizeDelta = panelSize;
        canvasRoot.anchorMin = new Vector2(1f, 1f);
        canvasRoot.anchorMax = new Vector2(1f, 1f);
        canvasRoot.pivot = new Vector2(1f, 1f);
        canvasRoot.anchoredPosition = anchoredScreenOffset;
        canvasRoot.localScale = Vector3.one;
        canvasRoot.localRotation = Quaternion.identity;

        EnsureEventSystemExists();

        var frame = CreatePanel(canvasRoot, "Frame", backgroundColor);
        Stretch(frame);
        AddShadow(frame);

        BuildHeader(frame);
        BuildBody(frame);
        BuildFooter(frame);

        uiBuilt = true;
    }

    private void BuildHeader(RectTransform parent)
    {
        var header = CreatePanel(parent, "Header", panelColor);
        Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -12f), new Vector2(-12f, -72f));

        var titleText = CreateText(header, "Title", 28, FontStyles.Bold, TextAlignmentOptions.TopLeft, textColor);
        Anchor(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.62f, 1f), new Vector2(12f, -10f), new Vector2(-8f, -6f));
        titleText.text = "Pokedex";

        headerCountText = CreateText(header, "Count", 16, FontStyles.Bold, TextAlignmentOptions.TopRight, accentColor);
        Anchor(headerCountText.rectTransform, new Vector2(0.62f, 0.58f), new Vector2(1f, 1f), new Vector2(8f, -10f), new Vector2(-10f, -24f));

        headerHintText = CreateText(header, "Hint", 14, FontStyles.Normal, TextAlignmentOptions.BottomRight, mutedTextColor);
        Anchor(headerHintText.rectTransform, new Vector2(0.34f, 0f), new Vector2(1f, 0.46f), new Vector2(12f, 8f), new Vector2(-10f, -8f));
        headerHintText.text = "Target an animal to reveal its entry.";
    }

    private void BuildBody(RectTransform parent)
    {
        var body = new GameObject("Body", typeof(RectTransform));
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.SetParent(parent, false);
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(12f, 54f);
        bodyRect.offsetMax = new Vector2(-12f, -96f);

        var catalogPanel = CreatePanel(bodyRect, "CatalogPanel", panelColor);
        Anchor(catalogPanel, new Vector2(0f, 0f), new Vector2(0.28f, 1f), Vector2.zero, Vector2.zero);

        var catalogTitle = CreateText(catalogPanel, "CatalogTitle", 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, accentColor);
        Anchor(catalogTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -28f));
        catalogTitle.text = "Animal Index";

        var catalogHint = CreateText(catalogPanel, "CatalogHint", 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(catalogHint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -24f), new Vector2(-10f, -40f));
        catalogHint.text = "Discovered entries stay visible here.";

        var scrollView = CreateScrollView(catalogPanel, out catalogContent);
        Anchor(scrollView.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 10f), new Vector2(-8f, -50f));

        catalogEmptyText = CreateText(catalogContent, "CatalogEmpty", 13, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, lockedTextColor);
        Anchor(catalogEmptyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -12f), new Vector2(-8f, -12f));
        catalogEmptyText.text = "Add entries to the database to populate this list.";

        var detailPanel = CreatePanel(bodyRect, "DetailPanel", panelColor);
        Anchor(detailPanel, new Vector2(0.30f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        var detailTitle = CreateText(detailPanel, "DetailTitle", 17, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, accentColor);
        Anchor(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, -28f));
        detailTitle.text = "Selected Animal";

        noSelectionPanel = CreateNoSelectionPanel(detailPanel);

        var topRow = new GameObject("TopRow", typeof(RectTransform));
        var topRowRect = topRow.GetComponent<RectTransform>();
        topRowRect.SetParent(detailPanel, false);
        topRowRect.anchorMin = new Vector2(0f, 0.52f);
        topRowRect.anchorMax = new Vector2(1f, 1f);
        topRowRect.offsetMin = new Vector2(12f, 10f);
        topRowRect.offsetMax = new Vector2(-12f, -34f);

        var iconFrame = CreatePanel(topRowRect, "IconFrame", backgroundColor);
        Anchor(iconFrame, new Vector2(0f, 0f), new Vector2(0.24f, 1f), Vector2.zero, Vector2.zero);

        detailIconImage = CreateRawImage(iconFrame, "Icon", accentMutedColor);
        var iconRect = detailIconImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.14f, 0.14f);
        iconRect.anchorMax = new Vector2(0.86f, 0.86f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var iconLabel = CreateText(iconFrame, "IconLabel", 13, FontStyles.Bold, TextAlignmentOptions.Center, lockedTextColor);
        iconLabel.text = "Icon";
        Anchor(iconLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.24f), Vector2.zero, Vector2.zero);

        var titleColumn = new GameObject("TitleColumn", typeof(RectTransform));
        var titleColumnRect = titleColumn.GetComponent<RectTransform>();
        titleColumnRect.SetParent(topRowRect, false);
        titleColumnRect.anchorMin = new Vector2(0.26f, 0f);
        titleColumnRect.anchorMax = new Vector2(1f, 1f);
        titleColumnRect.offsetMin = new Vector2(10f, 0f);
        titleColumnRect.offsetMax = new Vector2(0f, 0f);

        detailNameText = CreateText(titleColumnRect, "Name", 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(detailNameText.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        detailNameText.text = "No animal selected";

        detailScientificText = CreateText(titleColumnRect, "ScientificName", 13, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(detailScientificText.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);
        detailScientificText.text = "Target an animal to open its profile.";

        detailStatusText = CreateText(titleColumnRect, "Status", 12, FontStyles.Bold, TextAlignmentOptions.MidlineRight, accentColor);
        Anchor(detailStatusText.rectTransform, new Vector2(0.65f, 0f), new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        detailStatusText.text = "Waiting for discovery";

        var infoPanel = new GameObject("InfoPanel", typeof(RectTransform));
        var infoRect = infoPanel.GetComponent<RectTransform>();
        infoRect.SetParent(detailPanel, false);
        infoRect.anchorMin = new Vector2(0f, 0.30f);
        infoRect.anchorMax = new Vector2(1f, 0.54f);
        infoRect.offsetMin = new Vector2(10f, 4f);
        infoRect.offsetMax = new Vector2(-10f, -6f);

        detailMetaText = CreateText(infoRect, "Meta", 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(detailMetaText.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        detailDescriptionText = CreateText(infoRect, "Description", 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(detailDescriptionText.rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero);

        var lowerRow = new GameObject("LowerRow", typeof(RectTransform));
        var lowerRowRect = lowerRow.GetComponent<RectTransform>();
        lowerRowRect.SetParent(detailPanel, false);
        lowerRowRect.anchorMin = new Vector2(0f, 0f);
        lowerRowRect.anchorMax = new Vector2(1f, 0.24f);
        lowerRowRect.offsetMin = new Vector2(10f, 10f);
        lowerRowRect.offsetMax = new Vector2(-10f, -6f);

        detailBehaviorText = CreateSectionBox(lowerRowRect, "Behavior", "Behavior", accentMutedColor);
        detailTipsText = CreateSectionBox(lowerRowRect, "Tips", "Observation Tips", accentMutedColor);
        detailFactsText = CreateSectionBox(lowerRowRect, "Facts", "Facts", accentMutedColor);
    }

    private void BuildFooter(RectTransform parent)
    {
        var footer = CreatePanel(parent, "Footer", backgroundColor);
        Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 10f), new Vector2(-12f, 36f));

        var footerText = CreateText(footer, "FooterText", 12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(footerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(10f, 6f), new Vector2(-10f, -6f));
        footerText.text = "Future laser-pointer hook: call ShowEntry(...) when an animal is targeted.";

        var footerStatus = CreateText(footer, "FooterStatus", 12, FontStyles.Bold, TextAlignmentOptions.MidlineRight, accentColor);
        Anchor(footerStatus.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 1f), new Vector2(10f, 6f), new Vector2(-10f, -6f));
        footerStatus.text = "World-space HUD";
    }

    private GameObject CreateNoSelectionPanel(RectTransform parent)
    {
        var panel = CreatePanel(parent, "NoSelectionPanel", new Color(0.08f, 0.11f, 0.09f, 0.80f));
        Anchor(panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 18f), new Vector2(-20f, -18f));

        var label = CreateText(panel, "NoSelectionLabel", 14, FontStyles.Bold, TextAlignmentOptions.Center, lockedTextColor);
        Anchor(label.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 0.70f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
        label.text = "No animal selected";

        var body = CreateText(panel, "NoSelectionBody", 12, FontStyles.Normal, TextAlignmentOptions.Center, mutedTextColor);
        Anchor(body.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.40f), new Vector2(24f, 0f), new Vector2(-24f, 0f));
        body.text = "Target an animal in the world to reveal its entry, description, and notes.";

        return panel.gameObject;
    }

    private ScrollRect CreateScrollView(RectTransform parent, out RectTransform contentRoot)
    {
        var scrollObject = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        var scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.SetParent(parent, false);

        var scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(0f, 0f, 0f, 0.12f);

        var scrollMask = scrollObject.GetComponent<Mask>();
        scrollMask.showMaskGraphic = false;

        var scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.SetParent(scrollObject.transform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.02f);

        var viewportMask = viewport.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentRoot = content.GetComponent<RectTransform>();
        contentRoot.SetParent(viewportRect, false);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.offsetMin = new Vector2(10f, 10f);
        contentRoot.offsetMax = new Vector2(-10f, -10f);

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 8f;
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

    private Button CreateCatalogButton(PokedexEntryData entry)
    {
        var buttonObject = new GameObject($"Entry_{entry.EntryId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(catalogContent, false);

        var image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        var layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 42f;
        layoutElement.preferredHeight = 42f;

        var title = CreateText(buttonObject.transform, "Label", 12, FontStyles.Bold, TextAlignmentOptions.Left, textColor);
        Anchor(title.rectTransform, new Vector2(0f, 0.30f), new Vector2(0.82f, 0.86f), new Vector2(10f, 0f), new Vector2(-8f, 0f));
        title.text = entry.CommonName;

        var subtitle = CreateText(buttonObject.transform, "SubLabel", 10, FontStyles.Normal, TextAlignmentOptions.Left, mutedTextColor);
        Anchor(subtitle.rectTransform, new Vector2(0f, 0.08f), new Vector2(0.82f, 0.42f), new Vector2(10f, 0f), new Vector2(-8f, 0f));
        subtitle.text = string.IsNullOrWhiteSpace(entry.Category) ? "Unknown" : entry.Category;

        var chip = CreatePanel(buttonObject.transform, "StatusChip", accentMutedColor);
        Anchor(chip, new Vector2(0.84f, 0.17f), new Vector2(1f, 0.83f), new Vector2(0f, 0f), new Vector2(-12f, 0f));

        var chipText = CreateText(chip, "Status", 10, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(chipText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 2f), new Vector2(-6f, -2f));
        chipText.text = database != null && database.IsDiscovered(entry.EntryId) ? "Seen" : "Locked";

        var localEntry = entry;
        var button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => ShowEntry(localEntry, false));

        return button;
    }

    private TMP_Text CreateSectionBox(RectTransform parent, string objectName, string heading, Color headingColor)
    {
        var box = CreatePanel(parent, objectName, new Color(0.08f, 0.11f, 0.09f, 0.88f));
        Anchor(box, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);

        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0f);
        boxRect.anchorMax = new Vector2(1f, 1f);
        boxRect.offsetMin = Vector2.zero;
        boxRect.offsetMax = Vector2.zero;

        var title = CreateText(boxRect, $"{objectName}Heading", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, headingColor);
        Anchor(title.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, -4f));
        title.text = heading;

        var body = CreateText(boxRect, $"{objectName}Body", 10, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.62f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.text = string.Empty;
        return body;
    }

    private string BuildMetaLine(PokedexEntryData entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Category))
        {
            parts.Add($"Category: {entry.Category}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Habitat))
        {
            parts.Add($"Habitat: {entry.Habitat}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Diet))
        {
            parts.Add($"Diet: {entry.Diet}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Rarity))
        {
            parts.Add($"Rarity: {entry.Rarity}");
        }

        return string.Join("    ", parts);
    }

    private string BuildFactsText(PokedexEntryData entry)
    {
        if (entry.Facts == null || entry.Facts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var fact in entry.Facts)
        {
            if (string.IsNullOrWhiteSpace(fact))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ").Append(fact.Trim());
        }

        return builder.ToString();
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private void ClearCatalogButtons()
    {
        foreach (Transform child in catalogContent)
        {
            if (catalogEmptyText != null && child == catalogEmptyText.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        entryButtons.Clear();
    }

    private RectTransform CreatePanel(Transform parent, string objectName, Color color)
    {
        var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        var image = panelObject.GetComponent<Image>();
        image.color = color;

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

    private TMP_Text CreateText(Transform parent, string objectName, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void AddShadow(RectTransform rectTransform)
    {
        var shadow = rectTransform.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(6f, -6f);
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
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (listeners.Length <= 1)
        {
            return;
        }

        AudioListener preferredListener = null;
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            preferredListener = mainCamera.GetComponent<AudioListener>();
        }

        if (preferredListener == null)
        {
            preferredListener = listeners[0];
        }

        foreach (var listener in listeners)
        {
            if (listener != preferredListener)
            {
                listener.enabled = false;
            }
        }
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
}