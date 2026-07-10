using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PokedexXRCloneUIController : MonoBehaviour, IPokedexUI
{
    [Header("Data")]
    [SerializeField] private PokedexDatabase database;
    [SerializeField] private string initialEntryId;
    [SerializeField] private bool showFirstEntryOnStart = true;

    [Header("Placement")]
    [SerializeField] private bool buildUIOnAwake = true;
    [SerializeField] private bool followCamera = true;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, -0.18f, 1.35f);
    // ponytail: world-space metres-per-UI-unit; ~1.25m-wide panel. Tune for comfort in-headset.
    [SerializeField] private float worldCanvasScale = 0.0012f;
    [Tooltip("Right controller / pokedex model. If empty, auto-found from the laser reveal bridge. When set, the panel floats above it instead of the head.")]
    [SerializeField] private Transform handAnchor;
    [Tooltip("Offset from the hand anchor in its local space (metres). Y lifts the panel above the model.")]
    [SerializeField] private Vector3 handOffset = new Vector3(0f, 0.22f, 0.05f);
    [Tooltip("Tilt in degrees applied after the panel faces you. Positive pitches the top away from you.")]
    [SerializeField] private float panelTiltDegrees = 0f;
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
    private RectTransform headerRect;
    private RectTransform bodyRect;
    private RectTransform footerRect;
    private Image frameImage;
    private RectTransform entryListRect;
    private readonly Dictionary<string, Button> entryButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

    private TMP_Text titleText;
    private TMP_Text countText;
    private TMP_Text subtitleText;
    private TMP_Text entryNameText;
    private TMP_Text entryScientificText;
    private Image categoryIconImage;
    private TMP_Text categoryValueText;
    private TMP_Text heightValueText;
    private TMP_Text weightValueText;
    private TMP_Text dietValueText;
    private TMP_Text habitatText;
    private TMP_Text behaviorText;
    private TMP_Text funFactText;
    private TMP_Text footerText;
    private TMP_Text headerCloseText;
    private RectTransform resetButtonRect;
    private RectTransform switchButtonRect;
    private TMP_Text listEmptyText;
    private TMP_Text expandText;
    private RawImage entryIcon;
    private RawImage silhouetteImage;
    private RectTransform silhouetteFrameRect;
    private TMP_Text silhouetteGuideText;
    private TMP_Text silhouetteLabelText;
    private GameObject emptyStatePanel;
    private readonly List<Image> speciesIconSlots = new List<Image>();

    // Right-controller focus navigation: joystick moves focus between these buttons, A confirms.
    private readonly List<Button> focusTargets = new List<Button>();
    private int focusIndex;
    private RectTransform focusBorder;
    private float nextNavTime;
    private InputAction openCloseAction;
    private InputAction confirmAction;
    private InputAction navigateAction;

    [Header("3D Model Viewer")]
    [SerializeField] private float modelRotateSpeed = 120f;
    // ponytail: layer 31 assumed unused for the off-screen model rig; change if it clashes
    private const int ModelLayer = 31;
    private Camera modelCamera;
    private RenderTexture modelRenderTexture;
    private Transform modelPivot;
    private GameObject currentModel;

    private PokedexEntryData currentEntry;
    private bool uiBuilt;
    private bool isExpanded;
    private AudioSource audioSource;
    private static TMP_FontAsset fallbackFontAsset;
    private RectTransform discoveryToastRect;
    private TMP_Text discoveryToastTitle;
    private TMP_Text discoveryToastSubtitle;
    private int lastDiscoveredCount = 0;
    private Coroutine toastRoutine;

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
        // ensure an AudioSource exists for playing animal sounds
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
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

        HandleControllerInput();

        // Pulse the focus border so it reads clearly as the active selection.
        if (focusBorder != null && focusBorder.gameObject.activeInHierarchy)
        {
            var outline = focusBorder.GetComponent<Outline>();
            if (outline != null)
            {
                var oc = outline.effectColor;
                oc.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                outline.effectColor = oc;
            }
        }

        // Pulse the "?" while the slot is empty so it looks alive, not broken.
        if (silhouetteGuideText != null && silhouetteGuideText.text == "?")
        {
            float a = 0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
            var c = silhouetteGuideText.color; c.a = a; silhouetteGuideText.color = c;
        }

        if (currentModel != null && modelPivot != null && isExpanded)
        {
            var stick = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
            if (stick.sqrMagnitude > 0.01f)
            {
                modelPivot.Rotate(Vector3.up, -stick.x * modelRotateSpeed * Time.deltaTime, Space.World);
                modelPivot.Rotate(Vector3.right, stick.y * modelRotateSpeed * Time.deltaTime, Space.World);
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                // PC fallback: hold left mouse and drag to spin the model
                var drag = Mouse.current.delta.ReadValue();
                modelPivot.Rotate(Vector3.up, -drag.x * 0.3f, Space.World);
                modelPivot.Rotate(Vector3.right, drag.y * 0.3f, Space.World);
            }
        }
    }

    private void LateUpdate()
    {
        if (!followCamera || canvas == null)
        {
            return;
        }

        // The XR eye camera may spawn after Awake; resolve it lazily.
        if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        var cam = canvas.worldCamera;
        if (cam == null) return;
        var camT = cam.transform;

        var anchor = ResolveHandAnchor();
        if (anchor != null)
        {
            // Float above the controller/model so it looks like it projects out of the device...
            var pos = anchor.TransformPoint(handOffset);
            canvas.transform.position = pos;
            // ...but always face the user so the text stays readable.
            canvas.transform.rotation = Quaternion.LookRotation(pos - camT.position, Vector3.up)
                                        * Quaternion.Euler(panelTiltDegrees, 0f, 0f);
            return;
        }

        var headPos = camT.position + camT.rotation * cameraOffset;
        canvas.transform.position = headPos;
        canvas.transform.rotation = Quaternion.LookRotation(headPos - camT.position, camT.up)
                                    * Quaternion.Euler(panelTiltDegrees, 0f, 0f);
    }

    // Right controller transform: the assigned one, or the laser reveal bridge's (it lives
    // on the right ray interactor). Cached once found.
    private Transform ResolveHandAnchor()
    {
        if (handAnchor != null) return handAnchor;
        var bridge = FindFirstObjectByType<PokedexLaserRevealBridge>();
        if (bridge != null) handAnchor = bridge.transform;
        return handAnchor;
    }

    private void OnDestroy()
    {
        if (database != null)
        {
            database.DatabaseChanged -= HandleDatabaseChanged;
        }

        if (modelRenderTexture != null)
        {
            modelRenderTexture.Release();
        }

        openCloseAction?.Dispose();
        confirmAction?.Dispose();
        navigateAction?.Dispose();
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

    // Full reset to the initial "first Play" state: reloading the scene restores every
    // animal, bird, and the player/camera to their authored positions. The PokedexDatabase
    // is a ScriptableObject whose discovered-set survives scene loads in play mode, so we
    // clear it explicitly first.
    public void ResetDex()
    {
        database?.ClearDiscovered();
        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    // Cycle to the next scene in Build Settings order (wraps around). Add a scene to
    // Build Settings and it joins the rotation automatically — no code change needed.
    public void SwitchScene()
    {
        int count = SceneManager.sceneCountInBuildSettings;
        if (count < 2) return;
        int next = (SceneManager.GetActiveScene().buildIndex + 1) % count;
        SceneManager.LoadScene(next);
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

        if (currentEntry != null && database != null)
        {
            database.EnsureEntry(currentEntry); // add animals not yet in this DB (e.g. from another scene)
            if (markDiscovered) database.MarkDiscovered(currentEntry.EntryId);
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

    // Right controller: B = open/close, joystick = move focus, A = confirm focused button.
    // Bindings also include gamepad East/South + left stick so this is testable without a headset.
    private void EnsureControllerActions()
    {
        if (openCloseAction != null) return;

        openCloseAction = new InputAction("PokedexOpenClose", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        openCloseAction.AddBinding("<Gamepad>/buttonEast");

        confirmAction = new InputAction("PokedexConfirm", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");

        navigateAction = new InputAction("PokedexNavigate", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}");
        navigateAction.AddBinding("<Gamepad>/leftStick");

        openCloseAction.Enable();
        confirmAction.Enable();
        navigateAction.Enable();
    }

    private void HandleControllerInput()
    {
        EnsureControllerActions();

        if (openCloseAction.WasPressedThisFrame()) ToggleExpanded();
        if (!isExpanded) return;

        // Stick: left/right moves button focus, up/down pages through discovered animals.
        Vector2 nav = navigateAction.ReadValue<Vector2>();
        if (nav.magnitude < 0.3f)
        {
            nextNavTime = 0f; // released: next push registers immediately
        }
        else if (Time.unscaledTime >= nextNavTime)
        {
            if (Mathf.Abs(nav.x) >= Mathf.Abs(nav.y))
                MoveFocus(nav.x > 0f ? 1 : -1);
            else
                NavigateDiscovered(nav.y > 0f ? -1 : 1); // stick up = previous entry
            nextNavTime = Time.unscaledTime + 0.25f; // repeat delay while stick held
        }

        if (confirmAction.WasPressedThisFrame() && focusTargets.Count > 0)
        {
            var btn = focusTargets[focusIndex];
            if (btn != null && btn.gameObject.activeInHierarchy) btn.onClick.Invoke();
        }
    }

    private void MoveFocus(int dir)
    {
        if (focusTargets.Count == 0) return;
        focusIndex = ((focusIndex + dir) % focusTargets.Count + focusTargets.Count) % focusTargets.Count;
        UpdateFocusBorder();
    }

    // Page through the animals already discovered, in list order (wraps).
    private void NavigateDiscovered(int dir)
    {
        if (database == null) return;

        var discovered = new List<PokedexEntryData>();
        foreach (var e in database.Entries)
            if (database.IsDiscovered(e.EntryId)) discovered.Add(e);
        if (discovered.Count == 0) return;

        int index = 0;
        if (currentEntry != null)
        {
            for (int i = 0; i < discovered.Count; i++)
            {
                if (string.Equals(discovered[i].EntryId, currentEntry.EntryId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
        }

        index = ((index + dir) % discovered.Count + discovered.Count) % discovered.Count;
        ShowEntry(discovered[index], false); // already discovered; don't re-mark
    }

    private void UpdateFocusBorder()
    {
        if (focusTargets.Count == 0) return;
        if (focusBorder == null) CreateFocusBorder();

        focusIndex = Mathf.Clamp(focusIndex, 0, focusTargets.Count - 1);
        var target = focusTargets[focusIndex];
        if (target == null) { focusBorder.gameObject.SetActive(false); return; }

        focusBorder.SetParent(target.transform, false);
        focusBorder.SetAsLastSibling();
        focusBorder.anchorMin = Vector2.zero;
        focusBorder.anchorMax = Vector2.one;
        focusBorder.offsetMin = new Vector2(-4f, -4f);
        focusBorder.offsetMax = new Vector2(4f, 4f);
        focusBorder.gameObject.SetActive(isExpanded);
    }

    private void CreateFocusBorder()
    {
        var go = new GameObject("FocusBorder", typeof(RectTransform), typeof(Image), typeof(Outline));
        focusBorder = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);
        img.raycastTarget = false;
        var outline = go.GetComponent<Outline>();
        // White instead of holoAccentColor: the buttons themselves are green, so a green
        // outline barely showed up against them.
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(3f, 3f);
        go.SetActive(false);
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (rootRect != null)
        {
            rootRect.sizeDelta = expanded ? panelSize : collapsedPanelSize;
            if (!followCamera)
            {
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(0f, 1f);
                rootRect.pivot = new Vector2(0f, 1f);
                rootRect.anchoredPosition = anchoredScreenOffset;
            }
        }

        if (frameRect != null)
        {
            frameRect.sizeDelta = expanded ? panelSize : collapsedPanelSize;
            frameRect.anchorMin = new Vector2(0f, 1f);
            frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition = Vector2.zero;
        }

        if (frameImage != null)
        {
            frameImage.color = expanded ? backgroundColor : Color.clear;
        }

        if (entryListRect != null)
        {
            entryListRect.gameObject.SetActive(expanded);
        }

        if (headerRect != null)
        {
            headerRect.gameObject.SetActive(true);
        }

        if (bodyRect != null)
        {
            bodyRect.gameObject.SetActive(expanded);
        }

        if (subtitleText != null) subtitleText.gameObject.SetActive(false);
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
        if (footerRect != null)
        {
            footerRect.gameObject.SetActive(true);
        }

        if (footerText != null) footerText.gameObject.SetActive(expanded);
        if (resetButtonRect != null) resetButtonRect.gameObject.SetActive(expanded);
        if (switchButtonRect != null) switchButtonRect.gameObject.SetActive(expanded);
        if (emptyStatePanel != null) emptyStatePanel.SetActive(expanded && currentEntry == null);

        if (expanded)
        {
            focusIndex = 0;
            UpdateFocusBorder();
        }
        else if (focusBorder != null)
        {
            focusBorder.gameObject.SetActive(false);
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
        if (followCamera)
        {
            // World Space is the only render mode a VR headset displays; ScreenSpaceOverlay
            // renders to the desktop mirror only. LateUpdate keeps it in front of the camera.
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.35f;

        rootRect = root.GetComponent<RectTransform>();
        if (followCamera)
        {
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one * worldCanvasScale;
        }
        else
        {
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = anchoredScreenOffset;
            rootRect.localScale = Vector3.one;
        }
        rootRect.sizeDelta = startCollapsed ? collapsedPanelSize : panelSize;

        EnsureEventSystemExists();

        var frame = CreatePanel(rootRect, "Frame", backgroundColor, frameSprite, true);
        frameRect = frame;
        frameImage = frame.GetComponent<Image>();
        Anchor(frameRect, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        frameRect.pivot = new Vector2(0f, 1f);
        frameRect.sizeDelta = startCollapsed ? collapsedPanelSize : panelSize;
        AddShadow(frame);

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
        headerRect = header;
        Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-4f, -4f));

        subtitleText = CreateText(header, "Subtitle", 12, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.84f, 1f), new Vector2(10f, 0f), new Vector2(-4f, 0f));
        subtitleText.text = "XR Device Simulator style clone";

    }

    private void BuildBody(RectTransform parent)
    {
        var body = new GameObject("Body", typeof(RectTransform));
        bodyRect = body.GetComponent<RectTransform>();
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
        listEmptyText.text = "Point laser at an\nanimal to discover it";

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

        silhouetteImage = CreateRawImage(silhouetteBox, "SilhouetteImage", Color.white);
        // Center + keep it square (the model render texture is square) so it fits the frame
        // instead of stretching to the tall/narrow card. FitInParent => largest square that fits.
        Anchor(silhouetteImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var silhouetteFitter = silhouetteImage.gameObject.AddComponent<AspectRatioFitter>();
        silhouetteFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        silhouetteFitter.aspectRatio = 1f;

        silhouetteGuideText = CreateText(silhouetteBox, "SilhouetteGuide", 12, FontStyles.Bold, TextAlignmentOptions.Center, holoAccentColor);
        Anchor(silhouetteGuideText.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
        silhouetteGuideText.text = "?";

        silhouetteLabelText = CreateText(silhouetteCard, "SilhouetteLabel", 10, FontStyles.Normal, TextAlignmentOptions.Center, mutedTextColor);
        Anchor(silhouetteLabelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.10f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        silhouetteLabelText.text = "Undiscovered";

        var infoColumn = CreatePanel(detailPanel, "InfoColumn", new Color(0f, 0f, 0f, 0f));
        Anchor(infoColumn, new Vector2(0.36f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-6f, 0f));

        var categoryCard = CreatePanel(infoColumn, "CategoryCard", panelColor, panelSprite, true);
        Anchor(categoryCard, new Vector2(0f, 0.79f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -6f));
        var categoryTitle = CreateText(categoryCard, "CategoryTitle", 12, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(categoryTitle.rectTransform, new Vector2(0.18f, 0.54f), new Vector2(1f, 1f), new Vector2(4f, -2f), new Vector2(-8f, -4f));
        categoryTitle.text = "CATEGORY";
        categoryValueText = CreateText(categoryCard, "CategoryValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(categoryValueText.rectTransform, new Vector2(0.18f, 0f), new Vector2(1f, 0.55f), new Vector2(4f, 2f), new Vector2(-8f, -2f));
        categoryValueText.text = "Pack Hunter";
        var categoryIcon = CreatePanel(categoryCard, "CategoryIcon", holoAccentColor);
        categoryIconImage = categoryIcon.GetComponent<Image>();
        Anchor(categoryIcon, new Vector2(0f, 0f), new Vector2(0.14f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));

        var statsCard = CreatePanel(infoColumn, "StatsCard", panelColor, panelSprite, true);
        // Move stats lower to avoid overlapping the category above
        Anchor(statsCard, new Vector2(0f, 0.52f), new Vector2(1f, 0.75f), new Vector2(0f, 0f), new Vector2(0f, -6f));

        var heightLabel = CreateText(statsCard, "HeightLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(heightLabel.rectTransform, new Vector2(0.04f, 0.66f), new Vector2(0.42f, 1f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        heightLabel.text = "HEIGHT";
        heightValueText = CreateText(statsCard, "HeightValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(heightValueText.rectTransform, new Vector2(0.42f, 0.66f), new Vector2(1f, 1f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        heightValueText.text = "0.80 m";

        var weightLabel = CreateText(statsCard, "WeightLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(weightLabel.rectTransform, new Vector2(0.04f, 0.39f), new Vector2(0.42f, 0.66f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        weightLabel.text = "WEIGHT";
        weightValueText = CreateText(statsCard, "WeightValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(weightValueText.rectTransform, new Vector2(0.42f, 0.39f), new Vector2(1f, 0.66f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        weightValueText.text = "45 - 55 kg";

        var dietLabel = CreateText(statsCard, "DietLabel", 11, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(dietLabel.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.42f, 0.39f), new Vector2(0f, -4f), new Vector2(-4f, -2f));
        dietLabel.text = "DIET";
        dietValueText = CreateText(statsCard, "DietValue", 11, FontStyles.Normal, TextAlignmentOptions.MidlineRight, mutedTextColor);
        Anchor(dietValueText.rectTransform, new Vector2(0.42f, 0.12f), new Vector2(1f, 0.39f), new Vector2(4f, -4f), new Vector2(-8f, -2f));
        dietValueText.text = "Carnivore";

        var habitatCard = CreatePanel(infoColumn, "HabitatCard", panelColor, panelSprite, true);
        // Position habitat below stats with extra spacing from diet
        Anchor(habitatCard, new Vector2(0f, 0.33f), new Vector2(0.49f, 0.49f), new Vector2(0f, 0f), new Vector2(-3f, -4f));
        var habitatLabel = CreateText(habitatCard, "HabitatLabel", 10, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(habitatLabel.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        habitatLabel.text = "HABITAT";
        habitatText = CreateText(habitatCard, "HabitatText", 9, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(habitatText.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.62f), new Vector2(8f, 0f), new Vector2(-8f, -2f));
        habitatText.text = "Forests, mountains and northern regions.";

        var behaviorCard = CreatePanel(infoColumn, "BehaviorCard", panelColor, panelSprite, true);
        // Position behavior below habitat
        Anchor(behaviorCard, new Vector2(0f, 0.08f), new Vector2(0.49f, 0.31f), new Vector2(0f, 0f), new Vector2(-3f, -2f));
        var behaviorLabel = CreateText(behaviorCard, "BehaviorLabel", 10, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(behaviorLabel.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        behaviorLabel.text = "BEHAVIOR";
        behaviorText = CreateText(behaviorCard, "BehaviorText", 9, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(behaviorText.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.62f), new Vector2(8f, 0f), new Vector2(-8f, -2f));
        behaviorText.text = "Highly social. Lives and hunts in packs.";

        var bottomRightCard = CreatePanel(infoColumn, "BottomRightCard", panelColor, panelSprite, true);
        // Make fun-fact the same compact size as Behavior and place it on the right column
        Anchor(bottomRightCard, new Vector2(0.51f, 0.08f), new Vector2(1f, 0.31f), new Vector2(3f, 0f), new Vector2(0f, -2f));
        var funFactLabel = CreateText(bottomRightCard, "FunFactLabel", 12, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(funFactLabel.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        funFactLabel.text = "FUN FACT";
        funFactText = CreateText(bottomRightCard, "FunFactText", 12, FontStyles.Normal, TextAlignmentOptions.TopLeft, mutedTextColor);
        Anchor(funFactText.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.72f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        funFactText.text = "A wolf's howl can be heard up to 10 kilometers away.";

        // Add a Sound card on the right column sized like Habitat
        var soundCard = CreatePanel(infoColumn, "SoundCard", panelColor, panelSprite, true);
        // match habitat vertical span
        Anchor(soundCard, new Vector2(0.51f, 0.33f), new Vector2(1f, 0.49f), new Vector2(3f, 0f), new Vector2(0f, -4f));
        var soundLabel = CreateText(soundCard, "SoundLabel", 12, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        Anchor(soundLabel.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f), new Vector2(8f, -4f), new Vector2(-8f, -2f));
        soundLabel.text = "SOUND";
        var playButtonPanel = CreatePanel(soundCard, "PlayButton", accentMutedColor);
        // Make play button smaller and square to avoid overlapping the label
        Anchor(playButtonPanel, new Vector2(0.32f, 0.22f), new Vector2(0.68f, 0.58f), new Vector2(6f, 4f), new Vector2(-6f, -4f));
        var playImage = playButtonPanel.GetComponent<Image>();
        var playButton = playButtonPanel.gameObject.AddComponent<Button>();
        playButton.targetGraphic = playImage;
        playButton.onClick.AddListener(PlayCurrentEntrySound);
        focusTargets.Add(playButton);
        var playText = CreateText(playButtonPanel, "PlayText", 12, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(playText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        playText.text = "Play";

        emptyStatePanel = CreatePanel(detailPanel, "EmptyState", new Color(0f, 0f, 0f, 0.10f)).gameObject;
        Anchor(emptyStatePanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
        var emptyLabel = CreateText(emptyStatePanel.transform, "EmptyLabel", 14, FontStyles.Bold, TextAlignmentOptions.Center, lockedTextColor);
        Anchor(emptyLabel.rectTransform, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.66f), Vector2.zero, Vector2.zero);
        emptyLabel.text = "NO ANIMAL SELECTED";
    }

    private void BuildFooter(RectTransform parent)
    {
        var footer = CreatePanel(parent, "Footer", backgroundColor, panelSprite, true);
        footerRect = footer;
        Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 52f));

        footerText = CreateText(footer, "FooterText", 10, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, mutedTextColor);
        Anchor(footerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        footerText.text = "SPECIES DISCOVERED";

        var switchButton = CreatePanel(footer, "SwitchButton", accentMutedColor);
        Anchor(switchButton, new Vector2(0.40f, 0.16f), new Vector2(0.59f, 0.84f), new Vector2(0f, 0f), new Vector2(-4f, 0f));
        switchButtonRect = switchButton;
        var switchButtonGraphic = switchButton.GetComponent<Image>();
        var switchButtonBehaviour = switchButton.gameObject.AddComponent<Button>();
        switchButtonBehaviour.transition = Selectable.Transition.None;
        switchButtonBehaviour.targetGraphic = switchButtonGraphic;
        switchButtonBehaviour.onClick.AddListener(SwitchScene);
        focusTargets.Add(switchButtonBehaviour);
        var switchText = CreateText(switchButton, "SwitchText", 11, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(switchText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        switchText.text = "SWITCH";

        var resetButton = CreatePanel(footer, "ResetButton", accentMutedColor);
        Anchor(resetButton, new Vector2(0.60f, 0.16f), new Vector2(0.79f, 0.84f), new Vector2(0f, 0f), new Vector2(-4f, 0f));
        resetButtonRect = resetButton;
        var resetButtonGraphic = resetButton.GetComponent<Image>();
        var resetButtonBehaviour = resetButton.gameObject.AddComponent<Button>();
        resetButtonBehaviour.transition = Selectable.Transition.None;
        resetButtonBehaviour.targetGraphic = resetButtonGraphic;
        resetButtonBehaviour.onClick.AddListener(ResetDex);
        focusTargets.Add(resetButtonBehaviour);

        var resetText = CreateText(resetButton, "ResetText", 11, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(resetText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        resetText.text = "RESET";

        var closeButton = CreatePanel(footer, "CloseButton", accentColor);
        Anchor(closeButton, new Vector2(0.79f, 0.16f), new Vector2(1f, 0.84f), new Vector2(0f, 0f), new Vector2(-8f, 0f));
        var closeButtonGraphic = closeButton.GetComponent<Image>();
        var closeButtonBehaviour = closeButton.gameObject.AddComponent<Button>();
        closeButtonBehaviour.transition = Selectable.Transition.None;
        closeButtonBehaviour.targetGraphic = closeButtonGraphic;
        closeButtonBehaviour.onClick.AddListener(ToggleExpanded);
        focusTargets.Add(closeButtonBehaviour);
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
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastAfter(discoveryToastDuration));
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

        if (database == null || database.DiscoveredCount == 0)
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
            if (database.IsDiscovered(entry.EntryId))
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
            HideModel();
            if (silhouetteImage != null)
            {
                silhouetteImage.texture = null;
                silhouetteImage.color = new Color(1f, 1f, 1f, 0f);
            }
            if (silhouetteLabelText != null) silhouetteLabelText.text = "Undiscovered";
            if (silhouetteGuideText != null) silhouetteGuideText.text = "?";
            if (footerText != null) footerText.text = "Target an animal to fill the detail panel.";
            return;
        }

        if (entryNameText != null) entryNameText.text = currentEntry.CommonName;
        if (entryScientificText != null) entryScientificText.text = currentEntry.ScientificName;
        if (categoryIconImage != null) categoryIconImage.color = GetCategoryColor(currentEntry.Category);
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
        if (silhouetteGuideText != null) silhouetteGuideText.text = string.Empty;
        if (footerText != null) footerText.text = !string.IsNullOrWhiteSpace(currentEntry.ShortDescription) ? currentEntry.ShortDescription : currentEntry.BehaviorNotes;

        // Model rendering is isolated: a bad prefab must never leave the rest of the panel stale.
        try { ApplySilhouetteTexture(currentEntry); }
        catch (Exception e) { Debug.LogException(e); }

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
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;

        var title = CreateText(buttonObject.transform, "Label", 12, FontStyles.Bold, TextAlignmentOptions.Left, textColor);
        Anchor(title.rectTransform, new Vector2(0f, 0.36f), new Vector2(0.82f, 0.88f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.text = entry.CommonName;

        var subtitle = CreateText(buttonObject.transform, "SubLabel", 10, FontStyles.Normal, TextAlignmentOptions.Left, mutedTextColor);
        Anchor(subtitle.rectTransform, new Vector2(0f, 0.08f), new Vector2(0.82f, 0.44f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        subtitle.text = string.IsNullOrWhiteSpace(entry.Category) ? "Unknown" : entry.Category;

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

    private Color GetCategoryColor(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return holoAccentColor;
        }

        switch (category.Trim().ToLowerInvariant())
        {
            case "mammal":
            case "mammals":
                return new Color(0.53f, 0.35f, 0.20f, 1f);
            case "bird":
            case "birds":
                return new Color(0.55f, 0.72f, 0.90f, 1f);
            case "reptile":
            case "reptiles":
                return new Color(0.18f, 0.28f, 0.18f, 1f);
            case "amphibian":
            case "amphibians":
                return new Color(0.16f, 0.24f, 0.22f, 1f);
            case "fish":
                return new Color(0.18f, 0.22f, 0.30f, 1f);
            default:
                return new Color(0.22f, 0.22f, 0.22f, 1f);
        }
    }

    private void ApplySilhouetteTexture(PokedexEntryData entry)
    {
        if (silhouetteImage == null)
        {
            return;
        }

        if (TryShowModel(entry))
        {
            return;
        }

        var loadedTexture = LoadSilhouetteTexture(entry);
        if (loadedTexture != null)
        {
            silhouetteImage.texture = loadedTexture;
            silhouetteImage.color = Color.white;
            return;
        }

        silhouetteImage.texture = null;
        silhouetteImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private Texture2D LoadSilhouetteTexture(PokedexEntryData entry)
    {
        if (entry == null)
        {
            return null;
        }

        var entryId = entry.EntryId?.Trim();
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        var texture = Resources.Load<Texture2D>($"Pokedex/Silhouettes/{entryId}_holo");
        if (texture != null)
        {
            return texture;
        }

        texture = Resources.Load<Texture2D>($"Pokedex/Silhouettes/{entryId}");
        if (texture != null)
        {
            return texture;
        }

        return null;
    }

    // Loads a 3D prefab for the entry (assigned on the asset, or Resources/Pokedex/Models/{entryId})
    // and renders it into the silhouette RawImage via an off-screen camera. Returns false if none.
    private bool TryShowModel(PokedexEntryData entry)
    {
        var prefab = entry?.ModelPrefab;
        if (prefab == null && entry != null)
        {
            prefab = Resources.Load<GameObject>($"Pokedex/Models/{entry.EntryId}");
        }

        if (prefab == null)
        {
            HideModel();
            return false;
        }

        EnsureModelRig();

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        modelPivot.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera, not the tail
        currentModel = Instantiate(prefab, modelPivot);
        currentModel.SetActive(true);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.Euler(entry.ModelViewRotation);
        SetLayerRecursive(currentModel, ModelLayer);
        TameModelInstance(currentModel);
        StartCoroutine(FitModelRoutine(currentModel));

        modelCamera.enabled = true;
        silhouetteImage.texture = modelRenderTexture;
        silhouetteImage.color = Color.white;
        return true;
    }

    private void HideModel()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
        if (modelCamera != null)
        {
            modelCamera.enabled = false;
        }
    }

    private void EnsureModelRig()
    {
        if (modelCamera != null)
        {
            return;
        }

        modelRenderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        modelRenderTexture.Create();

        // Park the rig far from the world so its camera never catches scene geometry.
        var rig = new GameObject("PokedexModelRig").transform;
        rig.SetParent(transform, false);
        rig.position = new Vector3(0f, -1000f, 0f);

        var camObject = new GameObject("PokedexModelCamera", typeof(Camera));
        camObject.transform.SetParent(rig, false);
        camObject.transform.localPosition = new Vector3(0f, 0f, -3f);
        modelCamera = camObject.GetComponent<Camera>();
        modelCamera.clearFlags = CameraClearFlags.SolidColor;
        modelCamera.backgroundColor = Color.clear;
        modelCamera.cullingMask = 1 << ModelLayer;
        modelCamera.fieldOfView = 30f;
        modelCamera.nearClipPlane = 0.01f;
        modelCamera.farClipPlane = 50f;
        modelCamera.targetTexture = modelRenderTexture;
        modelCamera.enabled = false;

        var lightObject = new GameObject("PokedexModelLight", typeof(Light));
        lightObject.transform.SetParent(rig, false);
        lightObject.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << ModelLayer;
        light.intensity = 1.2f;

        modelPivot = new GameObject("PokedexModelPivot").transform;
        modelPivot.SetParent(rig, false);
    }

    // Strip behaviours that fight a static display: gravity (falling), root motion / offscreen
    // culling (skinned birds vanish when parked off-screen).
    private void TameModelInstance(GameObject go)
    {
        // Custom scripts (e.g. the LittleBird flight controller) reposition the model in world
        // space and fly it off our rig camera — kill them so it just sits and spins.
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null)
            {
                mb.enabled = false;
            }
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }
        foreach (var anim in go.GetComponentsInChildren<Animator>(true))
        {
            anim.applyRootMotion = false;
        }
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
        }
    }

    // Fit + center the model using its real WORLD-space bounds. Skinned/GLTF models lie about
    // their bind-pose local bounds (wrong size AND center -> model shows only legs), so instead
    // we wait a frame for the pose to settle, size to the camera frustum, wait once more, then
    // slide the model so its true center sits on the pivot the camera is aimed at.
    private IEnumerator FitModelRoutine(GameObject go)
    {
        yield return null; // skinned world bounds are stale the frame after Instantiate
        if (go == null || go != currentModel) yield break;

        if (TryGetWorldBounds(go, out var wb))
        {
            float maxDim = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
            if (maxDim > 0.0001f)
            {
                // Fit inside the model camera's frustum (FOV 30 @ dist 3 => ~1.6u tall) with margin.
                float target = 1.4f * (currentEntry != null ? currentEntry.ModelViewScale : 1f);
                go.transform.localScale *= target / maxDim;
            }
        }

        yield return null; // let bounds refresh after the rescale
        if (go == null || go != currentModel) yield break;

        if (TryGetWorldBounds(go, out var wb2))
        {
            go.transform.position += modelPivot.position - wb2.center;
        }
    }

    private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        // Prefer skinned meshes (the animal body) so helper meshes (hover spheres) don't skew the fit.
        bool skinnedOnly = go.GetComponentInChildren<SkinnedMeshRenderer>() != null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (skinnedOnly && !(r is SkinnedMeshRenderer)) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private void PlayCurrentEntrySound()
    {
        if (currentEntry == null)
        {
            Debug.Log("Pokedex: no entry selected to play sound.");
            return;
        }
        // Prefer an explicitly assigned AudioClip on the entry
        if (currentEntry.SoundClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(currentEntry.SoundClip);
            Debug.Log($"Pokedex: playing assigned sound for {currentEntry.CommonName}");
            return;
        }

        var clip = LoadSoundClipFromResources(currentEntry);
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
            Debug.Log($"Pokedex: playing sound for {currentEntry.CommonName} from Resources/Pokedex/Sounds/{currentEntry.EntryId}");
            return;
        }

        Debug.Log($"Pokedex: no sound found for {currentEntry.CommonName}. Assign an AudioClip on the entry or add a file at Assets/Resources/Pokedex/Sounds/{currentEntry.EntryId}.");
    }

    private AudioClip LoadSoundClipFromResources(PokedexEntryData entry)
    {
        if (entry == null)
        {
            return null;
        }

        var candidates = new List<string>();

        AddSoundCandidates(candidates, entry.EntryId);
        AddSoundCandidates(candidates, entry.CommonName);

        var commonName = entry.CommonName?.Trim();
        if (!string.IsNullOrWhiteSpace(commonName))
        {
            var words = commonName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                AddSoundCandidates(candidates, words[words.Length - 1]);
                AddSoundCandidates(candidates, string.Concat(words));
            }
        }

        foreach (var candidate in candidates)
        {
            var clip = Resources.Load<AudioClip>($"Pokedex/Sounds/{candidate}");
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private static void AddSoundCandidates(List<string> candidates, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        var normalized = trimmed.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);

        void AddCandidate(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (!candidates.Exists(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(candidate);
            }
        }

        AddCandidate(trimmed);
        AddCandidate(trimmed.ToLowerInvariant());
        AddCandidate(trimmed.ToUpperInvariant());
        AddCandidate(normalized);
        AddCandidate(normalized.ToLowerInvariant());
        AddCandidate(normalized.ToUpperInvariant());
        if (normalized.Length > 1)
        {
            AddCandidate(char.ToUpperInvariant(normalized[0]) + normalized.Substring(1).ToLowerInvariant());
        }
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
        // normalize all UI text to size 12 as requested
        text.fontSize = 12;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
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
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
