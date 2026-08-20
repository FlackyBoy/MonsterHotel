using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Écran de fin de journée (U2) — score, étoiles, résumé. Affiché par DayScoreManager à chaque
/// TimeManager.OnNewDay. Écran PARTAGÉ (pas scopé à un joueur, contrairement à RoomManagementPanel)
/// — n'importe quel joueur peut le fermer avec son action Submit.
///
/// Construit sur UI Toolkit (UIDocument/VisualElement, Flexbox) plutôt qu'uGUI — le layout
/// (espacement, retour à la ligne, empilement) est géré nativement par le moteur de layout Yoga.
///
/// La STRUCTURE statique (Panel, barre d'onglets, 3 zones de contenu) vient d'un fichier UXML
/// (DayEndPanel.uxml, à côté de ce script) chargé automatiquement par UIDocument — PAS générée par
/// code comme dans une version précédente. Raison du changement : un feedback Feel/MMFeedbacks
/// (voir showFeedback) doit pouvoir retrouver un élément par son nom dès que la scène démarre ; tant
/// que la structure était bâtie par code dans Start(), il existait une fenêtre où l'élément
/// n'existait pas encore, et Feel mettait en cache une recherche vide qui ne se rafraîchissait
/// jamais. Un UXML est cloné par UIDocument dans son propre OnEnable(), avant TOUT Start() de la
/// scène — l'élément existe donc de façon fiable, quel que soit l'ordre d'exécution des composants,
/// exactement comme dans la démo Assets/Feel/FeelDemos/UIToolkitFeedbacksDemo/.
///
/// Le CONTENU dynamique (les lignes de stats, différentes chaque jour) reste généré par code
/// (RebuildLines) — un UXML ne peut pas exprimer un contenu qui change à l'exécution.
///
/// 3 sections navigables via un onglet latéral :
///   0. "Récap général"    — équivalents globaux/cumulés de toutes les stats journalières.
///   1. "Récap journalier" — stats du jour qui vient de se terminer.
///   2. "Monstres"         — détail par type de monstre, généré depuis l'enum MonsterType.
///
/// Setup :
/// 1. Composant UIDocument sur ce GameObject, avec Panel Settings assigné (un asset existant — ex.
///    réutiliser Assets/Feel/FeelDemos/UIToolkitFeedbacksDemo/PanelSettings/
///    FeelUIToolkitDemoPanelSettings.asset — ou un asset dédié créé via Create > UI Toolkit > Panel
///    Settings). Si laissé vide, une instance est créée à l'exécution (CreateRuntimePanelSettings)
///    mais celle-ci a un bug de dimensionnement connu et non résolu — un asset assigné à la main est
///    la voie recommandée.
/// 2. Glisser Assets/Scripts/UI/DayEndPanel.uxml dans le champ "Source Asset" du même UIDocument.
/// 3. Pour un effet Feel sur "Panel" (ou tout autre élément nommé du UXML) : un MMF_Player avec un
///    feedback UI Toolkit ciblant ce UIDocument par nom (voir MMF_UIToolkit.Query) — les noms
///    disponibles sont ceux du UXML : Panel, TabBar, Tab_0/Tab_1/Tab_2, ContentArea,
///    Content_0/Content_1/Content_2, DismissPrompt.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DayEndPanel : MonoBehaviour
{
    [Header("Police (optionnel — sinon récupérée automatiquement depuis TMP Settings)")]
    public Font fontOverride;

    [Header("Actions")]
    public string continueActionName = "Submit";
    public string nextTabActionName  = "NextTab";
    public string prevTabActionName  = "PrevTab";

    [Header("Design — dimensions (valeurs de départ, à ajuster dans l'Inspector)")]
    [Tooltip("Largeur du panel, en % de l'écran")]
    [Range(20f, 100f)] public float panelWidthPercent = 60f;
    [Tooltip("Hauteur fixe du panel, en % de l'écran — ne varie jamais selon la quantité de contenu ; ce qui dépasse scrolle dans l'onglet concerné")]
    [Range(20f, 100f)] public float panelHeightPercent = 85f;
    [Tooltip("Largeur de la barre d'onglets latérale, en pixels")]
    public float tabBarWidth = 220f;
    [Tooltip("Taille de police des lignes de contenu et des onglets")]
    public float fontSize = 20f;
    [Tooltip("Rayon des coins arrondis du panel et des onglets, en pixels")]
    public float cornerRadius = 12f;
    [Tooltip("Marge/rembourrage intérieur du panel, en pixels")]
    public float panelPadding = 24f;
    [Tooltip("Espace vertical entre deux lignes de contenu (stats séparées), en pixels — UNIQUEMENT le texte, pas les boutons d'onglet (voir Tab Button Spacing)")]
    public float lineSpacing = 8f;
    [Tooltip("Interligne à l'intérieur d'une même ligne de stat quand elle retourne à la ligne (texte trop long pour la largeur) — différent de Line Spacing, qui espace des lignes de stat séparées")]
    public float paragraphSpacing = 4f;
    [Tooltip("Espace vertical entre deux boutons d'onglet, en pixels — UNIQUEMENT les boutons, pas le texte des stats (voir Line Spacing)")]
    public float tabButtonSpacing = 8f;

    [Header("Effets — Feel/MMFeedbacks (optionnel)")]
    [Tooltip("Joué à chaque Show(). Ses feedbacks UI Toolkit ciblent les éléments du UXML par nom (Panel, Tab_0, Tab_1, Tab_2, ...) — voir doc de la classe. Penser à Timescale Mode = Unscaled : le jeu est en pause pendant que ce panel est affiché.")]
    public MMF_Player showFeedback;
    [Tooltip("Un MMF_Player par onglet (index 0 = Général, 1 = Journalier, 2 = Monstres), joué quand l'onglet correspondant devient actif (ouverture ET clic). Élément vide = pas d'effet sur cet onglet. Même remarque Timescale Mode = Unscaled.")]
    public MMF_Player[] tabBounceFeedbacks = new MMF_Player[TabCount];

    [Header("Design — couleurs")]
    public Color colPanelBg     = new(0.08f, 0.08f, 0.08f, 0.94f);
    public Color colOverlayBg   = new(0f, 0f, 0f, 0.55f);
    public Color colTabActive   = new(1f, 0.75f, 0f, 1f);
    public Color colTabInactive = new(0.16f, 0.16f, 0.16f, 0.9f);
    public Color colText        = new(0.9f, 0.9f, 0.9f);
    public Color colPrompt      = new(1f, 0.85f, 0.3f);

    // ─── Privé ────────────────────────────────────────────────────

    UIDocument _document;
    VisualElement _root;
    VisualElement _panel;
    VisualElement[] _tabButtons;
    ScrollView[]    _tabContents;
    Label           _dismissPrompt;
    Font            _resolvedFont;

    bool _waitingDismiss;
    int  _activeTab; // 0 = Général, 1 = Journalier, 2 = Monstres

    DayScoreManager.DailyRecapData   _dailyData;
    DayScoreManager.GeneralRecapData _generalData;
    Dictionary<MonsterType, DayScoreManager.MonsterTypeStats> _statsByType = new();

    const int TabCount = 3;
    static readonly string[] TabNames = { "Récap général", "Récap journalier", "Monstres" };

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        EnsureBuilt();
    }

    /// <summary>
    /// Récupère les éléments existants du UXML et applique le style une seule fois (idempotent).
    /// Appelé depuis Start() (après tous les OnEnable() de la scène, dont celui d'UIDocument qui
    /// clone le UXML) et par sécurité depuis Show().
    /// </summary>
    void EnsureBuilt()
    {
        if (_root != null) return;

        _document = GetComponent<UIDocument>();
        if (_document.panelSettings == null)
            _document.panelSettings = CreateRuntimePanelSettings();

        if (_document.rootVisualElement == null)
        {
            Debug.LogWarning("[DayEndPanel] rootVisualElement pas encore prêt — nouvelle tentative au prochain Show().");
            return;
        }

        if (!QueryElements())
        {
            Debug.LogError("[DayEndPanel] Structure UXML introuvable — vérifie que DayEndPanel.uxml est assigné dans le champ \"Source Asset\" du UIDocument.");
            return;
        }

        _resolvedFont = fontOverride != null ? fontOverride : ResolveDefaultFont();

        ApplyStyling();
        WireTabClicks();
        _root.style.display = DisplayStyle.None;

        // Si showFeedback a "Auto Play On Start"/"On Enable" coché, MMF_Player peut avoir tenté de
        // s'initialiser avant que la police/le style ne soient appliqués — sans incidence sur la
        // recherche par nom (l'élément existe dès le clone du UXML, avant même ce Start()), mais on
        // réinitialise quand même par sécurité pour repartir d'un état propre.
        showFeedback?.Initialization();
    }

    /// <summary>
    /// Retrouve par nom les éléments définis dans DayEndPanel.uxml. Retourne false si la structure
    /// attendue n'est pas là (UXML non assigné, ou noms désynchronisés du fichier).
    /// </summary>
    bool QueryElements()
    {
        var root = _document.rootVisualElement;

        _root          = root.Q<VisualElement>("DayEndRoot");
        _panel         = root.Q<VisualElement>("Panel");
        _dismissPrompt = root.Q<Label>("DismissPrompt");
        var tabBar     = root.Q<VisualElement>("TabBar");
        var contentArea = root.Q<VisualElement>("ContentArea");

        if (_root == null || _panel == null || _dismissPrompt == null || tabBar == null || contentArea == null)
            return false;

        _tabButtons  = new VisualElement[TabCount];
        _tabContents = new ScrollView[TabCount];
        for (int i = 0; i < TabCount; i++)
        {
            _tabButtons[i]  = root.Q<VisualElement>($"Tab_{i}");
            _tabContents[i] = root.Q<ScrollView>($"Content_{i}");
            if (_tabButtons[i] == null || _tabContents[i] == null) return false;

            var tabLabel = root.Q<Label>($"TabLabel_{i}");
            if (tabLabel != null) tabLabel.text = TabNames[i];
        }

        return true;
    }

    void WireTabClicks()
    {
        for (int i = 0; i < TabCount; i++)
        {
            int tabIndex = i;
            _tabButtons[i].RegisterCallback<ClickEvent>(_ => SwitchTab(tabIndex));
        }
    }

    /// <summary>
    /// Applique couleurs/dimensions (réglables dans l'Inspector) aux éléments récupérés du UXML —
    /// le UXML ne définit que la structure/les noms, aucun style, pour que tout reste pilotable
    /// depuis l'Inspector comme avant.
    /// </summary>
    void ApplyStyling()
    {
        _root.style.width           = Length.Percent(100);
        _root.style.height          = Length.Percent(100);
        _root.style.flexDirection   = FlexDirection.Row;
        _root.style.alignItems      = Align.Center;
        _root.style.justifyContent  = Justify.Center;
        _root.style.backgroundColor = colOverlayBg;

        _panel.style.flexDirection = FlexDirection.Row;
        _panel.style.backgroundColor = colPanelBg;
        _panel.style.paddingLeft = _panel.style.paddingRight = panelPadding;
        _panel.style.paddingTop  = _panel.style.paddingBottom = panelPadding;
        _panel.style.borderTopLeftRadius = _panel.style.borderTopRightRadius = cornerRadius;
        _panel.style.borderBottomLeftRadius = _panel.style.borderBottomRightRadius = cornerRadius;
        _panel.style.width     = Length.Percent(panelWidthPercent);
        _panel.style.height = Length.Percent(panelHeightPercent);

        var tabBar = _panel.Q<VisualElement>("TabBar");
        tabBar.style.flexDirection = FlexDirection.Column;
        tabBar.style.width = tabBarWidth;
        tabBar.style.marginRight = panelPadding;
        tabBar.style.flexShrink = 0;

        var contentArea = _panel.Q<VisualElement>("ContentArea");
        contentArea.style.flexGrow = 1;
        contentArea.style.flexDirection = FlexDirection.Column;

        float tabRadius = cornerRadius * 0.5f;
        for (int i = 0; i < TabCount; i++)
        {
            var tabBtn = _tabButtons[i];
            tabBtn.style.marginBottom = tabButtonSpacing;
            tabBtn.style.paddingTop = tabBtn.style.paddingBottom = 14;
            tabBtn.style.paddingLeft = tabBtn.style.paddingRight = 12;
            tabBtn.style.borderTopLeftRadius = tabBtn.style.borderTopRightRadius = tabRadius;
            tabBtn.style.borderBottomLeftRadius = tabBtn.style.borderBottomRightRadius = tabRadius;

            var tabLabel = tabBtn.Q<Label>();
            if (tabLabel != null)
            {
                ApplyFont(tabLabel);
                tabLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                tabLabel.style.fontSize = fontSize;
                tabLabel.style.color = colText;
            }

            _tabContents[i].style.flexGrow = 1;
            _tabContents[i].style.display = DisplayStyle.None;
        }

        ApplyFont(_dismissPrompt);
        _dismissPrompt.style.color = colPrompt;
        _dismissPrompt.style.marginTop = 16;
        _dismissPrompt.style.fontSize = fontSize;
        _dismissPrompt.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    /// <summary>
    /// Fallback si aucun PanelSettings n'est assigné dans l'Inspector. ATTENTION — bug connu et non
    /// résolu : le panel généré à l'exécution ainsi calcule une largeur correcte mais une hauteur
    /// bien trop petite, rendant le contenu quasi invisible. Assigner un asset existant dans
    /// l'Inspector (voir doc de la classe) contourne le problème et est la voie vérifiée.
    /// </summary>
    static PanelSettings CreateRuntimePanelSettings()
    {
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.scaleMode        = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode  = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match            = 0.5f;
        settings.sortingOrder     = 100f;
        return settings;
    }

    /// <summary>Récupère le Font source (ttf/otf) du TMP_FontAsset par défaut du projet — cohérence
    /// visuelle avec le reste du jeu sans rien demander à l'utilisateur.</summary>
    static Font ResolveDefaultFont()
    {
        var tmpDefault = TMP_Settings.defaultFontAsset;
        return tmpDefault != null ? tmpDefault.sourceFontFile : null;
    }

    // ─── API publique ────────────────────────────────────────────

    public void Show(DayScoreManager.DailyRecapData daily, DayScoreManager.GeneralRecapData general,
        Dictionary<MonsterType, DayScoreManager.MonsterTypeStats> statsByType)
    {
        EnsureBuilt();

        _dailyData   = daily;
        _generalData = general;
        _statsByType = statsByType ?? new Dictionary<MonsterType, DayScoreManager.MonsterTypeStats>();

        _dismissPrompt.text = $"[ {continueActionName} ] Continuer";

        RebuildGeneralTab();
        RebuildJournalierTab();
        RebuildMonstresTab();

        _root.style.display = DisplayStyle.Flex;
        SwitchTab(0);
        _waitingDismiss = true;

        // Re-recherche forcée avant de jouer : les lignes de stats (ex. Query par classe
        // "stat-line") sont détruites et recréées à chaque jour par RebuildXxxTab() ci-dessus —
        // contrairement à "Panel"/"Tab_X" qui existent une fois pour toutes, une recherche mise en
        // cache une seule fois au démarrage retrouverait des éléments d'un jour précédent (détruits)
        // ou rien du tout. SwitchTab() fait de même pour tabBounceFeedbacks.
        showFeedback?.Initialization();
        showFeedback?.PlayFeedbacks();
    }

    void Update()
    {
        if (!_waitingDismiss) return;
        if (GameManager.Instance == null) return;

        foreach (var pi in GameManager.Instance.Players)
        {
            if (pi == null) continue;

            var confirm = pi.actions.FindAction(continueActionName, throwIfNotFound: false);
            if (confirm != null && confirm.WasPressedThisFrame()) { Hide(); return; }

            var next = pi.actions.FindAction(nextTabActionName, throwIfNotFound: false);
            if (next != null && next.WasPressedThisFrame()) { SwitchTab((_activeTab + 1) % TabCount); return; }

            var prev = pi.actions.FindAction(prevTabActionName, throwIfNotFound: false);
            if (prev != null && prev.WasPressedThisFrame()) { SwitchTab((_activeTab - 1 + TabCount) % TabCount); return; }
        }
    }

    void Hide()
    {
        _waitingDismiss = false;
        _root.style.display = DisplayStyle.None;
        GameManager.Instance?.ResumeGame();
    }

    // ─── Onglet latéral (Général / Journalier / Monstres) ──────────

    void SwitchTab(int tab)
    {
        _activeTab = tab;
        for (int i = 0; i < TabCount; i++)
        {
            bool active = i == tab;
            _tabContents[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            _tabButtons[i].style.backgroundColor = active ? colTabActive : colTabInactive;

            var label = _tabButtons[i].Q<Label>();
            if (label != null) label.style.color = active ? Color.black : colText;
        }

        // dismissPrompt suit toujours l'onglet actif — Add() le détache automatiquement de son
        // ancien parent (VisualElement n'a rien à détruire, contrairement à un GameObject uGUI :
        // aucun risque de le perdre en reconstruisant les listes, voir RebuildLines/Clear()).
        _tabContents[tab].Add(_dismissPrompt);

        // Un MMF_Player par bouton d'onglet (voir tabBounceFeedbacks) — Feel n'a aucun moyen de
        // savoir tout seul quel bouton vient d'être cliqué, donc c'est ce script qui déclenche le
        // bon. Joué aussi bien au clic qu'à l'ouverture (Show() appelle SwitchTab(0)).
        // Initialization() forcée avant : si ce player a un feedback ciblant une ligne de stat
        // (nom/classe), elle a pu être détruite/recréée depuis la dernière recherche (voir Show()).
        if (tabBounceFeedbacks != null && tab < tabBounceFeedbacks.Length)
        {
            var feedback = tabBounceFeedbacks[tab];
            feedback?.Initialization();
            feedback?.PlayFeedbacks();
        }
    }

    // ─── Section Récap général ──────────────────────────────────────

    void RebuildGeneralTab()
    {
        var lines = new List<string>
        {
            $"Jour actuel : {_generalData.currentDay}",
            $"Score global : {_generalData.globalScore}",
            $"{_generalData.globalStars} / 5 étoiles",
            $"Or total : {_generalData.totalGold}G",
            $"Satisfaction moyenne globale : {_generalData.avgSatisfactionAllTime:F0}/100",
            $"Renommée totale : {_generalData.totalRenown:F1}",
            $"Confort total : {_generalData.totalComfort:F1}",
            $"Arrivées au total : {_generalData.totalArrivalsRoom} chambre | {_generalData.totalArrivalsRestaurant} resto",
            $"Clients servis au total : {_generalData.totalServedRoom + _generalData.totalServedRestaurant} | Non servis : {_generalData.totalUnservedRestaurant}",
            $"Gain resto total : {_generalData.totalMealRevenue}G",
            $"Monstres présents : {_generalData.presentCount}",
        };
        RebuildLines(_tabContents[0], lines);
    }

    // ─── Section Récap journalier ────────────────────────────────────

    void RebuildJournalierTab()
    {
        var lines = new List<string>
        {
            $"Jour {_dailyData.day} terminé",
            $"Score : {_dailyData.score}",
            $"{_dailyData.stars} / 5 étoiles",
            $"Or : {(_dailyData.goldDelta >= 0 ? "+" : "")}{_dailyData.goldDelta}G",
            $"Satisfaction moyenne : {_dailyData.avgSatisfaction:F0}/100",
            $"Arrivées : {_dailyData.arrivalCountRoom} chambre | {_dailyData.arrivalCountRestaurant} resto",
            $"Clients servis : {_dailyData.servedCountRoom + _dailyData.servedCountRestaurant} | Non servis : {_dailyData.unservedCountRestaurant}",
            $"Gain resto : {_dailyData.mealRevenueToday}G",
            $"Renommée gagnée : {(_dailyData.renownGained >= 0 ? "+" : "")}{_dailyData.renownGained:F1}",
        };
        RebuildLines(_tabContents[1], lines);
    }

    // ─── Section Monstres — liste générée depuis l'enum MonsterType ────

    /// <summary>
    /// Un bloc de lignes par type de monstre (un titre + une stat par ligne) — présence actuelle,
    /// satisfaction/départs du jour, renommée globale et gagnée/perdue ce jour précisément, gain
    /// resto du jour et cumulé. Une ligne vide sépare chaque bloc. Générée depuis l'enum
    /// MonsterType : un nouveau monstre ajouté à l'enum obtient son bloc automatiquement, aucune
    /// construction UI manuelle nécessaire. Le nombre de lignes par type n'est pas fixe (voir
    /// TODO.md "éléments ciblables par Feel" pour la note sur les noms Content_2_Line_N).
    /// </summary>
    void RebuildMonstresTab()
    {
        var lines = new List<string>();
        var boldLines = new HashSet<int>();
        bool first = true;
        foreach (MonsterType type in System.Enum.GetValues(typeof(MonsterType)))
        {
            if (!first) lines.Add(string.Empty);
            first = false;

            boldLines.Add(lines.Count);
            lines.Add($"{type}");

            if (!_statsByType.TryGetValue(type, out var s))
            {
                lines.Add("Aucun présent, aucun départ aujourd'hui");
                continue;
            }

            lines.Add($"Présents : {s.presentCount}");
            lines.Add(s.departureCount > 0
                ? $"Satisfaction moyenne : {s.avgSatisfaction:F0}/100"
                : "Satisfaction moyenne : —");
            lines.Add($"Départs aujourd'hui : {s.departureCount}");
            lines.Add($"Renommée globale : {s.globalRenown:F1}");
            lines.Add($"Renommée gagnée aujourd'hui : +{s.renownGainedToday:F1}");
            lines.Add($"Renommée perdue aujourd'hui : -{s.renownLostToday:F1}");
            lines.Add($"Gain resto aujourd'hui : +{s.mealRevenueToday}G");
            lines.Add($"Gain resto total : {s.mealRevenueTotal}G");
        }
        RebuildLines(_tabContents[2], lines, boldLines);
    }

    // ─── Génération de listes de texte (auto-layout, aucune position calculée) ──

    /// <summary>
    /// Vide le conteneur et ajoute un Label par ligne. Retour à la ligne (WhiteSpace.Normal) et
    /// empilement (Column) entièrement gérés par le moteur de layout — plus de mesure de police ni
    /// de calcul de position par ligne, contrairement à l'ancienne version uGUI.
    /// Chaque ligne reçoit un nom stable ("{nom du conteneur}_Line_{index}", ex. "Content_1_Line_0")
    /// — reconstruit chaque jour donc le CONTENU change, mais le nom reste identique d'un jour à
    /// l'autre pour un même index, ce qui permet de cibler une ligne précise avec un feedback Feel.
    /// boldLines (optionnel) : indices à mettre en gras (ex. les titres de type de monstre dans
    /// l'onglet Monstres).
    /// </summary>
    void RebuildLines(VisualElement container, List<string> lines, HashSet<int> boldLines = null)
    {
        if (container == null) return;
        container.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            var label = MakeLabel(lines[i]);
            label.name = $"{container.name}_Line_{i}";
            if (boldLines != null && boldLines.Contains(i))
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(label);
        }
    }

    /// <summary>Classe USS commune à toutes les lignes générées (stats) — permet à un feedback
    /// Feel en Query Mode "Class" de cibler toutes les lignes d'un coup (Query = "stat-line"),
    /// plutôt qu'une par une par nom.</summary>
    const string StatLineClass = "stat-line";

    Label MakeLabel(string text)
    {
        var label = new Label(text);
        label.AddToClassList(StatLineClass);
        ApplyFont(label);
        label.style.color = colText;
        label.style.fontSize = fontSize;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = lineSpacing;
        label.style.unityParagraphSpacing = paragraphSpacing;
        return label;
    }

    void ApplyFont(Label label)
    {
        if (_resolvedFont != null)
            label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_resolvedFont));
    }
}
