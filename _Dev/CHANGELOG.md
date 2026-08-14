# Monster Hotel — Journal des modifications

---

## 2026-08-14 (suite) — Fix : débris de table impossibles à ramasser

Signalé par l'utilisateur : impossible de nettoyer les déchets laissés sur la table à manger après
qu'un monstre a mangé (`EatingSpot.SpawnMealDebris()`). Cause : `DebrisInstance.Update()` (détection
de portée joueur pour le ramassage manuel) comparait la distance 3D complète entre le joueur (au sol)
et le débris (posé sur la table, donc en hauteur) — l'écart vertical à lui seul pouvait dépasser
`pickupRange = 1.5f` même en étant collé à la table horizontalement.

Exactement le même bug avait déjà été trouvé et corrigé côté employé nettoyeur
(`CleaningEmployeeAI.HorizontalDistance()`, avec un commentaire explicite sur ce cas précis), mais le
fix n'avait jamais été reporté côté ramassage joueur. `DebrisInstance` compare maintenant en distance
horizontale (XZ) uniquement, même pattern que côté employé.

## 2026-08-14 — R3 : éclatement de `HotelConfig` en configs par catégorie

Chantier R3 du TODO : `HotelConfig.cs` (294 lignes, 29 sections `[Header]`) éclaté en 15
ScriptableObjects par domaine (`EconomyConfig`, `SatisfactionConfig`, `ReceptionConfig`,
`KitchenConfig`, `DayNightConfig`, `SpawnConfig`, `PlayerConfig`, `PlacementConfig`,
`CameraConfig`, `DitherConfig`, `BlockConfig`, `RoamConfig`, `EmployeeConfig`, `DebugConfig`,
`HotelCatalog`), chacun avec son propre asset sous `Resources/Config/` et le même pattern
singleton (`static Instance` → `Resources.Load`) que l'ancien `HotelConfig`. Pas de câblage
manuel entre assets : chaque catégorie se charge indépendamment.

`HotelConfig.cs` devient une façade statique (`HotelConfig.Economy`, `HotelConfig.Satisfaction`,
etc.) — garde le point d'accès central demandé par le TODO sans les risques d'un hub à références
Inspector (slot oublié = null). Les 25 fichiers de code qui lisaient `HotelConfig.Instance.xxx`
ont été mis à jour vers `HotelConfig.<Catégorie>.xxx`, sans changer le pattern de fallback
existant (`cfg != null ? cfg.champ : défaut`).

Vérification faite avant de découper : `cfg.monsters` (`SpawnScheduler`) et `cfg.rooms`
(`ShopCounter`) sont bien lus — contrairement à ce qu'affirmait R2 (note obsolète sur ce point).
`cfg.needTypes`, `cfg.blocks` et `blockHeight` restent orphelins (jamais lus), confirmé par grep
exhaustif — conservés tels quels, ce chantier ne touche pas au nettoyage (toujours R2).

Toutes les valeurs déjà réglées à la main dans l'ancien `HotelConfig.asset` ont été reportées à
l'identique dans les nouveaux assets, y compris celles flaguées "à trier" en B6
(`roamMinWait`/`roamMaxWait: 1`, `ditherHeightOffset: 500`, `playerMoveSpeed: 10`...) — ce
chantier est purement structurel, pas d'équilibrage.

🔍 **À faire dans l'éditeur pour vérifier** : Unity a été fermé pendant tout le découpage
(précaution — un changement de layout de ScriptableObject avait déjà causé une erreur "script
layout incompatible" par le passé, voir R2). À la réouverture, vérifier l'absence d'erreur dans
la Console, puis que les 15 assets sous `Resources/Config/` affichent bien leurs valeurs migrées
dans l'Inspector (ex: `EconomyConfig.startingGold = 10000`).

## 2026-08-13 (suite 2) — StandPoint dédié pour les comptoirs de réception

Suite du diagnostic receptionniste bloqué : le log `[Réception] TryStartTask` a confirmé que la
décision d'arbitrage (fix précédent) était correcte (`hotelGuest=null, restoGuest=14.9s → sert
resto`) — le blocage venait de la marche elle-même (`[NavMesh] ... chemin partiel trop long`, warning
répété). L'utilisateur a soupçonné le comptoir de "carver" le NavMesh — vérifié dans
`RestaurantCounter .prefab` : aucun `NavMeshObstacle`/`NavMeshModifier` présent, donc pas un carve à
proprement parler. Cause plus probable : la cible de marche était `desk.transform.position` (le
pivot brut du comptoir) plutôt qu'un point au sol devant lui — capture d'écran de l'utilisateur
montrant un personnage comme "posé sur"/empêtré dans le mesh du comptoir, cohérent avec un pivot mal
placé (en hauteur ou en plein milieu du meuble).

Fix (préventif, implémente l'idée déjà notée en TODO T5) : nouvelle propriété `StandPoint` sur
`ReceptionDesk`/`RestaurantReceptionDesk` — cherche un enfant nommé `"StandPoint"` et retourne sa
position si présent, sinon retombe sur `transform.position` (comportement historique, non-cassant).
Tous les points d'appel qui faisaient marcher un employé/monstre vers `desk.transform.position`
directement (`ReceptionEmployeeAI.GoToPost()`/`CheckInRoutine()`, `ReservationSystem`,
`RestaurantReservationSystem`, fallbacks sans slot de file) utilisent maintenant `desk.StandPoint`.

🔍 **À faire dans l'éditeur pour que le fix prenne effet** : ajouter un enfant nommé exactement
`StandPoint` sous chaque prefab de comptoir (hôtel `ReceptionDesk` et resto `RestaurantCounter`),
positionné au sol devant le comptoir (pas dans son mesh). Sans ce point, le comportement reste
identique à avant (aucune régression, mais le bug non plus).

## 2026-08-13 (suite) — Fix : réceptionniste qui n'allait plus au resto

Signalé par l'utilisateur : le réceptionniste laissait parfois la file du restaurant s'accumuler
sans jamais y aller. Cause : `ReceptionEmployeeAI` assignait un "poste" fixe (hôtel ou resto) à
chaque employé, avec **priorité absolue** sur ce poste tant qu'il y avait du monde à y servir —
il n'allait aider l'autre poste que si le sien était *complètement vide* au moment de la
vérification. Avec un flux d'arrivées hôtel suffisamment continu, cette condition n'était jamais
remplie, donc la file resto pouvait être ignorée indéfiniment (dépend du rythme d'arrivée des deux
files, d'où le "parfois").

Décision utilisateur : arbitrer par temps d'attente plutôt que par poste fixe. Refonte de
`TryStartTask()` — compare `WaitedSeconds` du premier de chaque file (`ReservationSystem.PendingGuest`
et `RestaurantReservationSystem.PendingVisitor` avaient déjà ce champ) et sert systématiquement le
plus ancien en attente, tous postes confondus. Suppression de tout le mécanisme `Post`/
`_currentPost`/`CountAtPost`/registre statique `_active`, devenu inutile : l'exclusivité `IsClaimed`
déjà en place suffit à répartir plusieurs employés entre les deux files sans concept de poste
(le suivant à devenir libre choisit naturellement le plus ancien parmi les non-réclamés restants).

## 2026-08-13 — Fix : déplacer une pièce occupée laissait les monstres sur place

Signalé par l'utilisateur : déplacer une pièce alors qu'un monstre est dedans laisse le monstre à
son ancien emplacement (`RoomPlacer.TryPlace()`, mode déplacement, repositionne la chambre et son
mobilier mais ne touche jamais aux monstres présents).

Cause trouvée : le bouton "Déplacer chambre" (`RoomManagementPanel.RebuildTabChambre()`) était déjà
masqué quand `RoomState.Occupied` — mais ce state ne concerne que les **chambres privées**
réservées à un client (`RoomInstance.Init()` : les pièces communes comme la cuisine/le salon
reçoivent un `FacilityRoomInstance` et ne passent jamais à `Occupied`, même avec des monstres
physiquement dedans). Le garde-fou existant ne couvrait donc pas les pièces communes.

Fix (`RoomManagementPanel.cs`) : nouvelle méthode `RoomHasMonsterInside(RoomInstance)` — vérifie la
présence physique de monstres dans les limites de la pièce (`BoundsUtils.Get()` + scan
`MonsterDataReference` via `FindObjectsByType`), plutôt que de se fier uniquement à `RoomState`.
Utilisée à deux endroits : (1) condition d'affichage du bouton "Déplacer chambre" (en plus de
`State != Occupied`), (2) double-check défensif dans `OnMoveClicked()` juste avant `StartMoving()`
(au cas où l'état changerait entre l'ouverture du panneau et le clic), avec message d'erreur
temporaire si bloqué (`ShowTemporaryMessage`, pattern déjà utilisé ailleurs dans ce fichier).

---

## 2026-08-12 (suite 8) — Fréquence d'apparition réglable par effet

Nouveau `weight` (défaut 1) sur `FightBurstEntry` — tirage pondéré (`PickWeightedBurst()`) au lieu
d'un tirage uniforme dans `BurstLoop()` : 2 = deux fois plus fréquent, 0.5 = deux fois plus rare,
0 ou négatif = jamais tiré (permet de désactiver temporairement une entrée sans la retirer de la
liste). Entrées sans prefab ou à poids nul ignorées du tirage.

## 2026-08-12 (suite 7) — Height offset + scale propres à chaque effet ponctuel

`FightBurstEntry` gagne `heightOffset` (défaut 0.8) et `scale` (défaut 3) propres à chaque entrée,
en plus de `applyJitter` déjà en place — permet de régler une "catégorie" d'effets (ex: tous les
textes `_BOOM_`/`_POW_`/...) différemment des autres, sans passer par un vrai système de
catégorie/enum (pas justifié pour ce besoin). `vfxHeightOffset`/`vfxScale` (champs globaux) ne
concernent plus que `fightCloudPrefab` (le nuage en boucle, resté un champ unique) — tooltips
clarifiés en conséquence. `BurstLoop()` utilise désormais `entry.heightOffset`/`entry.scale` au lieu
des champs globaux.

⚠️ Si `Fight Burst Prefabs` avait déjà été réassigné après le changement précédent, les nouvelles
valeurs `Height Offset`/`Scale` par entrée démarrent à leurs défauts (0.8/3) — à ajuster à l'œil.

## 2026-08-12 (suite 6) — Décalage réglable par effet, pas globalement

Correction du fix précédent : l'utilisateur veut le décalage sur certains effets seulement, d'autres
doivent rester pile centrés (positionnement déjà bon pour ceux-là). `fightBurstPrefabs` n'est plus
un `GameObject[]` mais un `FightBurstEntry[]` (nouvelle classe : `prefab` + bool `applyJitter`,
coché par défaut) — chaque entrée choisit individuellement si `burstPositionJitter` s'applique.

⚠️ **Changement de type de champ** : si `Fight Burst Prefabs` avait déjà été rempli dans l'Inspector
sur les prefabs monstres, Unity ne peut pas migrer l'ancien contenu (`GameObject[]` →
`FightBurstEntry[]`) — **à réassigner** (prefab + case `Apply Jitter` par entrée).

## 2026-08-12 (suite 5) — Décalage aléatoire des effets ponctuels

Les textes (`_BOOM_`/`_POW_`/...) apparaissaient pile centrés sur les deux monstres à chaque fois.
Nouveau `burstPositionJitter` (défaut 1m, à ajuster) — décalage horizontal aléatoire (`Random.insideUnitCircle`)
appliqué à chaque effet dans `BurstLoop()`. Le nuage en boucle (`fightCloudPrefab`) reste centré,
lui — il doit continuer à couvrir la paire de monstres, pas de raison de le décaler.

## 2026-08-12 (suite 4) — Effets ponctuels aléatoires et récurrents pendant la mêlée

Retour utilisateur : les effets ponctuels (ex. les textes cartoon `CFXR _BOOM_`/`_POW_`/`_WHAM_`/
`_BOING_`) ne doivent pas tous se déclencher ensemble au début, et doivent revenir de temps en
temps pendant toute la bagarre. `fightStartBurstPrefabs` renommé `fightBurstPrefabs`, nouveau
comportement dans `BurstLoop()` (nouvelle coroutine, propriété du leader comme le nuage) : pioche
**un seul** prefab au hasard dans la liste, attend un délai aléatoire (`burstIntervalMin`/`Max`,
défaut 1.5-4s), recommence tant que la mêlée dure. Stoppée explicitement dans `Interrupt()`
(`_burstCoroutine`) — sans ça elle continuerait à instancier des effets après la fin du combat.

## 2026-08-12 (suite 3) — Taille des VFX réglable

Nouveau champ `vfxScale` (défaut 3, à ajuster — les monstres sont à l'échelle ×3, les VFX en
avaient probablement besoin en proportion) sur `MonsterFightBehavior`, appliqué à `_vfxInstance`
(nuage) et à chaque effet de `fightStartBurstPrefabs` juste après leur `Instantiate()`. Modifie
l'échelle de l'instance créée en jeu, pas le prefab CFXR partagé — pas d'effet de bord si ce même
prefab est réutilisé ailleurs.

## 2026-08-12 (suite 2) — Debug : forcer une bagarre

`MonsterFightBehavior.DebugForceFight()` (`[ContextMenu("DEBUG — Forcer une bagarre avec le plus
proche")]`) — clic droit sur le composant en Play Mode sur un monstre : force une bagarre avec le
monstre libre le plus proche, ignore compatibilité/cooldown/recherche de besoin. Même pattern que
les outils debug déjà en place (`SpawnScheduler.ForceSpawnRoomGuest()`, `MonsterDebugTools`). Utile
pour tester sans attendre une rencontre opportuniste entre deux types compatibles-au-combat.

## 2026-08-12 (suite) — Fix : anims Fight/Talk qui se figent avant la fin

Signalé par l'utilisateur : l'anim de bagarre (placeholder `@male_emotion_angry`) se fige après
quelques secondes alors que le nuage de fumée continue de tourner. Cause : le clip n'a pas
`Loop Time` activé dans ses réglages d'import (`loopTime: 0`) — il joue une fois (~2.3s à 30fps,
70 frames) puis se fige sur la dernière frame, alors qu'une mêlée dure jusqu'à
`fightDurationBeforeAutoBreak` (12s par défaut) ou jusqu'à intervention du joueur.

En vérifiant, même souci trouvé côté conversations (pas encore signalé, mais même cause) : le clip
`Talk` (`@male_talk_stand`) a aussi `loopTime: 0` (300 frames, ~10s) — se fige pour toute
conversation dépassant sa durée, ce qui arrive pour une bonne partie de la plage `talkDurationMin`-
`talkDurationMax` (6-15s). Le clip `Listen` (`@male_nod_stand`) était déjà correctement en boucle,
pas touché.

Fix : `loopTime: 1` (+ `loop: 1`) sur les `.meta` de `@male_emotion_angry.FBX` et
`@male_talk_stand.FBX` (`Assets/Plugins/EverydayMotionPack/Motion/03_Emotion/` et `04_Interaction/`).
Vérifié au préalable qu'aucun autre état Animator du projet ne référence ces deux clips (recherche
sur leur guid) — la bascule en boucle ne touche donc que les états `Fight`/`Talk` déjà en place, pas
de risque d'effet de bord ailleurs. ⚠️ Si l'un de ces deux clips est réutilisé plus tard pour une
anim volontairement non bouclée (ex: un autre état qui voudrait juste un "sursaut" ponctuel), il
faudra soit dupliquer le clip, soit repasser `Loop Time` à false pour ce nouvel usage — la case
`Loop Time` est une propriété du clip source, pas de l'état qui le référence, donc partagée par
tous ses futurs usages.

## 2026-08-12 — Bagarres entre monstres incompatibles + séparation par le joueur

2e partie du chantier "interactions sociales monstres" (S2 dans le TODO). Composant frère de
`MonsterSocialBehavior` (même squelette : registre statique, state machine locale, verrouillage
synchrone, point de rendez-vous figé, point de sortie unique idempotent), pas une extension —
cohérent avec le style du projet. Plan complet dans
`C:\Users\Virgile\.claude\plans\elegant-prancing-pudding.md`.

### Assets/Scripts/Systems/Hotel/MonsterData.cs
Nouveau champ `incompatibleTypes[]` (MonsterType[], vide par défaut — à remplir en Inspector) +
`IsSociallyIncompatible()`. Miroir exact de `compatibleSocialTypes`/`IsSociallyCompatible`, vérifié
dans les deux sens.

### Assets/Scripts/Systems/Hotel/MonsterFightBehavior.cs (nouveau)
Recherche opportuniste d'un adversaire incompatible à proximité, verrouillage mutuel synchrone,
approche vers un point de rendez-vous figé, orientation instantanée vers la position réelle du
partenaire (mêmes précautions déjà éprouvées côté conversation contre la dynamique de poursuite
instable et les valeurs obsolètes). Différences avec le modèle conversation :
- Un seul état Animator `IsFighting`/`Fight` partagé par les deux participants (pas de distinction
  leader/follower comme Talk/Listen) — simplification assumée, l'utilisateur remplacera de toute
  façon le clip placeholder (`@male_emotion_angry`) par une vraie anim de bagarre plus tard.
- Deux issues : `ResolveByPlayer()` (joueur intervient à temps, pénalité de satisfaction faible) ou
  timeout après `fightDurationBeforeAutoBreak` sans intervention (pénalité sévère, appliquée par le
  leader). `Interrupt()` lui-même n'applique jamais de pénalité — toujours posée par l'appelant
  avant, symétrique avec l'absence de bonus de conversation sur interruption forcée.
- VFX (nuage de bagarre) instancié par le leader au point milieu réel des deux monstres (déjà
  arrêtés), non parenté (évite une destruction en cascade), détruit dans `Interrupt()`.

### Coordination croisée
Un monstre en bagarre ne doit pas être sélectionnable pour une conversation et vice-versa —
`MonsterSocialBehavior` et `MonsterFightBehavior` se référencent mutuellement (`_fightSibling`/
`_socialSibling`, vérifiés dans leurs `SearchLoop()`/scans respectifs). Pas de coordinateur
centralisé pour seulement 2 comportements "occupants" — cohérent avec la préférence du projet pour
la duplication directe plutôt que l'abstraction prématurée.

### Intégration dans le pattern existant (mêmes 5 points que pour les conversations)
`SpawnScheduler.cs` (composant ajouté avant Needs/Seeker/Roam, même contrainte d'ordre),
`ReservationSystem.cs` (`Activate()` au check-in + `Interrupt()` aux 3 mêmes points de sortie
forcée), `MonsterNeedSeeker.cs` (besoin urgent prime sur une bagarre en cours, sans pénalité
additionnelle), `MonsterRoamBehavior.IsBlocked` (étendu à `_fight.IsBusy`).

### Assets/Scripts/Systems/Player/MonsterFightBreaker.cs (nouveau)
Copie fidèle du pattern `CleaningInteractor.cs` (scan par distance sur `MonsterFightBehavior.All`,
prompt `WorldPrompt`, action Interact) — pas d'`InputConsumer`, suit l'exemple concret déjà en prod.
Ne propose l'interaction que pendant `IsFighting` (mêlée visible), pas pendant l'approche.
🔍 **À faire dans l'éditeur : ajouter ce composant au prefab Player** (Add Component, à côté de
`CleaningInteractor`) — pas de patch YAML sur le prefab Player, jugé plus risqué qu'utile pour un
ajout de composant aussi simple.

### Assets/Prefabs/Monsters/{Zombie,Vampire,Werewolf}.controller
Même pattern exact que `Talk`/`Listen` : nouveau paramètre bool `IsFighting`, nouvel état `Fight`
(clip placeholder `@male_emotion_angry.FBX`), transitions État par défaut↔Fight. Note : `Zombie.controller`
avait été modifié entre-temps par l'utilisateur (nouveau clip de marche `Zombie_Chase_1_Loop_IPC`) —
pris en compte, pas écrasé.

### VFX — mise à jour : asset prêt à l'emploi trouvé, plus de matériau custom nécessaire
Signalé par l'utilisateur après coup : le pack `Assets/JMO Assets/Cartoon FX Remaster/` (déjà dans
le projet) contient `CFXR Prefabs/Misc/CFXR2 Cartoon Fight (Loop).prefab` — un nuage de bagarre
cartoon bouclé tout fait (3 sous-émetteurs `Stars`/`Sparks`/`Smoke`, tous `looping: 1`), exactement
l'effet "scuffle cloud" recherché. Le `FightCloudMaterial.mat` custom construit précédemment (à
partir de `ParticlesUnlit.mat` + `MMParticlesDust.png`) est **retiré**, devenu inutile — pas de
raison de garder un asset de secours moins bon qu'une ressource déjà prête dans le projet.

### MonsterFightBehavior.cs — flash(s) d'impact au début de la mêlée
Nouveau champ `fightStartBurstPrefabs` (GameObject**[]**, peut rester vide) — tous les effets de la
liste sont instanciés ensemble, superposés (fire-and-forget, pas de référence gardée), au même
moment et au même endroit que le nuage en boucle. Tableau plutôt qu'un seul champ : demande
utilisateur explicite de pouvoir en cumuler plusieurs (ex: un "BOOM" + un poof de fumée en même
temps) au lieu d'un seul effet. Contrairement à `fightCloudPrefab`, pas besoin de les détruire
explicitement dans `Interrupt()` : les prefabs CFXR se détruisent tout seuls une fois joués
(comportement standard du pack, `Stop Action: Destroy` déjà configuré dessus).

🔍 **À faire dans l'éditeur** :
- Ajouter `MonsterFightBehavior` sur chacun des 3 prefabs monstres (Add Component, même geste que
  pour `MonsterSocialBehavior`) puis assigner :
  - `Fight Cloud Prefab` → `CFXR2 Cartoon Fight (Loop)`
  - `Fight Start Burst Prefabs` → taille de liste au choix, ex. `CFXR _BOOM_` + `CFXR Magic Poof`
    ensemble, ou n'importe laquelle des autres proposées (`CFXR Hit A`, `CFXR Hit D 3D`, `CFXR3 Hit
    Misc A/F`) — à composer librement.
  Un champ public assigné par Inspector ne peut pas être pré-rempli depuis le code. Reste vide et
  sans effet (le reste du système fonctionne quand même) tant que non fait.
- Remplir `incompatibleTypes` mutuellement sur au moins 2 `MonsterData` pour pouvoir tester.
- Remplacer le clip placeholder `Fight` (`@male_emotion_angry`) par une vraie anim de bagarre une
  fois trouvée.

## 2026-08-11 (suite) — Conversations entre monstres compatibles

Première brique du chantier "interactions sociales monstres" (discussion/bagarre-séparation/assise
repas-lounge/sommeil, scindé en 4 sous-parties — celle-ci ne couvre que les conversations). Plan
complet dans `C:\Users\Virgile\.claude\plans\elegant-prancing-pudding.md`.

Correction en cours de route : le Zombie actif en jeu (`Zombie2/tripo_convert_b9f3...` dans le
prefab, avatar auto-généré) est en fait déjà **Humanoid** — le modèle Generic repéré initialement
(`Zombie1`) est un reliquat désactivé, jamais utilisé. Pas de re-rig nécessaire : les 3 monstres
sont compatibles avec le pack `EverydayMotionPack` sans travail préalable.

### Assets/Scripts/Systems/Hotel/MonsterData.cs
Nouveau champ `compatibleSocialTypes[]` (MonsterType[], vide par défaut — à remplir en Inspector) +
`IsSociallyCompatible()`. Vérifiée dans les deux sens (A doit lister B ET B doit lister A) avant
qu'une conversation démarre.

### Assets/Scripts/Systems/Hotel/MonsterSocialBehavior.cs (nouveau)
Comportement coopératif de plus (même pattern que `MonsterRoamBehavior`/`MonsterNeedSeeker` — pas
de state machine centrale) : recherche opportuniste d'un partenaire compatible à proximité pendant
la balade (pas de pathfinding vers un partenaire lointain), verrouillage mutuel 100% synchrone
(`TryFindPartner()`, aucun yield entre le scan et l'écriture des états — tient lieu de mutex),
marche l'un vers l'autre (`MonsterMover.MoveTo`, phase 1 uniquement — la surcharge 2-phases a un bug
connu sur `OnArrived`), face-à-face (rotation manuelle indépendante de `MonsterMover`), anim
`IsTalking`, timer aléatoire porté par le leader. Point de sortie unique `Interrupt()` (idempotent,
propage au partenaire, référence nullée avant notification pour éviter la récursion) : sert à la
fois pour la fin normale (timer écoulé) et l'interruption forcée (besoin urgent, checkout, despawn).

### Intégration dans le pattern existant
- `SpawnScheduler.cs` : composant ajouté **avant** Needs/Seeker/Roam (ordre important — `AddComponent`
  déclenche `Awake()` immédiatement, un ajout tardif aurait laissé leurs `GetComponent` à `null`).
- `ReservationSystem.cs` : `Activate()` au check-in (comme Roam/Seeker, pas pour les visiteurs repas
  sans chambre) + `Interrupt()` explicite à 3 points de sortie (`CheckoutNow`, `CheckoutEarly`,
  `DebugForceLeave` cas 3) — ne pas compter uniquement sur `OnDestroy()` (filet de sécurité en plus),
  `WalkToAndDestroy` détruit après une marche, pas immédiatement.
- `MonsterNeedSeeker.cs` : `Interrupt()` appelé avant engagement dans `NeedCheckLoop()` — un besoin
  urgent et satisfiable prime sur une conversation en cours.
- `MonsterRoamBehavior.cs` : les 3 checks existants `_seeker.IsSeeking` étendus en `IsBlocked`
  (inclut aussi `_social.IsBusy`).

### Assets/Prefabs/Monsters/Zombie.controller, Vampire.controller, Werewolf.controller
Nouveau paramètre bool `IsTalking` + état `Talk` (clip `@male_talk_stand`, `EverydayMotionPack/
Motion/04_Interaction/`) + transitions État par défaut↔Talk, coexistant avec Idle↔Walk existant
(piloté par `Speed`, inchangé). ⚠️ L'état "par défaut" de Vampire.controller s'appelle `search`, pas
`Idle` — repéré via `m_DefaultState`, pas le nom littéral.

🔍 **À tester en jeu** : configurer `compatibleSocialTypes` sur au moins 2 `MonsterData` (aucune
valeur pré-remplie, à toi de définir les affinités) avant de pouvoir observer une conversation.

### Complément — impact gameplay + anti-boucle
Deux trous comblés suite à question utilisateur (déclenchement/fréquence/impact) :
- **Bonus de satisfaction** : `MonsterSocialBehavior.satisfactionBonus` (défaut 5, à ajuster) appliqué
  aux deux participants via `SatisfactionComponent.ApplyBonus()` — uniquement en fin normale (timer
  écoulé), pas si `Interrupt()` est déclenché de l'extérieur (besoin urgent, checkout).
- **Cooldown anti-boucle** : `cooldownAfterChat` (défaut 20s, à ajuster) posé dans `Interrupt()` à
  chaque fin de conversation (normale ou forcée) — sans ça, deux monstres libres et proches
  pouvaient ré-enchaîner immédiatement, potentiellement avec le même partenaire. `SearchLoop()` et
  `TryFindPartner()` (filtre candidats) vérifient tous les deux `Time.time < _cooldownUntil`.
- Bug latent trouvé et corrigé avant même de le committer : `SatisfactionComponent` mis en cache
  dans `Awake()` aurait été `null` — `SpawnScheduler` l'ajoute *après* `MonsterSocialBehavior` (voir
  note d'ordre plus haut). Fetch paresseux (`GetComponent` au point d'usage) à la place, même
  pattern que `MonsterNeedSeeker`.

### Fix — monstres collés, anim ne se déclenchait pas
Repéré en playtest (2 monstres qui se figent chevauchés, sans jouer l'anim `Talk`). Cause : chaque
monstre visait la **position exacte** du partenaire (`_mover.MoveTo(_partner.transform.position)`),
alors que `MonsterMover` s'arrête à quelques cm de sa cible — les deux essayaient donc de marcher
littéralement l'un sur l'autre. Comme chacun poursuivait en plus la position **mouvante** de l'autre
(recalculée toutes les 0.4s), la condition d'arrivée (`distance ≤ approachArrivalDistance`) ne se
satisfaisait jamais proprement, donc ni la rotation face-à-face ni `SetTalking(true)` ne se
déclenchaient. Fix : nouvelle méthode `ApproachPointNear()` — chaque monstre vise un point à
`approachArrivalDistance` du partenaire, sur l'axe qui les sépare, au lieu de sa position exacte.

### Fix #2 — toujours collés si déjà proches au moment du pairing
Persistait après le fix ci-dessus : la condition d'approche était `distance > approachArrivalDistance`
(uniquement "trop loin") — si deux monstres se pairaient déjà à moins de 1.3m l'un de l'autre
(fréquent, `searchRadius` va jusqu'à 5m), la boucle n'était jamais entrée et ils ne s'écartaient
jamais. Remplacé par une marge tolérée (`approachTolerance = 0.3f`, nouveau tunable) autour de la
distance cible : `Mathf.Abs(distance - approachArrivalDistance) > approachTolerance` — les fait
maintenant s'écarter activement s'ils étaient trop proches, pas seulement se rapprocher s'ils
étaient trop loin.

### Fix #3 — ne se parlent plus du tout (régression du fix #2)
`ApproachPointNear()` retombait dans son cas dégénéré (positions quasi confondues → `dir` quasi
nul) à **chaque tick** tant qu'ils restaient proches, tirant `Random.insideUnitSphere` à nouveau à
chaque fois — chacun partait donc dans une direction différente toutes les 0.4s au lieu de
converger vers un point stable, empêchant toute conversation de se déclencher. Fix : l'axe de
séparation est maintenant figé une seule fois dans `BeginPairing()` (nouveau champ `_approachAxis`)
plutôt que recalculé à chaque `ApproachPointNear()`.

### Fix #5 — oscillation Talk↔Walk
`MonsterMover.Stop()` n'a jamais réinitialisé `_targetSpeed` à 0 (introduit avec le lissage
Idle↔Walk des monstres, voir entrée du 2026-08-11). Quand `MonsterSocialBehavior` appelait `Stop()`
pour figer le monstre pendant la conversation, `Speed` continuait silencieusement à remonter vers 1
en arrière-plan (`Update()` de `MonsterMover` pousse toujours vers `_targetSpeed`) — dès qu'`IsTalking`
repassait à `false` ne serait-ce qu'un instant (ex: besoin urgent qui interrompt), `Speed` étant
resté au-dessus du seuil, l'Animator repartait instantanément en Walk. Fix : `Stop()` met maintenant
`_targetSpeed = 0f`.

### Fix #6 — "ils tournent l'un autour de l'autre" pendant l'approche
Chaque monstre re-visait la position **actuelle** (mobile) du partenaire toutes les 0.4s — avec les
deux qui se rapprochent simultanément, ça crée une dynamique de poursuite instable (orbite) plutôt
qu'une convergence propre. Remplacé par un **point de rendez-vous figé une seule fois** au moment du
pairing (`_approachTarget`, calculé dans `BeginPairing()` à partir du point milieu entre les deux
positions de départ + l'axe de séparation) — chacun marche vers une destination fixe au lieu de
poursuivre une cible mobile.

### Fix #7 — pas vraiment face à face + animations parfaitement synchronisées
Deux retours utilisateur après re-test : (1) les monstres ne s'orientaient pas correctement l'un
vers l'autre en discutant, (2) les deux jouent le même clip démarré au même instant → mouvements
en synchronie parfaite, effet miroir artificiel.
- Orientation : `RotateToFace(Vector3 targetPos)` dépendait de la position **live** du partenaire
  au moment de la rotation — si l'un finissait son approche/rotation avant que l'autre ait fini de
  s'arrêter, la direction calculée pouvait être légèrement obsolète. Remplacé par
  `RotateToFaceDirection(Vector3 dir)` : la direction est déterministe, dérivée de `_approachAxis`
  (figé à `BeginPairing()`, indépendant de toute position live).
- Désynchronisation : léger délai aléatoire (0-0.4s, `Random.Range`) inséré avant `SetTalking(true)`
  côté de chaque monstre indépendamment — ils ne démarrent plus le clip exactement à la même frame.

### Fix #8 — toujours pas face à face + toujours synchronisés (retour utilisateur après capture d'écran)
Approche plus radicale plutôt que d'ajuster encore les marges :
- **Orientation** : la rotation progressive (`RotateToFace`, coroutine sur plusieurs frames) est
  remplacée par un **snap instantané** (`FaceDirectionInstant`) — élimine toute possibilité que la
  coroutine ne se termine pas comme prévu. Direction toujours déterministe (`-_approachAxis`, figée
  à `BeginPairing()`).
- **Synchronisation** : au lieu d'un simple décalage temporel (jugé insuffisant), le leader et le
  follower jouent maintenant des **animations différentes** — nouveau paramètre bool `IsListening` +
  état `Listen` (clip `@male_nod_stand`, `EverydayMotionPack/Motion/04_Interaction/`) sur les 3
  `.controller`, en plus de `IsTalking`/`Talk` existants. Le leader "parle" (`IsTalking`), le
  follower "écoute" (`IsListening`) — plus aucune chance de synchronie visible puisque ce sont deux
  clips distincts. `Interrupt()` coupe maintenant les deux paramètres.
- Champ `faceRotationSpeed` retiré (devenu inutile, rotation instantanée).

### Fix #9 — toujours légèrement décalés (retour utilisateur, capture d'écran)
Animations confirmées bonnes cette fois, mais orientation encore imprécise. Cause probable : l'axe
de rotation (`_approachAxis`) est calculé **avant** tout déplacement, à partir des positions de
départ — mais le chemin NavMesh emprunté pour rejoindre `_approachTarget` n'est pas une ligne
droite (contournement d'obstacles), donc la position finale réelle peut dévier de cet axe. Revenu à
un calcul de direction basé sur la position **réelle** du partenaire — sans risque de valeur
obsolète cette fois, puisqu'on vient de confirmer via `WaitUntil(_partner._hasArrived)` que sa
position est stable (son propre `Stop()` déjà appelé). `_approachAxis` reste utilisé uniquement pour
le calcul du point de rendez-vous (`_approachTarget`), plus pour l'orientation finale.

### Nettoyage logs
Suppression de `EatingSpot.cs` : `"{monster} assis — attend sa nourriture"` (bruit, un log par
repas sans valeur diagnostique — signalé par l'utilisateur comme exemple de log à retirer).

### Logs de diagnostic ajoutés
Toujours pas de conversation observée après le fix #3 — plutôt que continuer à deviner, ajout de
`Debug.Log` à chaque étape clé (`Activate()`, partenaire trouvé dans `TryFindPartner()`, arrivée
face-à-face dans `ConversationRoutine()`, `Interrupt()`) pour localiser précisément où ça bloque au
prochain test. Suspects principaux à vérifier en parallèle, indépendants des bugs déjà corrigés :
`compatibleSocialTypes` probablement toujours vide sur les `MonsterData` (prérequis jamais confirmé
rempli), ou monstres testés en visiteurs repas (sans chambre) — `Activate()` n'est appelé qu'au
check-in, jamais pour les visiteurs, cohérent avec `MonsterRoamBehavior`.

### Fix #4 — robustesse si le composant est ajouté directement sur le prefab
L'utilisateur a ajouté `MonsterSocialBehavior` à la main sur le prefab (au lieu de le laisser
`SpawnScheduler` l'ajouter dynamiquement) pour pouvoir régler ses valeurs par défaut depuis
l'Inspector. Dans ce cas, son `Awake()` se déclenche **pendant** `Instantiate()`, donc avant que
`SpawnScheduler` ait ajouté `MonsterDataReference` juste après — `_dataRef` restait `null`, et
`TryFindPartner()` échouait silencieusement à chaque tentative (`myData == null` → return false),
sans qu'aucune erreur n'apparaisse en Console. Fix : mise en cache des dépendances déplacée
d'`Awake()` vers `Start()` — se déclenche après la fin de l'appel synchrone à
`SpawnScheduler.TrySpawn()` (tous ses `AddComponent`), donc robuste que le composant soit bake sur
le prefab ou ajouté au runtime.

## 2026-08-11 — Lissage Idle↔Walk pour les monstres

Suite du chantier animations, côté monstres cette fois (Zombie/Vampire/Werewolf). Contrairement au
joueur, pas de Blend Tree directionnel ici : les 3 controllers (`Assets/Prefabs/Monsters/*.controller`)
n'ont qu'un seul paramètre `Speed` (0/1) et un seul clip Walk chacun — un blend 8 directions
tomberait dans le même piège que celui résolu côté joueur (`MoveX` collé à 0, les monstres se
réorientant déjà vers leur direction de déplacement via `DirectMove()`).

### Assets/Scripts/Systems/Hotel/MonsterMover.cs
- `Speed` était poussé instantanément à 0 ou 1 (`SetFloat` sans damping) en début/fin de chaque
  phase de déplacement — transition Idle↔Walk abrupte au niveau du paramètre (le seul lissage venait
  du crossfade de la transition Animator, 0.25s).
- Nouveau champ `_targetSpeed` (0 ou 1, fixé par les coroutines de déplacement existantes) + nouvelle
  `Update()` qui approche `Speed` de cette cible en continu via `Animator.SetFloat(hash, target,
  dampTime, deltaTime)`, avec snap à la cible une fois l'écart négligeable (même pattern que
  `TopDownController.SetAnimFloat`, voir entrée précédente). Nouveau tunable `speedBlendDampTime =
  0.15f` (valeur de départ, à ajuster) sur `MonsterMover`.
- S'applique aux 3 monstres sans toucher aux `.controller` (le paramètre `Speed` existant suffit).
- Rotation dans `DirectMove()` : `Slerp` avec facteur fixe non exposé (`10f * Time.deltaTime`, même
  écueil que le joueur — un facteur de blend, pas une vraie vitesse angulaire) remplacé par
  `RotateTowards` + nouveau champ public `rotationSpeed = 360°/s` (valeur de départ, à ajuster dans
  l'Inspector de chaque prefab monstre).

## 2026-08-10 — Blend directionnel de marche fluide (joueur, skin Tall)

Demande : que les animations du joueur suivent la vraie direction de déplacement relative à son
orientation (avant/arrière/côtés/diagonales) au lieu d'un seul clip "marche avant" quel que soit le
sens réel du déplacement, avec des transitions lissées plutôt qu'instantanées. Scope confirmé avec
l'utilisateur : le joueur d'abord (skin "Tall", actif par défaut) — le 2e skin ("Small/Monster")
est en rig Generic, incompatible avec le pack d'animations Humanoid utilisé ici, non touché (voir
TODO Animation A2).

### Assets/Scripts/Systems/Player/TopDownController.cs
- `FixedUpdate()` calcule `localDir = transform.InverseTransformDirection(dir)` juste après la
  rotation du personnage vers sa direction de déplacement — donne `MoveX`/`MoveZ` en espace local
  (gauche/droite, avant/arrière relatifs à l'orientation du perso), ce qu'attend un Blend Tree 2D
  directionnel.
- `Speed`/`MoveX`/`MoveZ` sont désormais poussés à l'Animator via `Animator.SetFloat(id, value,
  dampTime, deltaTime)` (nouveau tunable `moveBlendDampTime = 0.1f`, valeur de départ à ajuster) au
  lieu d'un `SetFloat` instantané — lissage géré nativement par Unity.

### Assets/Art/Models/Player/Tall/Tall.controller
- Nouveaux paramètres float `MoveX`/`MoveZ` (défaut 0), en plus de `Speed` existant.
- Nouveau `BlendTree` ("Walk Blend Tree", `m_BlendType: 2` = Freeform Directional 2D,
  `m_BlendParameter: MoveX` / `m_BlendParameterY: MoveZ`) à 8 enfants — clips directionnels du pack
  `Assets/Plugins/EverydayMotionPack/Motion/02_Move/@male_move_walk_*` (avant/arrière/gauche/droite
  + 4 diagonales), positionnés sur le cercle unité correspondant à leur direction.
- L'état `Walk` (déjà présent, transitions Idle↔Walk sur `Speed` seuil 0.1 inchangées) référence
  maintenant ce BlendTree au lieu d'un clip unique.

🔍 **À vérifier dans l'éditeur avant playtest** : sur l'Animator du skin Tall du prefab Player,
confirmer que **Apply Root Motion est décoché** (le déplacement reste piloté par le Rigidbody, pas
par l'animation — les clips du pack ont probablement du root motion intégré qu'il ne faut pas
appliquer en double, sinon glissement de pieds / double mouvement). Voir TODO Animation A1 pour la
checklist de playtest complète.

## 2026-08-05 (suite 5) — Triplanaire sur les murs + texture sur les blocs d'expansion

### Assets/Shaders/RoomWallFade.shader + RoomWallDither.shader — triplanaire
Cause du souci remonté ("texture inversée d'un mur à l'autre dans RoomPlaceholder") : le mapping UV
d'un Cube Unity est fixé par axe **local**, pas le monde — chaque mur de `RoomPlaceholder`
(`CreateBox`, sans rotation) a un axe "large" différent (Nord/Sud en X, Est/Ouest en Z), donc la
texture semble filée dans une direction différente d'un mur à l'autre.

Fix : les deux shaders (déjà utilisés par `RD_ChambreStandard.mat`/`RD_ChambreVampire.mat`/
`ShaderWall.mat`) échantillonnent maintenant `_BaseMap` en **triplanaire** — projection depuis 3
axes du monde (XY/XZ/ZY), mélangée selon la normale (`SampleTriplanar()`), au lieu des UV du mesh.
Plus aucune dépendance aux UV de la primitive — le fondu par occlusion (`_FadeStrength`) et le
dithering (`_DitherAlpha`) existants sont inchangés. Nouveaux réglages `_TexScale` (tuilage monde)
et `_TriplanarSharpness` sur les matériaux — **valeurs de départ (1 et 4), à ajuster** selon la
taille réelle de la texture.

### BlockData.cs / ExpansionBlock.cs — texture optionnelle sur les blocs d'expansion
Demande : pouvoir aussi mettre une texture sur les blocs (`ExpansionBlockSpawner`), actuellement
couleur plate uniquement.
- `BlockData.blockTexture` (nouveau, optionnel) — nécessite que le matériau du bloc expose
  `_BaseMap` (URP Lit standard, ou `MonsterHotel/RoomWallDither`/`RoomWallFade` si on veut cumuler
  avec le dithering, demandé "éventuellement" — même propriétés, donc déjà compatible sans code
  en plus).
- `ExpansionBlock.ApplyBlockColor()` pousse la texture dans le `MaterialPropertyBlock` (une seule
  fois — `UpdateDamageColor()` ne touche jamais `_BaseMap`, donc elle reste posée entre les deux).

⚠️ Nouveau champ sur `BlockData` (ScriptableObject) : si erreur de build "script class layout
incompatible", fermer/rouvrir l'éditeur.

## 2026-08-05 (suite 4) — RW2 : vraies lignes de grille (remplace le placeholder blanc)

Le matériau placeholder (blanc semi-transparent, posé pour tester la mécanique avant d'avoir une
vraie texture) était toujours en place. Remplacé par un shader dédié — pas besoin de texture.

### Assets/Shaders/BuildGridLines.shader (nouveau)
`MonsterHotel/BuildGridLines` — dessine uniquement les contours de chaque cellule (`frac(uv *
_CellCount)` + test de distance au bord), transparent ailleurs. Propriétés : `_LineColor`,
`_LineWidth` (fraction de cellule), `_CellCount` (nombre de cases X/Y, piloté par code).

### Assets/Materials/BuildGrid_Mat.mat
Reconstruit pour utiliser ce nouveau shader (même nom/emplacement que le placeholder — pas de
réassignation nécessaire sur `GridVisualBuilder`, déjà branché dessus).

### GridVisualBuilder.cs
`RefreshFromGrid()` pilote maintenant `_CellCount` (`material.SetVector`) au lieu de
`mainTextureScale` (qui ne s'appliquait qu'à une texture tuilée, plus utilisée).

## 2026-08-05 (suite 3) — RW1 : retrait de l'animation de rect (effet "swipe" indésirable)

Retour utilisateur : la caméra de P2 "swipe" bizarrement pendant la transition (largeur de
`Camera.rect` animée en continu). Cause : redimensionner en continu le viewport d'une caméra
**perspective** déforme la projection pendant la transition (changement d'aspect ratio effectif) —
artefact visuel, pas juste "trop lent".

### SplitScreenManager.cs — UpdateBlend()
- Mise en page écran (rects `cam0`/`cam1`) repassée en bascule **nette** sur le changement de
  `_merged`, comme avant l'ajout de la fusion lissée.
- Gardé : le cadrage 3D de `PlayerCamera` (position/zoom, piloté par `mergeBlend`) continue de se
  lisser en continu — pas de souci de déformation là (déplacement dans l'espace 3D, pas
  redimensionnement de viewport). La caméra étant déjà en train de se repositionner en douceur au
  moment du cut de mise en page, ça limite quand même la sensation de saut brutal.

## 2026-08-05 (suite 2) — RW1 : transition split ↔ fusion lissée en continu

Retour utilisateur : la bascule split/fusion était un cut instantané (l'hystérésis ne fait que
décider *quand* basculer, pas *comment* la transition se fait visuellement) — pas fluide.

### PlayerCamera.cs — mergeBlend continu (remplace le on/off mergeWithPlayer != null)
- `mergeWithPlayer` reste assigné en continu dès qu'un 2e joueur existe ; nouveau `mergeBlend`
  (0-1) pilote l'intensité réelle du cadrage fusion, mélangé en continu avec le suivi solo
  (`Vector3.Lerp`/`Mathf.Lerp` sur la cible et la distance) — plus de branche binaire, donc plus
  de saut de caméra quand `mergeBlend` varie dans le temps.

### SplitScreenManager.cs — UpdateBlend() remplace ApplyMergeState()
- `_mergeBlend` animé vers l'état cible (`_merged`) via `Mathf.MoveTowards`, vitesse
  `mergeTransitionSpeed` (nouveau tunable HotelConfig, défaut 2 — plus petit = transition plus
  lente/fluide). `UpdateSplitOrMerge()` ne décide plus que l'état cible ; `UpdateBlend()` gère
  toute la transition visuelle.
- `cam1` (P2) **reste toujours active** dès que 2 joueurs sont présents — la largeur de son
  `Camera.rect` s'anime vers 0 au lieu d'un `SetActive(false)` instantané : `cam0` s'élargit de sa
  moitié vers le plein écran pendant que `cam1` se rétracte vers le bord droit et disparaît
  visuellement, sans pop.
- Le trait de séparation (RW1, ajout précédent) suit la même transition — fondu d'alpha
  (`1 - mergeBlend`) au lieu d'un `SetActive` discret, désactivé seulement une fois totalement
  invisible.

### HotelConfig.cs
Nouveau `splitScreenMergeTransitionSpeed` (défaut 2, à ajuster) dans la section "Écran scindé".

## 2026-08-05 (suite) — RW1 : paramètres split-screen déplacés dans HotelConfig

Demande : centraliser les réglages split-screen dans `HotelConfig` (paramétrage général du jeu),
plutôt que sur `SplitScreenManager` — cohérent avec tous les autres tunables du projet
(`buildModeExtraDistance`, `roomCursorSpeed`, etc., déjà sur `HotelConfig`).

### HotelConfig.cs — nouvelle section "Écran scindé (RW1)"
`splitScreenCameraHeight`, `splitScreenMergeDistance`, `splitScreenMergeHysteresis`,
`splitScreenMergeDistancePerUnit`, `splitScreenDividerWidth`, `splitScreenDividerColor` — mêmes
défauts qu'avant, juste déplacés/dupliqués comme source de vérité.

### SplitScreenManager.cs — ApplyConfig()
Reprend ces valeurs depuis `HotelConfig.Instance` dans `Awake()` (avant `EnsureCameras()`, pour que
les caméras soient créées avec les bonnes valeurs) — mêmes champs gardés sur le composant comme
repli si `HotelConfig` est absent, mais `HotelConfig` prime désormais. Modifiable dans l'asset
`HotelConfig` en Edit mode (pas besoin de lancer le jeu).

⚠️ Nouveaux champs sur `HotelConfig` (ScriptableObject) : si erreur de build "script class layout
incompatible", fermer/rouvrir l'éditeur (avertissement standard déjà vu plusieurs fois ce chantier).

## 2026-08-05 — RW1 : trait de séparation au milieu de l'écran

### SplitScreenManager.cs — CreateDivider()
- Trait vertical fin (Canvas UI dédié, `sortingOrder = 100`, au-dessus de tout) marquant nettement
  la coupure gauche/droite en mode scindé. Visible uniquement à 2 joueurs et hors fusion (caché en
  solo et en mode fusion caméra) — `UpdateDividerVisibility()`, appelée chaque frame avec
  `UpdateSplitOrMerge()`.
- Nouveaux tunables `dividerWidth` (défaut 4px) et `dividerColor` (défaut noir) — à ajuster.

## 2026-08-03 (suite 3) — RW1 : fusion caméra quand les joueurs sont proches

Playtest RW1 confirmé fonctionnel. Ajout demandé : l'écran ne doit pas rester scindé en permanence
— si P1 et P2 sont proches, une seule caméra plein écran cadre les deux ; au-delà d'une certaine
distance, retour au split gauche/droite classique. **Toujours une coupure verticale nette en mode
scindé, jamais orientée selon leur position relative** (contrairement à l'ancien système adaptatif
— la fusion résout le même besoin sans réintroduire une découpe qui pivote).

### PlayerCamera.cs — mode fusion (mergeWithPlayer)
- Nouveau champ `mergeWithPlayer` : si renseigné, la caméra cadre le point milieu entre `player` et
  lui, avec un dézoom qui grandit avec leur distance (`mergeDistancePerUnit`, défaut 0.6, à
  ajuster). Refactor : le calcul de shake extrait dans `ComputeShakeOffset()`, partagé entre le
  suivi normal et le mode fusion (évitait la duplication).

### SplitScreenManager.cs — bascule split/fusion avec hystérésis
- Nouveaux tunables `mergeDistance` (défaut 15, à ajuster) et `mergeHysteresis` (défaut 3) — bascule
  vers fusion sous `mergeDistance - hysteresis`, vers split au-dessus de `mergeDistance +
  hysteresis`, marge dans les deux sens pour éviter un flicker pile au seuil.
- En fusion : `cam1` désactivée, `cam0` passe plein écran et cadre les deux joueurs via
  `mergeWithPlayer`.
- **Force toujours le mode scindé si un des deux joueurs est en train de construire** (RW2) — la
  fusion ne s'applique jamais pendant la construction, pour ne pas interférer avec le dézoom/
  cadrage curseur déjà géré par `PlayerCamera` en mode normal.
- Nettoyage : retrait des logs `[RW1-Check]` (dans `SplitScreenManager`/`GameManager`/
  `TopDownController`/`PlayerCamera`) ajoutés pendant le diagnostic du chantier précédent —
  diagnostic terminé, plus nécessaires.

## 2026-08-03 — RW1 : écran scindé statique (remplace l'adaptatif)

Plan validé avec l'utilisateur avant implémentation (`.claude/plans/elegant-prancing-pudding.md`).
Décision : split **statique vertical** (P1 gauche, P2 droite), ne bouge plus jamais selon la
position des joueurs — remplace le split adaptatif (`ProjectDawn.SplitScreen`/`SplitScreenEffect`,
composait dynamiquement façon Voronoi selon la position des deux joueurs).

### SplitScreenManager.cs — rework complet, plus de dépendance au package
- `cam0`/`cam1` sont maintenant deux caméras natives Unity avec un `Camera.rect` **fixe** — plus de
  `RenderTexture`/command buffer de composition. Nouvelle méthode `UpdateLayout()` : 1 joueur actif
  → sa caméra plein écran, 2 joueurs → moitié gauche (P1) / droite (P2).
- Nouveau `GetCameraForPlayer(PlayerInput)` (remplace l'ancien `GetPlayerAt(int)`, plus utile
  puisque `PlayerCamera` connaît directement son joueur).
- Câble enfin `cameraHeight` (orphelin depuis un audit précédent, jamais lu) comme distance de
  base caméra ↔ joueur.
- La Main Camera reste dans la scène (toujours taguée `MainCamera`) mais ne rend plus la scène —
  `cam0`/`cam1` couvrent déjà 100% de l'écran à elles deux, un rendu en plus par-dessus casserait
  l'affichage. `cullingMask = 0` en code (`SplitScreenManager.Awake()`) : ne rend plus rien de
  visible, mais reste **activée** pour que `Camera.main` reste non-null (nécessaire aux scripts UI
  world-space qui s'y réfèrent — voir plus bas).

### PlayerCamera.cs (nouveau) — remplace CameraShaker + BuildModeCameraZoom + BuildCursorTargetProvider
Sous l'ancien système, `SplitScreenEffect` réécrivait `transform.position` en premier chaque frame,
obligeant les 3 scripts ci-dessus à s'exécuter après lui (Script Execution Order, ajouté à la main
pour RW2). Avec des caméras indépendantes, plus besoin de cette dépendance d'ordre — un seul
composant possède entièrement sa position :
- Suit son joueur, ou son curseur de placement pendant la construction (lissé — reprend le rôle de
  `BuildCursorTargetProvider`, mais en lisant `TopDownController.BuildCursorWorldPos` directement,
  plus besoin du hook `ISplitScreenTargetPosition` du package).
- Dézoom construction (reprend `BuildModeCameraZoom`, mêmes tunables `HotelConfig.buildModeExtraDistance`/
  `buildModeZoomLerpSpeed`/`buildModeCameraFollowSpeed`, inchangés).
- Bascule le culling mask du layer `BuildGrid` (idem).
- Absorbe le shake (logique reprise à l'identique de `CameraShaker.cs`) — expose
  `Shake(amplitude, duration, frequency)`.
- **Suppression sans risque** : `CameraShaker.cs`, `BuildModeCameraZoom.cs`,
  `BuildCursorTargetProvider.cs` supprimés — tous leurs composants étaient créés par code dans
  `SplitScreenManager.CreateCamera()`/`EnsureCameras()`, jamais sérialisés dans la scène, donc
  aucun risque de "Missing Script" au chargement.

### Effet de bord trouvé et corrigé : `Camera.main` scopé par joueur
Trouvé en explorant l'impact du changement : `RoomPlacer.cs`/`FurniturePlacer.cs` (×2)/
`DecorationPlacer.cs` calculaient la direction du curseur de placement via
`Camera.main.transform.right/forward` — global, pas par joueur. Ça "marchait" avec une seule
caméra adaptative partagée, mais casse pour le joueur qui n'est pas sur la caméra taguée
`MainCamera` une fois chaque joueur indépendant. Même souci dans `BuildingDither.cs`/
`RoomDither.cs` (`IsOccludingAnyPlayer()`, testaient l'occlusion via une position `Camera.main`
unique contre `RoomPlacer.All`).

Fix : `TopDownController.cs` résout et expose `PlayerCamera` (caméra de CE joueur, via
`SplitScreenManager.GetCameraForPlayer`, résolue à la demande plutôt que mise en cache pour éviter
une valeur périmée si `Start()` tourne avant l'assignation de caméra). Les 4 sites de calcul de
curseur et les 2 boucles d'occlusion utilisent maintenant la caméra du joueur concerné plutôt
qu'une `Camera.main` globale (repli défensif sur `Camera.main` conservé dans les placers si
`PlayerCamera` n'est pas encore assignée).

**Pas touché** (limitation pré-existante, pas aggravée par ce chantier) : les canvases world-space
qui font `canvas.worldCamera = Camera.main` ou billboardent vers elle (`WorldPrompt`, `GuestBubble`,
`MealVisitorBadge`, `RoomSign`, `WaitingGaugeUI`, `EmployeeInteractionWheel`) — déjà approximatif
avant (un seul repère caméra partagé entre les deux joueurs), inchangé par ce chantier.

### FeedbackManager.cs
`ShakeCamera()` cible `GetComponent<PlayerCamera>()` au lieu de `GetComponent<CameraShaker>()`.

⚠️ **Nettoyage optionnel, différé, pas bloquant** (à faire une fois RW1 confirmé en jeu) :
- Retirer `SplitScreenRendererFeature` de `Assets/Settings/PC_Renderer.asset` (Inspector).
- Retirer la dépendance `com.projectdawn.splitscreen` de `Packages/manifest.json`.
- Script Execution Order (`SplitScreenEffect` avant `CameraShaker`/`BuildModeCameraZoom`, réglé
  pour RW2) n'a plus d'effet utile (scripts disparus) — peut être laissé tel quel ou nettoyé.

---

## 2026-07-27 (suite 7) — RW2 : dézoom caméra + grille visuelle en construction

Plan validé avec l'utilisateur avant implémentation (voir `.claude/plans/elegant-prancing-pudding.md`
si besoin de le retrouver). Découverte clé pendant l'exploration : le curseur de placement libre
(indépendant de la position du joueur) **existait déjà** sur `RoomPlacer`/`FurniturePlacer`/
`DecorationPlacer` (stick droit / flèches, `TopDownController.CursorInput`) — donc le vrai
chantier n'était que : dézoom caméra, cadrage sur le curseur, grille visuelle au sol, verrouillage
du déplacement pendant la construction. Tout ça 100% indépendant par joueur en coop écran splitté.

### Zoom par caméra sans patch du package tiers
`SplitScreenEffect` (package `ProjectDawn.SplitScreen`) synchronise `orthographicSize`/
`fieldOfView`/`projectionMatrix` de force sur toutes les caméras joueur depuis une valeur unique
partagée à chaque frame — aucun override par écran possible par ce biais. Mais le "zoom" réel de ce
rig (caméra perspective top-down) vient de la **position** caméra (`Distance` ajoutée après
positionnement), et le projet a déjà un précédent exact pour l'overrider post-hoc :
`CameraShaker.cs` ajoute un offset à `transform.position` dans son propre `LateUpdate()`, en
s'appuyant sur le fait qu'il s'exécute après `SplitScreenEffect`. Nouveau
**`BuildModeCameraZoom.cs`** (sur `cam0`/`cam1`, à côté de `CameraShaker`) reproduit exactement ce
principe : lerp un `_extraDistance` selon `IsBuilding` du joueur propriétaire de cette caméra,
ajoute `transform.rotation * (0,0,-_extraDistance)`. Additif, se compose proprement avec le shake.

### La caméra cadre le curseur, pas le corps du joueur
Le package expose déjà un point d'extension officiel pour ça (pas de patch nécessaire) :
`ISplitScreenTargetPosition`/`ISplitScreenTranslatingPosition`, scannés sur les `MonoBehaviour` du
même GameObject que `SplitScreenEffect`. Nouveau **`BuildCursorTargetProvider.cs`** (ajouté
automatiquement sur la Main Camera par `SplitScreenManager.EnsureCameras`) : si le joueur de cet
écran est en train de construire, retourne un lerp interne (par écran) vers
`TopDownController.BuildCursorWorldPos` au lieu de la position brute du joueur — évite un saut de
caméra à l'entrée/sortie du mode. Signatures en `float3` (Unity.Mathematics, castées explicitement
vers/depuis `Vector3`) — même API que le package.

### Mapping joueur ↔ écran
`SplitScreenManager` ne trackait que `List<Transform> _targets`. Ajout d'un `List<PlayerInput>
_players` parallèle (mêmes points d'ajout/retrait dans `HandlePlayerJoined`/`HandlePlayerLeft`) +
`public PlayerInput GetPlayerAt(int screenIndex)` — source de vérité réutilisée par
`BuildModeCameraZoom`/`BuildCursorTargetProvider` pour résoudre "quel joueur possède cet écran".

### Agrégation IsBuilding / BuildCursorWorldPos
`TopDownController` cachait déjà (sans s'en servir) une référence `RoomPlacer` — étendu pour
cacher aussi `FurniturePlacer`/`DecorationPlacer`, et exposer :
- `IsBuilding` : `OR` des trois `IsPlacing`, **avec une exception** — `FurniturePlacer.IsPlacing`
  est ignoré tant que `PanelIsControlling` est vrai. Trouvé en explorant `RoomManagementPanel` :
  `SelectButton()` appelle déjà `StartPlacing()` (donc `IsPlacing = true`) dès qu'on **survole**
  un item de la liste meuble, avant toute confirmation — sans cette exception, naviguer
  rapidement dans le menu aurait fait clignoter le zoom/la grille à chaque appui.
- `BuildCursorWorldPos` : position du curseur/ghost du placer actif, sinon position du joueur.
- Nouveaux getters `CursorWorldPos` sur `RoomPlacer.cs`/`DecorationPlacer.cs` (`FurniturePlacer`
  avait déjà `GhostWorldPos`/`IsPlacing` publics, rien à ajouter).

### Verrouillage du déplacement pendant la construction
`TopDownController.FixedUpdate()` coupe la vélocité et retourne tôt si `IsBuilding`. Pas juste
cosmétique : sur clavier, P2 utilise les **flèches** à la fois pour le déplacement ET le curseur de
placement (`PlayerInputBinder`) — sans ce verrou, bouger le curseur ferait aussi marcher le
personnage. Appliqué uniformément à la manette aussi (où il n'y a pas cette collision, sticks
gauche/droit déjà séparés) pour un ressenti cohérent entre les deux devices.

### Grille visuelle — grandit avec l'hôtel, visible seulement côté joueur qui construit
- `GridManager.cs` : nouvel event `OnGridExpanded` + `GetOverallBounds()`, mis à jour dans
  `OpenExpandedCell()` (le vrai moment où une cellule étendue devient jouable, **pas**
  `RegisterExpandedOccupant()` qui ne fait que marquer l'empreinte d'un bloc encore intact/bloqué
  — distinction trouvée en traçant les deux call sites dans `ExpansionBlock.cs`).
- Nouveau **`GridVisualBuilder.cs`** (`Systems/Grid/`) : un seul Quad, position/échelle/tuilage
  entièrement calculés depuis `GridManager` (pas de réglage manuel dans l'Inspector — convention
  déjà établie ce chantier après plusieurs oublis de setup manuel). Pas de collider (ne doit pas
  perturber les raycasts sol de `DecorationPlacer`/`GridManager.WorldToCell`). Se reconstruit sur
  `GridManager.OnGridExpanded`, pas de polling par frame.
- Visibilité isolée par joueur via layer + culling mask (technique standard Unity pour "même monde,
  caméras différentes") : nouveau layer `BuildGrid`, exclu par défaut du `cullingMask` de `cam0`/
  `cam1` à la création (`SplitScreenManager.CreateCamera`), réactivé par `BuildModeCameraZoom` en
  même temps que le zoom — un seul composant centralise tout l'état visuel "construction" de
  chaque caméra.

### HotelConfig.cs — nouveaux tunables
`buildModeExtraDistance` (défaut 12), `buildModeZoomLerpSpeed` (défaut 4), 
`buildModeCameraFollowSpeed` (défaut 15) — valeurs de départ, **à ajuster par l'utilisateur**.

⚠️ **Setup Unity manuel requis avant que ça fonctionne** :
1. Créer le Layer `BuildGrid` (Project Settings → Tags and Layers).
2. Project Settings → Script Execution Order → épingler `SplitScreenEffect` avant `CameraShaker`
   et `BuildModeCameraZoom` (fiabilise aussi une fragilité latente du shake existant, qui
   fonctionnait jusqu'ici par ordre d'exécution implicite non garanti).
3. Créer un GameObject avec `GridVisualBuilder` (à côté de `GridManager` dans la scène) +
   assigner un matériau tuilé (unlit, transparent, lignes de grille) sur son champ `Grid Material`.

⚠️ Nouveaux champs sur `HotelConfig` (ScriptableObject) : si erreur de build "script class layout
incompatible", fermer/rouvrir l'éditeur (avertissement standard déjà vu plusieurs fois ce chantier).

## 2026-07-27 (suite 6) — G6-B22 reverté : BuildNavMesh() causait un gros ralentissement

Playtest : passer les rebakes automatiques en `BuildNavMesh()` (reconstruction complète, voir
entrée précédente) causait un **gros ralentissement en jeu à chaque pose de pièce**. Cause : cette
méthode reconstruit tout le NavMesh de l'hôtel entier à chaque appel — le coût grossit avec la
taille de l'hôtel, contrairement à `UpdateNavMesh()` (incrémental) qui ne retouche que les tuiles
changées.

### RoomPlacer.cs / ExpansionBlock.cs — retour à UpdateNavMesh()
- Revert complet : les deux rebakes automatiques repassent sur `surface.UpdateNavMesh(surface.navMeshData)`.
- Le vrai filet de sécurité reste le fix G6-B20 (plafond `partialPathMaxSkip` +
  `Debug.LogWarning("[NavMesh] ...")` dans `MonsterMover`) : une zone mal connectée par
  l'incrémental redevient visible/diagnosticable via ce warning au lieu de faire traverser un mur en
  silence. Un Bake manuel complet (Inspector → NavMeshSurface → Bake) reste le recours ponctuel si
  ce warning apparaît après une construction — pas une nécessité systématique.

## 2026-07-27 (suite 5) — G6-B22 : rebake NavMesh automatique passé en reconstruction complète

Après tous les fix précédents (G6-B20/B21), un Bake manuel complet réglait systématiquement les
soucis de connexion NavMesh, mais il fallait le refaire après chaque nouvelle construction en jeu —
signe que le rebake **automatique** du jeu n'était pas fiable.

### RoomPlacer.cs / ExpansionBlock.cs — BuildNavMesh() au lieu de UpdateNavMesh()
- Les deux rebakes automatiques (placement d'une room, destruction d'un bloc d'expansion)
  utilisaient `NavMeshSurface.UpdateNavMesh(surface.navMeshData)` — une mise à jour **incrémentale**
  (patch des tuiles modifiées) plutôt qu'une reconstruction complète. C'est cet incrémental qui
  laissait des zones mal connectées sans le signaler, expliquant tous les soucis de ce chantier.
- Fix : les deux passent à `surface.BuildNavMesh()` (reconstruction complète, **synchrone** —
  cette version du package AI Navigation n'expose pas d'équivalent async pour un rebuild complet,
  contrairement à `UpdateNavMesh` qui est async). Plus besoin de Bake manuel après une construction
  en jeu normal.
- **Erreur de compil initiale** : première tentative avec `BuildNavMeshAsync()`, qui n'existe pas
  dans cette version du package (`com.unity.ai.navigation@78534c21b27d`) — corrigé en `BuildNavMesh()`.
- Compromis : une reconstruction complète coûte un peu plus cher qu'un patch incrémental et bloque
  le temps du build (synchrone), mais ne se déclenche que sur une action de construction du joueur
  (placement de room, expansion), pas à chaque frame — impact négligeable en pratique.

## 2026-07-27 (suite 3) — G6-B21 : le "bug NavMesh" du cuisinier n'en était pas un — prefab manquant

Après le fix voxel size (G6-B20), le cuisinier traversait **toujours** les murs et apparaissait en
plus à moitié enfoncé dans le sol. Fausse piste NavMesh abandonnée : recherche du GUID de
`EmployeeData_Cook.asset` → `characterPrefabs[0]` (`3a70582c57024f34593855d8df955585`) dans tout le
projet — aucun fichier `.meta` ne le référence, seulement d'anciens logs `AssetImportWorker`.
**Le prefab du skin cuisinier a été supprimé/déplacé, la référence est cassée.**

Conséquence dans `EmployeeManager.Hire()` : `assignedPrefab` résout à `null`, donc repli sur
`GameObject.CreatePrimitive(PrimitiveType.Capsule)` — sans `NavMeshAgent` (`MonsterMover` ne fait
alors plus aucun pathfinding, ligne droite pure → traverse les murs) et avec un pivot centré
(apparaît à moitié dans le sol une fois posée à hauteur du plancher). Les deux symptômes rapportés
s'expliquaient entièrement par ce repli, rien à voir avec le NavMesh.

### EmployeeManager.cs — Hire() : repli capsule blindé
- Ajoute un `Debug.LogWarning("[Employés] ... utilise une capsule de repli.")` explicite dès qu'un
  rôle tombe sur ce repli — pour que ce cas soit immédiatement identifiable en console au lieu de
  ressembler à un bug NavMesh sur un autre rôle à l'avenir.
- Ajoute un `NavMeshAgent` sur la capsule de repli, pour qu'elle respecte au moins les murs en
  attendant que le vrai prefab soit réassigné.

⚠️ **Action requise (Unity, pas de code)** : ouvrir `Assets/Data/Employee/EmployeeData_Cook.asset`,
réassigner le bon prefab dans `Character Prefabs` (actuellement "Missing").

## 2026-07-27 (suite 2) — G6-B20 : cause réelle du mur traversé par le cuisinier trouvée — voxel size

Le fix précédent (plafond sur les branches `PathPartial`/`PathInvalid`) empêchait le cuisinier de
traverser en silence, mais il continuait à passer à travers un mur spécifique **sans aucun
warning** — donc pas un problème de filet de secours cette fois. Confirmé par capture d'écran du
gizmo NavMesh : le maillage rose continuait tout droit à travers le mur, des deux côtés, sans
interruption. Un rebake complet seul ne changeait rien (testé).

**Cause** : la voxel size par défaut du `NavMeshSurface` (~6.7cm, calculée depuis `agentRadius =
0.2`) était trop grossière pour capturer un mur de ~5cm d'épaisseur (`RoomPlaceholder.wallThickness`
sur les petites pièces façon Cuisine) — le mur disparaissait pendant la voxelisation. C'est la piste
envisagée initialement en tout début de diagnostic, avant qu'on ne parte sur la piste (correcte
mais partielle) du filet `PathPartial`.

**Fix (Unity, pas de code)** : `NavMeshSurface` → coché `Override Voxel Size` → `0.02` → Bake.
Confirmé résolu en jeu (capture d'écran du gizmo : mur bien bloquant des deux côtés). Corrige aussi
probablement le "cuisinier enfoncé dans le sol" signalé au passage — même zone de maillage mal
formée.

## 2026-07-27 (suite) — G6-B20 : cuisinier traversait encore les murs (branche PathInvalid)

Playtest : après le fix du filet `PathPartial`, le réceptionniste se rend de nouveau correctement
au comptoir resto **une fois un rebake complet fait** (bouton Bake, pas l'incrémental) — confirme
que `UpdateNavMesh` (incrémental) pouvait laisser une zone dans un état déconnecté incohérent après
plusieurs rebakes successifs (placements de rooms/blocks). Pas de fix de code pour cette partie —
juste confirmé par le rebake manuel.

Mais le cuisinier traversait toujours les murs en allant au resto, **sans aucun warning** dans la
console — signe que ce n'était pas la branche `PathPartial` cette fois.

### MonsterMover.cs — NavigateTo() : même plafond appliqué à la branche PathInvalid
- Le `else` final de `NavigateTo()` (`path.status` ni `PathComplete` ni `PathPartial`, donc
  `PathInvalid` — aucun chemin trouvé du tout) faisait encore `DirectMove(target)` sans aucune
  limite ni warning — même bug que celui corrigé pour `PathPartial`, juste sur une branche
  différente, donc pas couvert par le fix précédent.
- Fix : même logique — sous `partialPathMaxSkip` (distance à vol d'oiseau depuis la position
  actuelle), complète en ligne droite comme avant ; au-delà, reste sur place et log
  `[NavMesh] ... aucun chemin trouvé (...) vers ..., reste sur place plutôt que de traverser un mur.`

## 2026-07-27 — G6-B20 : monstres traversant les murs

Diagnostic en jeu : le NavMesh baké est confirmé propre partout (carve correct), donc pas un souci
de voxel size/épaisseur de murs comme envisagé initialement.

### MonsterMover.cs — NavigateTo() : filet PathPartial plafonné
- Vraie cause trouvée : le filet ajouté pour G6-B16 (réceptionniste hôtel↔resto) complétait le
  trajet en ligne droite **sans aucune limite de distance** dès que `CalculatePath` renvoyait
  `PathPartial` — traversait donc des murs entiers si le NavMesh avait une zone déconnectée loin de
  la cible.
- Nouveau champ `partialPathMaxSkip` (défaut **2.5m**, à ajuster) : le filet ne complète en ligne
  droite que si la distance restante après le dernier point NavMesh valide est sous ce seuil
  (bordure mal snapée, léger décalage de porte). Au-delà, le personnage reste au dernier point
  atteignable + `Debug.LogWarning("[NavMesh] ...")` pour repérer précisément la zone déconnectée.
- **Compromis assumé** : des personnages peuvent réapparaître "bloqués" dans les cas de vraie
  déconnexion NavMesh (au lieu de traverser silencieusement) — c'est voulu, ça rend le problème
  sous-jacent diagnosticable via le warning plutôt que de le masquer. Si ça arrive, chercher le
  warning dans la console pour localiser la zone à corriger (rooms mal jointes, gap non baké).
  Possiblement lié à G6-B13 (réceptionniste parfois bloqué vers le comptoir resto).

## 2026-07-23 — Bug : position ET rotation du prefab cuisinier reset à l'embauche

### EmployeeManager.cs — Hire()
- `Instantiate(prefab, pos, Quaternion.identity)` écrasait la rotation définie sur le prefab
  (`Quaternion.identity` en dur) — fix initial : `prefab.transform.rotation`.
- Même souci trouvé côté position juste après : `pos` était toujours `spawnPoint.position` telle
  quelle, ignorant le décalage local du prefab. Fix : `pos = spawnPoint.position + prefab.transform.position`
  (préserve le décalage local du prefab plutôt que de forcer le spawnPoint brut).
- **Note** : `SpawnScheduler.cs` (spawn des monstres) a le même `Quaternion.identity` en dur
  (ligne ~130) — pas touché ici (pas signalé comme problème côté monstres), mais même famille de bug si ça se manifeste un jour.

## 2026-07-22 (suite 3) — G5 : détritus aléatoires en balade

### MonsterData.cs — nouveaux champs `roamLitterChance` / `litterPrefabs`
- `roamLitterChance` (0-1, défaut **0.1**, à ajuster) : probabilité qu'un monstre laisse un détritus
  à chaque étape de balade.
- `litterPrefabs[]` : prefabs de détritus propres à ce type de monstre. Vide par défaut sur tous les
  assets existants — **aucun effet tant que non rempli dans l'Inspector**.

### MonsterRoamBehavior.cs — TrySpawnLitter()
- Appelée à chaque nouveau waypoint pendant la phase de roam (même cadence que
  `waypointInterval`) : tirage indépendant sur `roamLitterChance`, instancie un `litterPrefabs`
  aléatoire à la position courante du monstre si succès.
- Détritus non rattaché à une chambre (`DebrisInstance.Init(null)`), même pattern que
  `EatingSpot.SpawnMealDebris` — ramassable par le joueur ou un nettoyeur via `DebrisInstance.All`
  sans changement côté `CleaningEmployeeAI`/`CleaningInteractor`.

⚠️ Nouveaux champs sur `MonsterData` (ScriptableObject) : si Unity affiche une erreur de build
"script class layout incompatible between the editor and the player", fermer/rouvrir l'éditeur.

## 2026-07-22 (suite 2) — U5 : enchaînement auto table → chaises

### FurnitureData.cs — nouveau champ `matchingChair`
- Lien optionnel d'une table vers la `FurnitureData` de sa chaise assortie. Vide par défaut sur tous
  les assets existants (nouveau champ ScriptableObject) — **à assigner manuellement dans l'Inspector
  pour chaque table** pour activer l'enchaînement.

### FurniturePlacer.cs — TryPlace() enchaîne sur la chaise après la table
- Si `matchingChair` est renseigné sur la table posée, bascule directement le ghost sur cette
  `FurnitureData` (mode `requiresChairSlot`) au lieu de fermer le placement ou de relancer la même
  table (`multiPlace`) — le joueur n'a plus besoin de rouvrir le menu meuble pour poser les chaises.
- Volontairement pas de passage par `StartPlacing()` : ça aurait recentré le curseur sur le milieu de
  la pièce et réinitialisé le callback `_onDone` en cours. Le curseur reste donc là où la table vient
  d'être posée, prêt à snapper sur ses `ChairSlot`.
- Chaque chaise posée continue de proposer la suivante automatiquement (comportement existant de
  `PlaceOnChairSlot()`) — le joueur sort du mode chaise en annulant (clic droit / touche annuler).

⚠️ Nouveau champ sur `FurnitureData` (ScriptableObject) : si Unity affiche une erreur de build
"script class layout incompatible between the editor and the player", fermer/rouvrir l'éditeur.

## 2026-07-22 (suite) — G6 Phase 4 activée (Phase 3 abandonnée, Phase 5 en pause)

### MonsterNeedSeeker.cs — Conversion visiteur→client activée par défaut
- `stayAfterMealChance` : 0 → **0.25** (valeur de départ, à ajuster).
- `MealVisitorBadge` retiré du monstre au moment de la conversion — sinon le badge orange "Repas"
  restait affiché après qu'il soit devenu un vrai client en attente de chambre (confusion visuelle).
- Vérifié avant activation : `MonsterNeedsComponent.ActivateDecay()` est idempotent (juste
  `_decayActive = true`), pas de risque de double-activation si `TryAssignRoom()` la rappelle sur un
  monstre déjà en decay depuis son passage en visiteur repas.
- **Limite connue, non traitée (cas rare)** : si un monstre a plusieurs besoins définis, un 2e besoin
  pourrait redevenir critique pendant qu'il attend encore en file de réception hôtel (converti mais
  pas encore casé) — le ferait potentiellement repartir vers une facilité alors qu'il est déjà en
  attente d'une chambre. Pas de monstre actuel avec plusieurs besoins à ma connaissance, donc pas
  traité pour rester scopé — à revisiter si ça devient un cas réel.

### G6-P3 (heures précises) abandonnée, G6-P5 (enchaînement d'activités) en pause
- Décision utilisateur — TODO mis à jour en conséquence.

## 2026-07-22 — Deux réceptionnistes convergeaient sur le même poste

Remonté : avec 2 réceptionnistes, quand un poste était vide et l'autre occupé, **les deux**
rejoignaient le poste occupé (et inversement) — `IsClaimed` empêchait bien de cibler le même
monstre, mais rien n'empêchait de se retrouver physiquement au même comptoir.

### ReceptionEmployeeAI.cs — Un poste = au plus un réceptionniste à la fois
- Remplace l'ancienne alternance par préférence (`_preferRestaurantNext`, un simple booléen sans
  coordination entre employés) par un vrai **poste assigné par employé** (`_currentPost`), coordonné
  via un registre statique de tous les réceptionnistes actifs.
- Règle : chaque employé tient son poste tant qu'il y a du travail dessus. S'il n'y a rien à faire à
  son poste, il ne va aider l'autre poste **que si personne d'autre ne le couvre déjà**
  (`CountAtPost(...) == 0`) — sinon il reste sur place.
- Premier passage : s'assigne au poste le moins couvert (priorité hôtel en cas d'égalité, ex. tout
  premier réceptionniste embauché).
- Avec un seul réceptionniste, ça revient exactement au comportement précédent (alterne librement,
  puisque l'autre poste n'est par définition jamais couvert par quelqu'un d'autre).
- **⚠️ Pas encore testé en jeu avec 2 réceptionnistes** — à confirmer.

## 2026-07-21 (suite 14) — Debug : forcer le spawn client chambre / visiteur repas

### SpawnScheduler.cs — Deux nouveaux menus de debug, sans attendre le spawn naturel
- `TrySpawn()` accepte maintenant `forceVisitor` (impose visiteur repas / client chambre, sans
  tirage aléatoire) et `bypassGates` (ignore fenêtre horaire, seuil légendaire, plafond
  `maxPending` — uniquement pour le spawn forcé de debug, jamais le cycle normal).
- **"Debug : Spawner un client chambre"** — force un monstre aléatoire du pool à rejoindre
  directement la file de réception hôtel.
- **"Debug : Spawner un visiteur repas"** — force un monstre ayant au moins un besoin défini
  (`MonsterData.needs`) à arriver directement comme visiteur repas (droit au restaurant, pas de
  chambre). Avertit si aucun monstre du pool n'a de besoin défini.
- Accessible clic droit sur le composant `Spawn Scheduler` dans l'Inspector en Play Mode, à côté du
  "Spawner un monstre maintenant" déjà existant.

## 2026-07-21 (suite 13) — Debug : terminer le séjour d'un client chambre

### MonsterDebugTools.cs / ReservationSystem.cs — Nouveau menu contextuel
- `ReservationSystem.DebugFinishStay(monster)` : déclenche immédiatement un check-out **normal**
  (comme si la durée de séjour était naturellement écoulée) — pourboire calculé sur la satisfaction
  actuelle, contrairement à "Forcer le départ" (`DebugForceLeave`) qui traite ça comme un départ en
  colère sans pourboire. Sans effet si le monstre n'occupe pas de chambre.
- Nouveau menu contextuel **"Debug : Terminer le séjour"** sur `MonsterDebugTools` (clic droit sur
  le composant en Play Mode, à côté de "Forcer le départ" déjà existant) — utile pour tester le
  cycle check-out/nettoyage/réattribution sans attendre la durée complète du séjour.

## 2026-07-21 (suite 12) — Chambre nettoyée non réassignable (diagnostic)

Remonté : une chambre libérée puis nettoyée ne peut plus être réassignée à un monstre en attente à
la réception. Relu `RoomInstance.TryAutoClean()`/`GetCompatibleRooms()` sans trouver de bug évident
sur le papier — logs de diagnostic ajoutés pour confirmer où ça bloque réellement :
- `RoomInstance.TryAutoClean()` logue explicitement quand une chambre redevient `Empty`.
- `ReservationSystem.CheckInNext()` logue, quand aucune chambre compatible n'est trouvée, l'état
  exact de chaque chambre du bon type (`State`, `IsFullyFurnished`) — ou signale qu'aucune chambre
  de ce type n'existe du tout.
- **À tester** : reproduire (chambre libérée → nettoyée → monstre en attente non réaffecté),
  regarder si le log "entièrement nettoyée" apparaît, puis si le log `[Réception] ... indisponible`
  apparaît et avec quel `State`/`IsFullyFurnished`.

## 2026-07-21 (suite 11) — Cause probable trouvée : chemin NavMesh partiel jamais complété

Confirmé par deux employés différents, bloqués dans les deux sens (hôtel↔resto) — pas spécifique à
un employé ni une direction, cohérent avec un problème de connectivité NavMesh entre les deux zones.

### MonsterMover.cs — `NavigateTo()` abandonnait silencieusement sur un chemin partiel
- `NavMeshPathStatus.PathPartial` (chemin trouvé mais qui n'atteint pas la cible — typiquement
  quand deux zones ne sont pas complètement reliées sur le NavMesh) était traité exactement comme
  `PathComplete` : le personnage suivait les coins du chemin jusqu'au dernier point atteignable,
  puis la coroutine se terminait — sans jamais avoir atteint la vraie cible, et sans aucun repli.
  Exactement le symptôme "démarre un mouvement puis reste sur place".
- **Fix** : si le chemin est `PathPartial`, une fois les coins suivis, complète maintenant le reste
  du trajet en ligne droite (`DirectMove`) — comme le repli déjà utilisé quand le chemin est
  entièrement invalide. Ce correctif est **défensif** (permet d'arriver quand même) mais ne résout
  pas la cause root : si le NavMesh entre le comptoir hôtel et le comptoir resto n'est vraiment pas
  connecté, ça vaut le coup de vérifier/rebaker le NavMesh (Window → AI → Navigation) pour un trajet
  propre plutôt qu'une marche en ligne droite qui peut traverser les murs visuellement.

## 2026-07-21 (suite 10) — Paiement du restaurant (feature manquante) + mouvement intermittent

### MonsterData.cs / EatingSpot.cs — Un repas servi ne rapportait jamais d'argent
- Vérifié : aucune logique de revenu n'existait nulle part pour le restaurant — seul le séjour en
  chambre (`revenuePerNight`) rapportait de l'argent. Un monstre pouvait manger gratuitement, client
  chambre ou visiteur.
- Nouveau champ `MonsterData.mealRevenue` (défaut 15 — **valeur de départ à ajuster toi-même**,
  cohérent avec `revenuePerNight` qui est déjà le tien à régler). **⚠️ Nouveau champ sur un
  ScriptableObject : si Unity affiche une erreur "script class layout incompatible" au build, ferme
  et rouvre l'Éditeur (même cause récurrente que pour `HotelConfig` plus tôt ce chantier).**
- `EatingSpot.PayForMeal()` appelée aux deux points de fin de repas réussie (`EatRoutine` — livraison
  avec plat, et `DelayedServiceComplete` — fallback sans plat) : verse `mealRevenue` du monstre
  servi, log `[Paiement]` cohérent avec le reste du jeu. S'applique aux clients chambre et aux
  visiteurs sans distinction (les deux payent leur repas séparément de la nuitée).

### Réceptionniste — Retour au comptoir hôtel pas systématique (signalé, pas encore expliqué)
- "Parfois" (pas systématique) le réceptionniste quitte le comptoir resto vers le comptoir hôtel
  sans y arriver — mêmes symptômes que le bug résolu précédemment (déplacement qui démarre puis ne
  progresse pas), mais cette fois intermittent plutôt que systématique dans un sens précis. Pas de
  nouvelle cause de code identifiée. Hypothèse la plus probable : problème de NavMesh/pathing entre
  les deux comptoirs (couloir étroit, connectivité partielle, ou encombrement temporaire par
  d'autres monstres/employés) plutôt qu'un bug de logique — à surveiller si ça devient fréquent.

## 2026-07-21 (suite 9) — Prefabs Zombie/Vampire/Werewolf cassés (régression de ma faute)

### Zombie.prefab / Vampire.prefab — "The referenced script ... is missing!"
- Cause : `NoSeatIndicator.cs` avait été figé en dur (composant réel sauvegardé sur le prefab, pas
  juste ajouté à l'exécution) sur `Zombie.prefab` (composant racine) et séparément sur
  `Vampire.prefab` (probablement via une action "Apply to Prefab" pendant un test antérieur).
  Sa suppression plus tôt ce chantier (remplacé par la réception restaurant) a cassé ces deux
  prefabs — `Werewolf.prefab` est une **variante** de `Zombie.prefab` et héritait donc du même
  problème sans avoir sa propre copie.
- **Fix** : retiré proprement le composant + sa référence dans `m_Component`/`m_AddedComponents`
  sur les deux prefabs concernés (édition directe du YAML, vérifié qu'aucune référence orpheline ne
  subsiste). Confirmé qu'aucun autre prefab du projet n'a `NoSeatIndicator` figé dessus.
- **Leçon retenue** : supprimer un script peut casser un prefab même s'il n'est censé être qu'ajouté
  dynamiquement à l'exécution — toujours vérifier `grep` sur les `.prefab`/`.unity` avant de
  supprimer un fichier `.cs`, pas seulement les références dans le code C#.

### Régressions de mouvement signalées en parallèle (pas encore expliquées)
- Client hôtel ne se dirige plus vers le comptoir, réceptionniste non plus, nettoyeur ne se dirige
  plus vers les déchets à nettoyer. Pas de lien direct trouvé avec le bug de script manquant
  ci-dessus (un avertissement "missing script" n'empêche pas Unity d'instancier normalement le
  reste du GameObject). **À revérifier après ce fix** — le spam de warnings a pu perturber autre
  chose, ou c'est un problème séparé à diagnostiquer proprement si ça persiste.

## 2026-07-21 (suite 8) — Nettoyage des logs de diagnostic

Retiré tous les `Debug.Log`/`LogWarning` ajoutés au fil de la traque de bugs de ce chantier
(`ReceptionEmployeeAI`, `ReservationSystem.CheckInNext()`, `ReceptionInteractor`, `ReceptionDesk`/
`RestaurantReceptionDesk` à l'enregistrement, `RestaurantReservationSystem.RegisterArrival()`) —
la console redevient lisible. Conservés uniquement les avertissements sur de vraies conditions
d'erreur, toujours utiles en jeu :
- `EatingSpot` — monstre disparu sans libérer sa place.
- `RestaurantReservationSystem.RegisterArrival()` — aucun comptoir resto trouvé / aucun slot
  disponible.
- `ReceptionEmployeeAI.CheckInRoutine()` — employé jamais arrivé au comptoir (bloqué en chemin).
- `MonsterNeedSeeker.NeedCheckLoop()` — besoin insatisfait sans facilité trouvée (pré-existant).

## 2026-07-21 (suite 7) — Validation "à distance" sans arrivée réelle (bug de conception confirmé)

Remonté : l'employé au comptoir hôtel amorce un mouvement vers le resto puis reste sur place — le
monstre finit quand même par être validé "tout seul". Deux problèmes distincts :
1. Le déplacement de l'employé vers le comptoir resto semble bloquer/échouer (cause pas encore
   identifiée — nécessite investigation séparée, potentiellement un souci de NavMesh/pathing autour
   du comptoir).
2. **Bug de conception confirmé et corrigé** : `CheckInRoutine()` appelait `CheckInNext()` après le
   timeout de 15s **même si l'employé n'était jamais arrivé** (le timeout servait juste à ne pas
   attendre indéfiniment, mais la validation se faisait quand même ensuite) — un employé bloqué en
   chemin validait donc le monstre "à distance" au bout de 15s, sans jamais avoir été physiquement
   au comptoir. D'où l'impression qu'il était "validé tout seul".

### ReceptionEmployeeAI.cs — Ne valide plus que si l'arrivée est confirmée
- `CheckInRoutine()` ne tente `CheckInNext()` que si l'employé a réellement atteint le comptoir
  (distance ≤ 1.5). Sinon : `Debug.LogWarning` explicite ("n'a jamais atteint le comptoir... bloqué
  en chemin ?") + abandon de cette tentative — le monstre reste en attente, réessayé plus tard.
- **Utile pour la suite** : ce nouveau warning va maintenant apparaître à chaque fois que le blocage
  de déplacement (point 1) se reproduit — permettra de confirmer si c'est systématique et de
  creuser la cause réelle (NavMesh, obstruction physique autour du comptoir hôtel, etc.).

## 2026-07-21 (suite 6) — Crash MissingReferenceException (explique "n'accepte plus personne")

### RestaurantReservationSystem.cs / ReservationSystem.cs — Référence morte non protégée
- Cause : un visiteur repas a son propre garde-fou de sécurité indépendant
  (`MonsterNeedSeeker.VisitTimeoutRoutine`, 90s) qui le détruit directement (`ExitHotel()` →
  `WalkToAndDestroy`) **sans passer par** `RestaurantReservationSystem` — s'il était encore
  enregistré dans `_pending` (en file, ou en attente de validation) à ce moment-là, la référence
  restait dans la liste après sa destruction. `NextServiceableUnclaimed` (ajouté au tour précédent)
  accédait à `guest.Monster.transform` sans vérifier `!= null` d'abord → `MissingReferenceException`
  ("fake null" Unity classique) à **chaque frame** dès que ce cas se présentait.
- Cette exception coupait `TryStartTask()` en plein milieu, avant d'atteindre la logique de
  démarrage de tâche — explique très probablement le "n'accepte plus personne même si des places
  sont dispo" (l'employé ne traitait plus jamais rien après le premier crash) et une partie de
  l'alternance qui semblait erratique.
- **Fix** : `Update()` des deux systèmes purge maintenant activement les entrées dont le monstre a
  été détruit (même pattern que l'ancien nettoyage de `_noSeatQueue`) — plus de référence morte qui
  traîne. Ajout de vérifications `!= null` défensives dans `NextUnchecked`/`NextUnclaimed`/
  `NextServiceableUnclaimed` des deux côtés, et dans `CheckInNext()`, en ceinture de sécurité.

## 2026-07-21 (suite 5) — Tables bloquées + réceptionniste ne s'engage plus sur du non-servable

### EatingSpot.cs — Place bloquée définitivement si le monstre disparaît en attendant (bug confirmé)
- Cause trouvée pour "les tables ne se libèrent jamais, mais les nouvelles fonctionnent" :
  `Update()` faisait `if (_state != WaitingForFood || _monster == null) return;` — si le monstre
  était détruit **pendant** qu'il attendait sa nourriture (départ forcé ailleurs dans le jeu, timeout
  hôtel qui le supprime pendant qu'il est assis, etc., sans repasser par ce composant), cette ligne
  retournait indéfiniment sans jamais appeler `Release()`. `_state` restait bloqué sur
  `WaitingForFood` pour toujours → `IsOccupied` (`_state != Empty || IsDirty`) restait vrai à vie,
  bloquant cette place définitivement — alors qu'une table neuve démarre forcément à `Empty`.
- **Fix** : détecte spécifiquement ce cas (`_state == WaitingForFood && _monster == null`) et
  appelle `Release()` immédiatement, avec un `LogWarning` pour tracer quand ça arrive.

### ReceptionEmployeeAI.cs / ReservationSystem.cs / RestaurantReservationSystem.cs — Ne plus s'engager sur un monstre non-servable
- Nouveau comportement demandé : si les monstres en attente à un comptoir ne peuvent pas être
  acceptés (pas de chambre compatible côté hôtel, pas de place libre côté resto), le réceptionniste
  ne doit pas s'y rendre inutilement — il doit traiter l'autre file à la place.
- Nouvelles propriétés `NextServiceableUnclaimed` (hôtel : filtre en plus sur `GetCompatibleRooms` ;
  resto : filtre en plus sur `FacilityRoomInstance.FindNearestFreeSpot`) — utilisées à la place de
  `NextUnclaimed` uniquement pour la **sélection de tâche** de l'employé (le joueur continue
  d'utiliser `NextUnchecked`, sans ce filtre, car il n'y a pas de coût à essayer).
- Corrige aussi probablement une partie du bug "le réceptionniste ne retourne pas au comptoir
  resto" : si un monstre hôtel sans chambre disponible restait sélectionné comme tâche, l'employé
  perdait un cycle complet à marcher/attendre pour rien avant de pouvoir retenter le resto.

## 2026-07-21 (suite 4) — Logs de cycle de vie complet pour ReceptionEmployeeAI

Log fourni par l'utilisateur montre une décision correcte (hôtel vide, resto non vide → devrait
appeler `StartRestaurantTask`) au moment où le réceptionniste cesse de retourner au resto — la
décision elle-même semble bonne, donc le problème est probablement plus loin dans la coroutine.

### ReceptionEmployeeAI.cs — Logs ajoutés sur tout le cycle de la tâche
- `StartHotelTask`/`StartRestaurantTask` : confirment que la tâche a bien démarré.
- `CheckInRoutine` : log au démarrage (comptoir trouvé ou non), à l'arrivée (ou timeout, avec
  distance restante), si `CheckInNext()` est sauté (employé plus en état Working), et à la toute
  fin juste avant `EndTask()`.
- **À tester** : reproduire la même situation (hôtel vide, resto avec quelqu'un en attente) et
  copier toute la séquence de logs `[Réception]` qui suit la ligne `TryStartTask`. Ça montrera
  précisément si `StartRestaurantTask` est appelée, si le comptoir resto est trouvé, si l'employé
  arrive ou timeout, et si `CheckInNext()` s'exécute.

## 2026-07-21 (suite 3) — Comptoir hôtel retrouvé, alternance réceptionniste à diagnostiquer

`ReceptionDesk` manquant confirmé et corrigé côté utilisateur. Nouveau souci : le réceptionniste
ne se rend plus au comptoir resto — reste bloqué sur l'hôtel, l'alternance ne se déclenche pas.

### ReceptionEmployeeAI.cs — Log de diagnostic dans TryStartTask()
- Relecture complète de la logique d'alternance (`_preferRestaurantNext`) sans trouver de bug
  évident — devrait basculer correctement même avec un flux hôtel continu (le champ reste `true`
  après une tâche hôtel tant qu'aucune tâche resto n'a pu démarrer, donc la prochaine décision
  vérifie le resto en premier).
- Log ajouté à chaque décision (uniquement si un monstre attend d'un côté ou de l'autre, pour ne
  pas spammer) : préférence actuelle, monstre hôtel non réclamé trouvé ou non, monstre resto non
  réclamé trouvé ou non. **À tester** : observer la console pendant que le réceptionniste travaille
  avec du monde des deux côtés — ça dira si `NextUnclaimed` échoue à trouver le monstre resto, ou
  si la logique de choix elle-même part en vrille.

## 2026-07-21 (suite 2) — Diagnostic définitif comptoir hôtel introuvable

Le joueur ET le réceptionniste échouent tous les deux à trouver `ReceptionDesk` (alors que les
deux trouvent bien `RestaurantReceptionDesk`) — signal fort que le composant n'est enregistré nulle
part actuellement, pas juste "hors de portée".

### ReceptionDesk.cs — Enregistrement passé de Awake() à OnEnable() + log de confirmation
- Si le GameObject portant `ReceptionDesk` était **inactif** au chargement de la scène, `Awake()`
  ne se déclenche jamais tant qu'il reste inactif — `ReceptionDesk.All` resterait vide
  silencieusement, sans aucune erreur. Passé à `OnEnable()`/`OnDisable()` (comme
  `FacilityRoomInstance` et les autres registres du projet) — s'enregistre aussi si l'objet est
  activé après coup.
- `Debug.Log` ajouté à l'enregistrement (nom, position, total de comptoirs actifs) — même log
  ajouté sur `RestaurantReceptionDesk` pour comparaison directe.
- **Test définitif** : relancer, chercher `[ReceptionDesk]` dans la console dès le lancement. Si
  cette ligne n'apparaît **jamais**, le composant n'est bien sur aucun objet actif de la scène — il
  faut l'ajouter (ou réactiver l'objet qui le porte). Si elle apparaît mais loin des coordonnées du
  vrai comptoir, c'est le mauvais objet qui le porte.

## 2026-07-21 (suite) — Rework complet du comportement réception (spec utilisateur)

Reprise complète suite à un récapitulatif clair de l'utilisateur, remplaçant les hypothèses/patchs
successifs précédents. Comportement cible :
- 2 comptoirs : hôtel (fixe) et restaurant (meuble posé par le joueur dans la cuisine).
- Un monstre peut arriver pour manger seul, réserver une chambre + manger, ou manger puis réserver.
- Validation (joueur ou réceptionniste) requise aux deux comptoirs selon le cas.
- Le réceptionniste **alterne** entre hôtel et resto (pas de priorité fixe) ; plusieurs
  réceptionnistes se répartissent le travail **sans doublon**.
- Abandon repas : visiteur sans chambre → quitte l'hôtel ; client chambre → satisfaction diminue
  mais garde sa réservation.

### ReservationSystem.cs / RestaurantReservationSystem.cs — Système de "réclamation" (anti-doublon)
- Nouveau champ `IsClaimed` sur `PendingGuest`/`PendingVisitor`. Nouvelle propriété `NextUnclaimed`
  (comme `NextUnchecked` mais ignore les monstres déjà réclamés par un réceptionniste) — utilisée
  uniquement pour la **sélection de tâche** par les employés, afin qu'avec plusieurs
  réceptionnistes, deux employés ne convergent jamais sur le même monstre.
- `CheckInNext()` accepte maintenant un paramètre optionnel (`specific`) — un réceptionniste qui a
  réclamé un monstre précis valide **ce monstre-là** spécifiquement (pas "le prochain disponible",
  qui pourrait avoir changé entre-temps). Le joueur (`ReceptionInteractor`/`RestaurantReceptionInteractor`)
  continue d'appeler `CheckInNext()` sans argument — comportement inchangé, il peut toujours
  accueillir un monstre même réclamé par un employé en chemin (`IsCheckedIn` re-vérifié pour éviter
  un double traitement).

### ReceptionEmployeeAI.cs — Réécrit : alternance + réclamation
- Remplace la priorité fixe "hôtel d'abord" par une **alternance** : après avoir traité une tâche
  hôtel, la tentative suivante regarde le restaurant en premier, et inversement — évite qu'une file
  resto reste indéfiniment derrière une file hôtel jamais vide.
- Réclame (`IsClaimed = true`) le monstre choisi *avant* de commencer à marcher vers le comptoir —
  `ReleaseReservations()` (appelée automatiquement par `EndTask()`, y compris en cas d'interruption
  — fin d'heures, démission) libère la réclamation si la tâche n'aboutit pas, pour qu'un autre
  réceptionniste (ou une tentative suivante) puisse reprendre ce monstre.

### MonsterNeedSeeker.cs — Conversion visiteur → client + abandon unifié avec pénalité
- `Activate()` (et `MonsterRoamBehavior.Activate()`) ignorent maintenant un second appel
  (`if (_active) return;`) — nécessaire car la conversion ci-dessous ré-appelle ces méthodes sur un
  monstre déjà actif en tant que visiteur, ce qui aurait démarré une 2e coroutine `NeedCheckLoop`/
  `RoamLoop` en parallèle sans cette garde.
- Nouveau `stayAfterMealChance` (0 par défaut = comportement d'origine inchangé) : après un repas
  réussi, un visiteur sans chambre peut désormais décider de rester et rejoindre la file de
  réception hôtel (`ReservationSystem.RegisterArrival`) au lieu de systématiquement repartir —
  couvre le 3e cas d'arrivée demandé ("manger puis réserver une chambre"). Le timeout de sécurité
  du visiteur (`VisitTimeoutRoutine`) est annulé lors de la conversion (sinon il aurait pu détruire
  le monstre après coup, alors qu'il vient de devenir un client légitime avec une chambre réservée).
  **Valeur laissée à 0 (désactivé) — à toi de choisir la chance si tu veux activer cette conversion.**
- Nouveau `GiveUpMeal(reason)` centralise l'abandon d'une tentative de repas (file resto trop
  pleine/lente, ou jamais servi à table) : client chambre → pénalité de satisfaction explicite
  (`giveUpMealPenalty`, 10 par défaut) + retour en chambre (garde sa réservation, peut réessayer) ;
  visiteur → `ExitDissatisfied()` (quitte l'hôtel, note `[Réputation]`). Remplace la logique
  dupliquée précédemment dans `RestaurantReservationSystem.RemoveVisitor()` et `EatingSpot`
  (timeout d'attente à table), qui ne pénalisait pas explicitement les clients chambre.

### Points non résolus (à vérifier après ce rework)
- **Check-in hôtel joueur toujours cassé** : log confirmé — `ReceptionInteractor` ne trouve aucun
  `ReceptionDesk` à portée (`desk=aucun`). Le comptoir physique de l'hôtel n'a probablement pas (ou
  plus) le composant `Reception Desk`, ou le joueur n'était pas à moins de 1.8 unité au moment du
  test. **Action requise** : vérifier ce composant sur le comptoir hôtel réel dans la Hierarchy.

## 2026-07-21 — Suite playtest réception restaurant

### RestaurantReservationSystem.cs / MonsterNeedSeeker.cs — Monstre bloqué si comptoir pas encore construit (bug confirmé)
- Cause des bugs "un monstre arrivé avant que la cuisine soit construite n'y va jamais même une
  fois construite" + "il faut valider le premier au comptoir pour débloquer les suivants" : c'était
  **le même bug**. `RegisterArrival()` mettait `HasArrived = true` sans déplacement ni retry quand
  aucun `RestaurantReceptionDesk` n'existait — le monstre restait figé pour toujours, son besoin
  marqué "en cours de traitement" (`_seekingNeed`) ne se relançant jamais. Les monstres qui
  commençaient à chercher *après* la construction du comptoir fonctionnaient normalement, donnant
  la fausse impression que "valider le premier débloque les autres" (coïncidence, pas causalité).
- **Fix** : `RegisterArrival()` retourne maintenant `bool` — `false` si aucun comptoir n'existe,
  sans rien enregistrer. `MonsterNeedSeeker.NeedCheckLoop()` ne verrouille `_seekingNeed` que si
  l'enregistrement réussit ; sinon il retente automatiquement au prochain cycle (`checkInterval`,
  5s par défaut) — un monstre affamé retentera donc tout seul une fois le comptoir construit.

### ReservationSystem.cs / ReceptionInteractor.cs — Logs de diagnostic (bug "check-in hôtel impossible" toujours en cours)
- Pas de bug identifié dans le code du flux joueur (`ReceptionInteractor` n'a pas été modifié ce
  chantier) — logs ajoutés pour voir exactement où ça bloque à la prochaine tentative :
  - `ReceptionInteractor` : si Interact est pressé sans effet, logue l'état du comptoir détecté,
    de `ReservationSystem`, et du monstre en attente.
  - `ReservationSystem.CheckInNext()` : logue explicitement si aucun monstre n'est en attente, ou
    si aucune chambre compatible n'est libre.
- **À tester** : s'approcher du comptoir hôtel avec un monstre en attente, appuyer sur Interact,
  regarder la console.

## 2026-07-20 (suite 3) — Bugs playtest réception restaurant

3 bugs remontés après config initiale : (1) l'embauche d'un réceptionniste validait instantanément
les monstres déjà en attente, avant même qu'il ait rejoint le comptoir ; (2) les monstres affamés
se dirigeaient vers la réception de l'hôtel au lieu du restaurant ; (3) la validation devait se
faire au comptoir resto alors que le monstre attendait visuellement à la réception hôtel.

### ReceptionEmployeeAI.cs — Check-in validé avant l'arrivée physique (bug 1, confirmé)
- `CheckInRoutine()` lançait `_mover.MoveTo(desk)` puis attendait un **délai fixe**
  (`_checkInBaseDelay`, non lié à la distance réelle) avant d'appeler `CheckInNext()`. Pour un
  employé fraîchement embauché (donc loin du comptoir), ce délai s'écoulait bien avant qu'il ait
  fini de marcher — le check-in se déclenchait alors que l'employé était encore en chemin, voire
  à peine sorti du point de spawn.
- **Fix** : attend maintenant l'arrivée physique réelle (distance < 1.5, timeout de sécurité 15s)
  avant d'entamer le délai administratif — même pattern que `CookEmployeeAI`/`CleaningEmployeeAI`.

### ReceptionQueueManager.cs — `queueStart` non assigné retombait sur l'origine du monde (bug 2/3, hypothèse la plus probable)
- `SlotPosition()` retournait silencieusement `Vector3.zero` si `queueStart` n'était pas assigné —
  si l'hôtel est construit près de l'origine de la scène (fréquent), un comptoir resto dont le
  `queueStart` n'a pas été configuré envoie donc ses monstres près de l'origine, qui peut coïncider
  visuellement avec la réception de l'hôtel. Expliquerait les bugs 2 et 3 : le monstre marche vers
  (0,0,0) plutôt que vers le vrai comptoir resto, alors que la validation (qui utilise la position
  réelle du comptoir, pas `SlotPosition`) fonctionne correctement une fois qu'on s'y rend.
- **Fix** : repli sur `transform.position` du comptoir lui-même (au lieu de l'origine du monde) +
  `Debug.LogWarning` explicite si `queueStart` n'est pas assigné — **vérifier que le `queueStart`
  du comptoir resto est bien assigné dans l'Inspector**, ce log confirmera si c'était la cause.

### RestaurantReservationSystem.cs — Logs de traçabilité ajoutés
- `RegisterArrival()` logue maintenant explicitement : aucun comptoir trouvé, slot obtenu (avec la
  position exacte visée), ou file pleine — permet de confirmer en jeu quel cas se produit si le
  bug persiste après le fix `queueStart` ci-dessus.

**⚠️ À vérifier en priorité** : le champ `Queue Start` du `Reception Queue Manager` sur le meuble
comptoir restaurant est-il bien assigné à un enfant du prefab ? Si le nouveau `Debug.LogWarning`
apparaît dans la console au moment où un monstre cherche à manger, c'est confirmé.

---

## 2026-07-20 (suite 2) — Réception du restaurant

Bug remonté : au-delà de 2 monstres en attente devant la cuisine, le 3e restait figé au point de
spawn (`FacilityRoomInstance.HasRoom` capé par `capacity`, silencieusement — `MonsterNeedSeeker`
retrouvait `facility == null` et ne faisait rien). Deuxième bug lié : un monstre qui abandonnait
faute d'être servi revenait immédiatement dans la file du restaurant en boucle au lieu de partir.
Cause racine commune : pas de "réception" pour le restaurant — contrairement à l'hôtel, rien ne
validait de façon synchrone qu'une place existait avant d'engager un monstre dans l'attente.

**Décision (validée par l'utilisateur)** : répliquer le pattern de la réception hôtel pour le
restaurant (comptoir + file à slots + validation par un employé), le même réceptionniste gère les
deux, priorité hôtel. Un client chambre qui abandonne au resto retourne en chambre (garde sa
réservation) ; un visiteur sans chambre qui abandonne quitte l'hôtel avec une note d'insatisfaction.

### ReceptionQueueManager.cs — Singleton retiré
- `Instance` supprimé : chaque comptoir (hôtel, restaurant) a maintenant sa propre instance,
  référencée directement par le système qui la possède, plutôt qu'un singleton global qui aurait
  empêché toute seconde instance de coexister (`Destroy(gameObject)` sur la 2e au démarrage).

### ReservationSystem.cs — Référence directe au lieu du singleton
- Nouveau champ `receptionQueue` (`ReceptionQueueManager`) — remplace les 7 usages de
  `ReceptionQueueManager.Instance` dans ce fichier.
- **⚠️ ACTION REQUISE** : assigner dans l'Inspector le `ReceptionQueueManager` existant de l'hôtel
  (celui qui utilisait le singleton) à ce nouveau champ `ReservationSystem.receptionQueue` — sinon
  la réception de l'hôtel cesse de fonctionner (aucun fallback silencieux, c'est intentionnel pour
  ne pas masquer l'oubli).

### RestaurantReceptionDesk.cs *(nouveau fichier)* — Comptoir posable comme un meuble
- Sur demande de l'utilisateur ("faut que ça soit un meuble que l'on peut poser dans la cuisine") :
  ce comptoir n'est **pas** un GameObject à câbler manuellement en scène, mais un composant destiné
  au **prefab d'un meuble** (`FurnitureData.prefab`), posable dans la cuisine via `FurniturePlacer`
  exactement comme le frigo ou le plan de travail. Vérifié dans `FurniturePlacer.TryPlace()` : seuls
  les `Collider` du visuel sont désactivés à la pose, les autres scripts (dont ce composant) restent
  actifs normalement.
- Porte son propre `ReceptionQueueManager` (sibling component sur le même prefab, auto-récupéré au
  `Awake()` si non assigné) — chaque comptoir posé a donc sa file indépendante, avec son propre
  `queueStart` (enfant du prefab) pour orienter la file.
- `RestaurantReservationSystem` ne référence plus aucun `ReceptionQueueManager` fixe : il retrouve
  dynamiquement le comptoir actuellement posé via `RestaurantReceptionDesk.All` (`ActiveDesk()`).
  L'abonnement à `queueManager.OnSlotIndexChanged` se fait côté comptoir (`Start()`, pas `Awake()`,
  pour garantir que `RestaurantReservationSystem.Instance` est déjà initialisé) et se désabonne à
  `OnDestroy()` — robuste si le meuble est posé en cours de partie, déplacé ou retiré.

### RestaurantReservationSystem.cs *(nouveau fichier)*
- Même principe que `ReservationSystem` mais pour le restaurant : arrivée → file physique
  (`ReceptionQueueManager` dédié) → `CheckInNext()` n'accepte que si une place (`EatingSpot`) est
  disponible **tout de suite** (vérification synchrone, comme `TryAssignRoom`) — élimine tout état
  "en attente mais sans place" intermédiaire.
- Timeout d'attente en file (`maxWaitTime`, défaut 60s) : au-delà, le monstre abandonne —
  `RemoveVisitor()` branche selon `GuestRoomReference` : client chambre → `OnServiceComplete()`
  (retour en chambre, garde la réservation) ; visiteur → `MonsterNeedSeeker.ExitDissatisfied()`
  (quitte l'hôtel, log `[Réputation]`).
- `DebugForceLeave(monster)` (retourne bool) — utilisé par `MonsterDebugTools`.

### FacilityRoomInstance.cs — Toute la file d'attente ad-hoc retirée
- Supprimé : `_noSeatQueue`, `NoSeatEntry`, `RepositionQueue()`, `ComputeQueuePath()`,
  `PositionAlongPath()`, `QueueOrigin`/`QueueDirection`, `noSeatQueueStart`,
  `noSeatSlotSpacing`/`noSeatSlotNavSearchRadius`/`noSeatQueueRevalidateInterval`,
  `noSeatSatisfactionDecay`, les gizmos associés, et la revalidation périodique — tout ce système
  (slots, suivi de chemin NavMesh, revalidation) construit dans ce chantier devient obsolète
  puisqu'un monstre n'atteint plus jamais cette facilité sans place déjà garantie.
- `RequestService()` : la branche `playerServiceOnly` devient un garde-fou (log un avertissement,
  ne fait rien) — le vrai point d'entrée est désormais `RestaurantReservationSystem.RegisterArrival()`.
- `FindNearestFreeSpot()` rendu `public static` (utilisé par `RestaurantReservationSystem`).
- `HasRoom` simplifié : `true` pour `playerServiceOnly` (capacité gérée par la file de réception,
  plus par cette facilité), inchangé pour le mode auto.

### NoSeatIndicator.cs *(fichier supprimé)*
- Plus aucun appelant (créé/affiché uniquement par l'ancienne file `_noSeatQueue`) — supprimé plutôt
  que laissé orphelin, même logique que le nettoyage de `ReservationSystem.ProcessQueue()` (B7).

### MonsterNeedSeeker.cs
- `NeedCheckLoop()` : pour une facilité `playerServiceOnly`, appelle désormais
  `RestaurantReservationSystem.Instance.RegisterArrival()` au lieu de `facility.RequestService()`
  directement — les facilités "auto" (non playerServiceOnly) gardent le flux inchangé.
- Nouveau `ExitDissatisfied(reason)` : quitte l'hôtel + log `[Réputation]` — utilisé pour les
  visiteurs qui abandonnent (file resto ou attente à table), jamais pour les clients chambre.

### EatingSpot.cs — Timeout d'attente à table : la même branche client/visiteur
- Le timeout `waitGaugeMaxTime` (déjà existant) branche maintenant explicitement : client chambre
  → `OnServiceComplete()` (retour en chambre) ; visiteur → `ExitDissatisfied()` (note d'insatisfaction).
  Avant, les deux cas passaient par `OnServiceComplete()` sans distinction ni note.

### ReceptionEmployeeAI.cs — Gère les deux réceptions
- `TryStartTask()` vérifie d'abord `ReservationSystem.NextUnchecked` (hôtel), puis seulement
  `RestaurantReservationSystem.NextUnchecked` (resto) si la file hôtel est vide — priorité hôtel.
- `CheckInRoutine(bool isRestaurant)` — se déplace vers le bon comptoir (`ReceptionDesk` ou
  `RestaurantReceptionDesk`) et appelle le bon `CheckInNext()` selon le cas.

### RestaurantReceptionInteractor.cs *(nouveau fichier, Player)*
- Équivalent de `ReceptionInteractor` (hôtel) mais pour le restaurant — permet au **joueur**
  d'accueillir manuellement les clients du restaurant tant qu'aucun réceptionniste n'est embauché.
  Sans ce composant, la file resto ne serait jamais validée en l'absence d'employé (`CheckInNext()`
  ne serait appelé par personne).

### MonsterDebugTools.cs
- `ForceLeave()` vérifie d'abord la file du restaurant (`RestaurantReservationSystem.DebugForceLeave`,
  libère son slot proprement) avant de retomber sur la logique hôtel existante.

---

## ⚠️ ACTIONS REQUISES DANS L'ÉDITEUR UNITY (bloquant — le code compile mais rien ne fonctionne sans ça)

1. **`ReservationSystem.receptionQueue`** : assigner le `ReceptionQueueManager` existant de l'hôtel
   dans l'Inspector (sinon la réception de l'hôtel s'arrête complètement).
2. **`RestaurantReservationSystem`** : ajouter ce composant sur un GameObject dans `_Managers`
   (comme `ReservationSystem`) — plus aucun champ à assigner dessus, il retrouve le comptoir
   automatiquement une fois posé (étape 3).
3. **Créer le meuble "Comptoir restaurant"** (contenu, pas de câblage scène) :
   - Un prefab avec, sur le GameObject racine : `RestaurantReceptionDesk` + `ReceptionQueueManager`
     (assigner son `queueStart` à un enfant du prefab, orienté vers où la file doit s'étirer,
     régler `maxSlots`/`slotSpacing`).
   - Un `FurnitureData` (clic droit → Create → MonsterHotel → Furniture Data) référençant ce prefab,
     avec un coût, ajouté à `optionalFurniture` (ou `requiredFurniture` si tu veux le rendre
     obligatoire pour que la cuisine soit "fonctionnelle") du `RoomData` de la cuisine.
   - Ensuite : se pose comme n'importe quel meuble via le panneau de gestion de chambre.
4. **Prefab(s) joueur** : ajouter `RestaurantReceptionInteractor` à côté du `ReceptionInteractor`
   existant, pour pouvoir accueillir manuellement sans réceptionniste embauché.

## 2026-07-20 (suite)

### CleaningEmployeeAI.cs — Débris sur les tables jamais vraiment "atteints" (B8 résolu)
- Cause trouvée pour le bug B8 (nettoyeur qui semblait faire l'action sans se déplacer) : les débris de repas apparaissent à la hauteur de la table (`EatingSpot.SpawnMealDebris()`, `surface.position`), mais `MonsterMover` verrouille toujours le Y du nettoyeur à la hauteur du sol — il ne "monte" jamais visuellement. Le test d'arrivée comparait la distance 3D complète, qui incluait donc en permanence l'écart de hauteur avec la table, ne pouvant jamais descendre sous le seuil (1.2). Résultat : timeout de 10s systématique, ramassage exécuté quand même via le fallback (d'où l'impression qu'il "nettoie sans se déplacer").
- Débris au sol (posés par un monstre en balade) n'étaient pas affectés — seuls ceux sur une table (hauteur non nulle) déclenchaient ce comportement.
- **Fix** : nouveau helper `HorizontalDistance()` (ignore Y) utilisé dans `CleanRoutine()` et `PickupDebrisRoutine()` à la place de `Vector3.Distance()` — cohérent avec ce que le déplacement peut réellement atteindre (jamais la hauteur).

## 2026-07-20

### FacilityRoomInstance.cs — File d'attente devant la cuisine (slots, comme la réception)
- Bug remonté en playtest : les monstres sans siège libre (mode `playerServiceOnly`) marchaient tous vers le **même** point fixe devant la facilité (`transform.position - forward * ...`), donc s'empilaient les uns dans les autres. `ReceptionQueueManager` avait déjà résolu ce problème pour la réception via des slots indexés — pattern repris ici.
- Nouveau `noSeatSlotSpacing` (défaut 1.2) + `NoSeatSlotPosition(index)` : chaque monstre en attente reçoit une position propre, alignée derrière la facilité (`basePos - forward * (index * spacing)`).
- `RepositionQueue()` appelée à l'ajout d'un nouveau monstre dans `_noSeatQueue` **et** à chaque fois qu'un monstre quitte la file (siège trouvé / détruit) — les suivants avancent d'un cran, comme la compaction de `ReceptionQueueManager`.
- ~~Corrige aussi probablement le badge "centré au milieu des monstres"~~ — **infirmé**, le badge restait centré sur le monstre même après ce fix. Voir correction dédiée ci-dessous.

### MealVisitorBadge.cs — Bulle "Repas" toujours centrée sur le monstre (suite du bug ci-dessus)
- Cause réelle, distincte du chevauchement de file : `offset` (1.8 en hauteur) ne dépasse pas la tête sur ces modèles — `NoSeatIndicator` (autre badge, icône "!") utilisait déjà 4.0 pour la même raison. `offset` remonté à **2.6** (au-dessus de la tête, sous le badge NoSeatIndicator à 4.0 pour que les deux ne se superposent pas quand un visiteur repas attend un siège).

### NoSeatIndicator.cs — Jauge d'attente pendant la file devant la cuisine
- Ajout d'une barre de remplissage (vert → rouge) sous l'icône "!", sur le même principe que la barre de patience de `GuestBubble` en réception — reflète `SatisfactionComponent.Normalized` du monstre pendant qu'il patiente sans siège (la satisfaction décroit déjà via `noSeatSatisfactionDecay`, seul l'affichage manquait).
- S'applique à **tout** monstre dans la file (visiteur repas ou client chambre en manque), puisque `NoSeatIndicator` est déjà affiché pour tous les cas d'attente — pas de composant dédié supplémentaire.

### FacilityRoomInstance.cs — La file d'attente se réajuste si l'environnement change (revalidation périodique)
- Nouvelle revalidation périodique (`noSeatQueueRevalidateInterval`, défaut 2s) : tant que la file n'est pas vide, `RepositionQueue()` est réappelée régulièrement même sans ajout/retrait, pour que les monstres déjà en attente se repositionnent si l'environnement change après qu'ils se soient arrêtés (le mover désactive son `NavMeshAgent` une fois arrivé — sans cette revalidation ils resteraient figés). `RepositionQueue()` ignore les monstres déjà à moins de 0.2 unité de leur slot pour ne pas relancer une coroutine de déplacement inutilement à chaque tick.

### FacilityRoomInstance.cs — La file suit maintenant le chemin NavMesh réel (pas une ligne droite)
- ~~Recalage NavMesh à petit rayon (2) sur une ligne droite figée~~ puis ~~Transform `noSeatQueueStart` fixe assigné à la main~~ — **remplacés**. L'utilisateur a fait remarquer que la construction est dynamique : une pièce peut être placée n'importe où, n'importe quand, y compris juste devant la cuisine — une ligne droite (même recalée localement) ne peut pas s'adapter à un vrai changement de géométrie (couloir qui tourne, nouvelle pièce). Confirmé par capture d'écran : un visiteur restait figé au point de spawn car sa position de file théorique n'était pas sur un chemin réellement praticable.
- **Fix définitif** : `ComputeQueuePath()` calcule un vrai `NavMesh.CalculatePath()` depuis l'origine de la file vers un point lointain dans sa direction générale ; `RepositionQueue()`/`PositionAlongPath()` placent chaque monstre à une distance cumulée le long des **coins réels de ce chemin**, pas d'une ligne droite. Recalculé à chaque appel de `RepositionQueue()` (ajout/retrait + revalidation périodique ci-dessus) — donc si le joueur construit une pièce entre-temps, la file se réajuste automatiquement au prochain cycle (jusqu'à 2s de latence), sans aucune action manuelle.
- `noSeatQueueStart` (Transform optionnel) reste disponible — c'est le "poste" d'accueil du restaurant que le joueur peut placer/orienter à la main (même rôle que `ReceptionQueueManager.queueStart`), mais il ne sert plus que de point de départ + direction générale pour le calcul de chemin, plus une ligne rigide.
- Gizmos mis à jour (`OnDrawGizmosSelected`) : ligne cyan = chemin réel calculé, sphères jaunes = slots — visible en sélectionnant la cuisine dans l'éditeur, utile pour vérifier que le chemin calculé correspond bien au couloir voulu.
- **Limite connue inchangée** : seul le placement de **pièces** rebake le NavMesh dans ce projet (`RoomPlacer.RebakeNavMeshAsync`) — un meuble seul posé sur le chemin ne carve pas le NavMesh (`FurniturePlacer` n'y touche pas, cf. `T3`) et ne sera donc pas contourné par `ComputeQueuePath()`.

## 2026-07-16

### G6 Phase 2 — Marqueur visuel visiteur repas
- **MealVisitorBadge.cs** *(nouveau fichier)* : bulle world-space orange ("Repas") au-dessus d'un visiteur repas, construite dynamiquement (même pattern que `GuestBubble` mais autonome — pas de dépendance à `ReservationSystem.PendingGuest`). Se détruit automatiquement avec le monstre.
- Câblé dans `SpawnScheduler.TrySpawn()` — ajouté uniquement sur les monstres déterminés comme visiteurs repas (Phase 1), absent sur les clients chambre normaux (qui gardent `GuestBubble` tant qu'ils sont en file).

### G6 Phase 1 — Visiteurs repas sans chambre
Objectif : débloquer la file d'attente réception quand toutes les chambres sont prises, en donnant aux monstres une alternative "juste venir manger" sans réserver de chambre.

- **HotelConfig.cs** : `mealVisitorChance` (0-1, défaut 0.6) et `mealVisitorMaxDuration` (défaut 90s, garde-fou anti-blocage).
- **MonsterNeedsComponent.cs** : `SetNeedLevel(NeedType, float)` — force le niveau d'un besoin (utilisé pour faire arriver un visiteur déjà affamé).
- **MonsterNeedSeeker.cs** : `ActivateAsVisitor(maxDuration)` — mode "visiteur sans chambre" : après service, `OnServiceComplete()` fait sortir le monstre de l'hôtel (`ExitHotel()` → `WalkToAndDestroy`) au lieu de tenter un retour en chambre inexistante. Timeout de sécurité (`VisitTimeoutRoutine`) qui force la sortie si jamais servi.
- **SpawnScheduler.cs** : à chaque spawn, tirage aléatoire (si le monstre a au moins un besoin) — visiteur repas : besoins mis à 0.15 (déclenche une recherche de facilité dès la 1ère vérification), `ActivateAsVisitor()`, **ne rejoint pas `ReservationSystem`** (pas de file de réception, pas de check-in). Sinon : flux normal inchangé (file d'attente → chambre).
- Réutilise tel quel le système existant `FacilityRoomInstance`/`MonsterNeedSeeker` (recherche de facilité, attente devant la cuisine si occupée via `NoSeatIndicator`) — aucune nouvelle IA de déplacement nécessaire.
- **Limite connue** : `SpawnScheduler.maxPending` (cap par monstre) ne compte que les monstres dans `ReservationSystem._pending` — les visiteurs n'y entrant jamais, ils ne sont pas soumis à ce plafond, seulement à leur intervalle de spawn. Pas de cap dédié pour l'instant (à revoir si besoin en phase ultérieure).
- **Phases 2-5 non implémentées** (identification visuelle, heures précises, conversion en client chambre, enchaînement d'activités + système de plats à score) — voir TODO G6.

### ReservationSystem.cs — Revert : la durée de séjour reste liée à dayDuration
- Sur clarification de l'utilisateur : le découplage `nightStayDuration` (introduit le 2026-07-15 pour permettre plusieurs séjours par chambre et par jour) ne correspond **pas** au comportement voulu. Un monstre doit rester `nights × dayDuration` — 1 nuit = 1 journée complète, comme à l'origine. Chaque chambre ne loge donc qu'un monstre à la fois par jour, ce qui est la pression volontaire à construire plusieurs chambres.
- `ReservationSystem.TryAssignRoom()` recalcule `actualDuration` via `TimeManager.dayDuration` à nouveau.
- `HotelConfig.nightStayDuration` **laissé déclaré mais inutilisé** (pas supprimé) pour éviter de redéclencher l'erreur de build "script class layout incompatible" rencontrée en l'ajoutant (désync de sérialisation Editor/Player nécessitant un restart Unity). À nettoyer plus tard lors d'une passe de compilation propre si besoin.

### RoomInstance.cs / RoomManagementPanel.cs — Feedback "pas assez de place" pour l'amélioration de chambre
- `RoomInstance.UpgradeRoom()` : nouvelle surcharge `UpgradeRoom(out string failReason)` qui distingue les causes d'échec (pas d'amélioration dispo, non meublée, pas assez de place, or insuffisant). L'ancienne signature `UpgradeRoom()` reste disponible (délègue à la nouvelle).
- `RoomManagementPanel.OnUpgradeRoomClicked()` affiche désormais la raison de l'échec pendant 2s dans `roomStateText` (texte rouge) avant de revenir à l'affichage normal.

### ReservationSystem.cs — Logs complets sur les paiements des monstres
- Check-in (`TryAssignRoom`) : log du revenu de séjour (`nights × revenuePerNight`) + solde après paiement.
- Check-out normal (`CheckoutNow`) : log du pourboire (déjà existant, reformaté avec le solde après paiement).
- Départ anticipé par insatisfaction (`CheckoutEarly`) : log explicite "pas de pourboire" (auparavant aucune trace du montant, même nul).
- Abandon de la file par timeout (`RemoveGuest`, angry) : nouveau log "aucun revenu généré" (auparavant complètement silencieux).
- Tous préfixés `[Paiement]` pour un filtrage facile dans la console.

### EmployeeTaskAI.cs *(nouveau fichier)* — Refacto R1 : base commune aux 3 IA employé
- Nouvelle classe abstraite `EmployeeTaskAI` factorisant le squelette partagé par `CookEmployeeAI`, `CleaningEmployeeAI`, `ReceptionEmployeeAI` : cycle Working/GoingToWork, flag `_busy`, `EndTask()` (reset busy + BlockBreak + réservations via `ReleaseReservations()` overridable), et `RatingSpeedMultiplier()` (le calcul note→vitesse dupliqué 3x, maintenant centralisé).
- Sous-classes réduites à `GoToPost()` (déplacement initial vers le poste, vide pour le nettoyeur qui n'en a pas) + `TryStartTask()` (recherche/lancement de tâche) + `ReleaseReservations()` (si applicable).
- **Bug latent corrigé au passage** : `ReceptionEmployeeAI` faisait déjà `StopAllCoroutines()` en cas d'interruption externe (démission, fin d'heures de travail) mais `CookEmployeeAI`/`CleaningEmployeeAI` ne le faisaient pas — leur coroutine en cours continuait de tourner en arrière-plan pendant qu'une nouvelle tâche pouvait démarrer en parallèle dès que `_busy` repassait à `false`. Le comportement est maintenant uniforme dans la classe de base.
- Aucun changement de comportement observable côté gameplay — refacto pur + fix du bug latent ci-dessus.

## 2026-07-15

### SpawnScheduler.cs / ShopCounter.cs — HotelConfig comme source unique pour monsters/rooms (B5)
- `SpawnScheduler.monsterPool` et `ShopCounter.catalog` sont désormais écrasés par `HotelConfig.monsters`/`HotelConfig.rooms` au démarrage, si ces tableaux sont non-vides — évite la divergence entre catalogue central et listes locales dupliquées par scène.
- **`needTypes` et `blocks` volontairement non câblés** : `needTypes` n'a pas de catalogue local équivalent à remplacer (chaque `MonsterData` a ses propres besoins) ; `blocks` est consommé de façon ordonnée par anneau (`ExpansionBlockSpawner.ringBlocks[]`), une liste non-ordonnée casserait la progression de coût Terre/Roche.
- **⚠️ Action requise** : `HotelConfig.rooms` ne contient qu'1 seule chambre dans l'asset actuel — à compléter avec toutes les chambres avant relance, sinon le magasin perd des options par rapport à avant ce changement.
- `blockHeight` laissé tel quel (non câblé), sur demande explicite.

### SpawnScheduler.cs — Fix Vampire/Loup-Garou qui ne spawnaient jamais (B1)
- **Cause** : la fenêtre d'ouverture générale (`spawnOpenHour`-`spawnCloseHour`) était appliquée à TOUS les monstres, y compris ceux avec `preferredSpawnTime = NightOnly` — mais cette fenêtre ne chevauche jamais la plage horaire "nuit" (`TimeManager.CurrentSpawnTime`), rendant la condition impossible à satisfaire.
- Fix : la fenêtre d'ouverture générale ne gate plus que les monstres `SpawnTime.Any`. Les monstres avec une préférence jour/nuit explicite s'appuient uniquement sur celle-ci.

### ReservationSystem.cs / RoomInstance.cs — Suppression code mort `ProcessQueue()` (B7)
- `ProcessQueue()` ne faisait plus jamais rien depuis le redesign du check-in (aucun monstre ne peut rester `IsCheckedIn` sans chambre). Méthode + les 2 appels (`RoomInstance.AddFurniture()`, `SetState()`) supprimés.

### CookEmployeeAI.cs / EatingSpot.cs — Clarification B2 (pas un bug)
- `HotelConfig.recipes` vide fait retomber `CompleteDelivery()` sur la branche fallback (remplissage direct du besoin le plus urgent) — le service se termine correctement. Le matching recette/ingrédient/monstre est inactif faute de données, mais le système fonctionne. À traiter comme un remplissage de contenu (étape équilibrage), pas un fix de code.

## 2026-07-10

### Passe d'équilibrage GD (économie, séjours, installations, blocs)
Validé par sessions de discussion GD successives — voir raisonnement complet dans l'historique de conversation. Résumé des changements appliqués :

**HotelConfig.cs**
- `startingGold` : 1000 → **350** — force des choix dès le départ (1 chambre = 150G, laisse peu de marge avant la 1ère embauche).
- `employeeSalaryPerRating` : 8 → **6** — allège le salaire de la 1ère embauche (rating 10 : 80G/j → 60G/j, frais d'embauche 120G → 90G).
- Ajout de `nightStayDuration = 60f` — durée réelle (secondes) d'**une nuit** de séjour, découplée de `dayDuration` (300s). Sans ça, 1 nuit = 1 jour entier, plafonnant le débit à 1 monstre par chambre par jour.

**ReservationSystem.cs**
- `TryAssignRoom()` : `EconomyManager.Earn(revenuePerNight)` → `Earn(revenuePerNight * nights)` — le revenu ignorait totalement la durée du séjour, un séjour de 3 nuits rapportait pareil qu'1 nuit.
- `actualDuration` utilise désormais `HotelConfig.nightStayDuration` au lieu de `TimeManager.dayDuration`.

**VampireData.asset / WerewolfData.asset**
- Durées de séjour explicites (remplacent le champ YAML fantôme `stayDuration`, silencieusement ignoré par le schéma actuel) : Vampire **2-3 nuits** (haute valeur, immobilise la chambre plus longtemps — vraie tension volume vs qualité face au Zombie), Loup-Garou **1-2 nuits**.

**RoomType_Werewolf.asset** *(nouveau)*
- Nouveau type de chambre dédié (au lieu de partager `RoomType_Standard` avec le Zombie). `WerewolfData.compatibleRoomTypes` et les 2 chambres Cave (`RD_ChambreCave_Niv1`/`Niv2`) mis à jour en conséquence.
- Corrige au passage un bug de données : `RD_ChambreCave_Niv1.roomType` était un entier brut (`2`, reliquat de l'ancien enum) au lieu d'une référence d'objet — les loups-garous ne pouvaient jamais être logés.

**Mobilier optionnel** (`FD_Commode`, `Furniture_Trash`)
- `revenueBonus`/`attractivenessBonus` : 0/0 → **5/2** — tout le mobilier optionnel du jeu avait un bonus nul, aucune raison rationnelle de l'acheter.

**KitchenFacility.asset**
- Coût de la salle : 100G → **50G** (coût total avec meubles obligatoires : 300G → 250G).

**Terre.asset / Roche.asset (blocs destructibles)**
- Terre (anneau proche) : 50G/1s → **40G/0,5s**.
- Roche (anneau lointain) : 50G/1s → **80G/1,5s**.
- Avant ce changement, les deux types de blocs avaient un coût et une durée identiques malgré un système conçu pour une progression par anneau.

### PlayerInputBinder.cs — Fix vol de manette en 2 joueurs
- **Cause** : `TrySoloDeviceSwitch()` lit `Keyboard.current` (état global, pas par joueur) pour détecter une intention de switch clavier↔manette. `kb.wKey`/`kb.aKey`/`kb.sKey`/`kb.dKey` désignent des positions physiques — exactement les touches Z/Q/S/D en AZERTY. Cette méthode tourne sur CHAQUE `PlayerInputBinder`, y compris celui de P2 à la manette : quand P1 (clavier) appuie sur ZQSD, le binder de P2 le détecte et bascule P2 vers le clavier, libérant sa manette — que P1 récupère aussitôt via sa propre logique de switch.
- Fix : `TrySoloDeviceSwitch()` retourne immédiatement si `PlayerInput.all.Count > 1` — cette logique n'a de sens qu'en solo.
- **LocalJoinManager.cs / LocalJoinOnHold.cs** : ajout de `pi.neverAutoSwitchControlSchemes = true` sur tous les chemins de spawn de P2 (manette, clavier, cheat C+P) — P1 l'avait déjà via `AutoSpawnP1`, P2 non, laissant Unity potentiellement ré-assigner ses devices automatiquement.

### RoomShopPanel.cs — Fix affichage "RoomType_Standard (RoomTypeData)" dans le nom de chambre
- **Cause** : `RoomData.roomType` est une référence `RoomTypeData` (ScriptableObject), pas une string. `BuildRoomSlots()` faisait `{data.roomType}` dans l'interpolation, appelant `ToString()` sur l'objet Unity — format par défaut `"NomAsset (NomType)"`.
- Fix : utilise `data.roomType.typeName` (le champ display prévu à cet effet sur `RoomTypeData`), avec garde `null`.

### RoomShopPanel.cs — Cancel manette (tentative layout vertical revertée)
- Ajout du support Cancel manette (`cancelActionName`) : `Update()` ferme le panneau (`Hide()`) sur `WasPressedThisFrame()`, même pattern que `FridgeUI`. Conservé.
- **Reverté** : la tentative de forcer un `VerticalLayoutGroup` par code (`EnsureVerticalLayout()`) + largeur fixe de panneau (`panelWidth`) cassait le menu (signalé par l'utilisateur — "le menu est tout cassé, pire qu'avant"). Cause exacte non investiguée avant le revert ; probablement un conflit avec la config Inspector existante (destruction de composants au runtime + `SetSizeWithCurrentAnchors` sur des ancres pas forcément adaptées). Le layout horizontal d'origine (configuré dans l'Inspector sur `content`) est donc toujours en place — le problème de débordement pour P2 reste **non résolu**, à reprendre différemment (voir T2c dans TODO.md).

### MonsterDebugTools.cs *(nouveau fichier)* + ReservationSystem.cs — Debug : forcer le départ d'un monstre
- Ajout de `ReservationSystem.DebugForceLeave(GameObject monster)` — gère les 3 cas possibles : monstre encore en file d'attente (retiré de `_pending`, sort en colère), monstre occupant une chambre (`CheckoutEarly`), ou monstre ailleurs dans l'hôtel (repas, balade — marche direct vers la sortie).
- `MonsterDebugTools` : composant ajouté automatiquement à chaque monstre par `SpawnScheduler.TrySpawn()`. En Play Mode, clic droit sur le composant dans l'Inspector → **"Debug : Forcer le départ"**.
- Confirmation au passage : le verrouillage anti-doublon cuisinier (`EatingSpot.IsReservedForService`) est déjà solide — la réservation est synchrone dans `Update()` avant le `StartCoroutine`, donc deux cuisiniers ne peuvent jamais se voir attribuer le même monstre, même en cas d'exécution la même frame.

### CookEmployeeAI.cs — Fix cuisiniers qui semblent alterner au lieu de travailler en parallèle
- **Cause** : avec un seul `WorkbenchStation`/`FridgeStation` dans la scène (`FindAnyObjectByType`), tous les cuisiniers convergeaient vers exactement le même point (`transform.position`). Les coroutines tournaient déjà en parallèle (aucun verrou logique), mais visuellement les employés se superposaient parfaitement au même endroit, donnant l'impression qu'ils alternaient plutôt que de travailler simultanément sur des monstres différents.
- Fix : ajout de `PersonalOffset()` — décalage stable par employé (basé sur `GetInstanceID()`, distribué en cercle autour du point). `BenchPosition()`/`FridgePosition()` remplacent les accès directs à `_bench.transform.position`/`_fridge.transform.position` dans tous les déplacements (`GoToKitchen`, `WaitAtCounter`, `CookRoutine`).
- Chaque cuisinier a donc un point d'attente/travail légèrement distinct autour de la station partagée — visible clairement en parallèle plutôt que superposé.
- **Cause** : quand `ReceptionQueueManager.Instance` est `null` au moment de `RegisterArrival` (comptoir/gestionnaire de file pas encore présent dans la scène), le monstre était marqué `HasArrived = true` sans jamais recevoir le moindre ordre de déplacement — figé à son point de spawn pour toujours, même si un `ReceptionQueueManager` apparaissait plus tard.
- Fix : dans ce cas (ainsi que file pleine), le monstre marche désormais vers le `ReceptionDesk` le plus proche s'il en existe un, au lieu de rester figé.

### EmployeeInstance.cs — Système de pause désactivé (mis en commentaire)
- Aucune "break room" n'a jamais été construite/testée en scène — la fonctionnalité pause existait dans le code mais n'était pas finie.
- Mis en commentaire (pas supprimé, pour réactivation facile) : `UpdateNeeds()` (fatigue/bien-être), le bloc pause de `UpdateSchedule()` (auto-déclenchement + transition GoingToBreak→OnBreak), `CheckResignation()`, `ForceWork()`, `GoOnBreak()`, les branches GoingToBreak/OnBreak de `SetState()`, et `GoToBreakRoom()`.
- **Conservé actif** : la partie non liée à la pause de `UpdateSchedule()` — passage `Idle → GoingToWork` selon `workStartHour`/`workEndHour`. Sans ça les employés ne démarreraient jamais leur service.
- Effet : un employé travaille en continu pendant ses heures, sans jamais partir en pause ni pouvoir démissionner pour cause de bien-être bas. `EmployeeInteractionWheel` (boutons Pause/Reprendre) continue de compiler et de s'afficher mais devient inerte (no-op).
- Objectif : isoler et valider le reste du système employé (recrutement, IA de tâches, verrouillage anti-doublon) sans la dépendance non testée à une salle de pause.

### EmployeeInstance.cs — Fix critique : employés bloqués à vie en pause (cause racine de 4 bugs)
- **Cause** : `SetState(EmployeeState.OnBreak)` n'était appelé nulle part dans le code. Un employé passant en `GoingToBreak` marchait vers la salle de pause (ou restait sur place si aucune salle n'existe) mais rien ne le faisait jamais transiter vers `OnBreak`. Le décompte de `_breakTimer` ne se faisait que `if (State == OnBreak)` — jamais atteint. L'employé restait donc bloqué indéfiniment en `GoingToBreak`, dès le premier déclenchement de pause automatique.
- Ce bug explique à lui seul plusieurs symptômes signalés séparément :
  - Cuisinier "bloqué au bout d'un moment" (pause déclenchée après une tâche longue qui a fait dépasser `breakInterval`)
  - Un seul nettoyage effectué en tout, le 1er employé n'enchaînant jamais sur une 2e tâche, le 2e employé ne faisant jamais rien (accumule `_workTimer` même en restant idle, part en pause avant même d'avoir trouvé une tâche, reste bloqué)
  - Réceptionniste qui "n'accepte plus personne" au bout d'un moment (rien à voir avec sa note — bloqué en pause)
  - Nouveaux monstres qui restent au spawn point (la file de réception se remplit puisque personne n'est plus jamais accepté, bloquant les 5 slots physiques)
- Fix : `GoingToBreak` bascule automatiquement vers `OnBreak` dès le tick suivant, indépendamment de l'arrivée physique à la salle de pause (le déplacement du `mover` continue en tâche de fond). Évite aussi de dépendre de `MonsterMover.OnArrived`, qui n'est de toute façon jamais invoqué par la variante `MoveTo(entryPoint, finalTarget)` utilisée par `GoToBreakRoom()` pour les `RoomInstance` (bug latent distinct, non corrigé ici faute de besoin).

### Verrouillage de tâche entre employés (anti-doublon, E2)
- **FurnitureInstance.cs** : ajout de `IsBeingCleaned` + `ReserveCleaning()` / `ReleaseCleaning()`.
- **DebrisInstance.cs** : ajout de `IsReserved` + `Reserve()` / `ReleaseReservation()`.
- **EatingSpot.cs** : ajout de `IsReservedForService` + `ReserveForService()` / `ReleaseServiceReservation()`. Réinitialisé automatiquement dans `Release()`.
- **CleaningEmployeeAI.cs** : `FindDirtyFurniture()` et `FindDebris()` excluent désormais les cibles déjà réservées. La réservation est posée de façon synchrone dès qu'une cible est choisie dans `Update()` (avant `StartCoroutine`), donc deux employés dans la même frame ne peuvent jamais choisir la même cible — `Update()` des MonoBehaviour s'exécute séquentiellement, pas en parallèle. Libération dans `EndTask()`, couvrant tous les cas de sortie (succès, timeout, interruption).
- **CookEmployeeAI.cs** : même principe pour `FindWaitingSpot()` — exclut les `EatingSpot` déjà réservés par un autre cuisinier. Libération dans `EndCycle()`.
- La réception n'a pas ce problème : elle traite toujours "le prochain en tête de file" au moment de l'appel (pas de cible mise en cache), donc deux réceptionnistes ne peuvent pas dupliquer un traitement — ils accélèrent juste le débit.

### ReservationSystem.cs — Redesign de la logique d'acceptation (remplace le fix "zone d'attente")
- **Revert** : la "zone d'attente sur le côté" (`MoveToWaitingArea`, `ReceptionQueueManager.WaitingAreaPosition`) ajoutée dans le fix précédent ne correspondait pas au comportement attendu — retirée entièrement.
- **Nouveau comportement, conforme à la file d'attente classique** : `CheckInNext()` n'accepte désormais le monstre en tête de file (`NextUnchecked`) que si une chambre compatible est **déjà disponible** (`GetCompatibleRooms(guest.Data).Count > 0`). Sinon, rien ne se passe — le monstre reste simplement en tête de file jusqu'à ce qu'une chambre existe.
- Conséquence : un monstre ne peut plus jamais être "checked-in sans chambre" — cet état intermédiaire (`NextCheckedInWaiting`) n'est plus jamais atteint, éliminant du même coup le bug de chevauchement (plus besoin de déplacer qui que ce soit ailleurs, la file reste toujours cohérente).
- Identique pour le joueur (`ReceptionInteractor`) et l'employé (`ReceptionEmployeeAI`) puisque les deux appellent `CheckInNext()`.
- `ReceptionEmployeeAI` : commentaire et condition de déclenchement nettoyés (`NextCheckedInWaiting` retiré de la condition, devenu inatteignable).

### ReservationSystem.cs — Fix file d'attente bloquée (cause racine)
- **Cause identifiée** : `TryAssignRoom` ne libérait le slot physique de la file (`ReceptionQueueManager.ReleaseSlot`) que si une chambre était trouvée immédiatement (`if (rooms.Count == 0) return false;` sortait avant l'appel). Un monstre accepté au comptoir mais sans chambre disponible restait donc coincé indéfiniment dans son slot, bloquant tous les monstres derrière lui dans la file.
- Fix : `CheckInNext()` libère désormais le slot **dès l'acceptation** (`guest.IsCheckedIn = true`), indépendamment de la disponibilité d'une chambre. `guest.QueueSlotIndex` repassé à `-1` pour éviter un double-release plus tard dans `TryAssignRoom`.
- `NextUnchecked` revenu à un simple FIFO par ordre d'insertion dans `_pending` (le tri par `QueueSlotIndex` introduit précédemment ne réglait pas le vrai problème et ajoutait une dépendance fragile à la synchronisation des slots).

## 2026-06-01

### EmployeeBoardTrigger.cs *(nouveau fichier)*
- Créé : tableau RH interactif dans la scène — le joueur s'approche et appuie sur Interact pour ouvrir le panneau d'embauche
- Détection par distance (`interactRange = 2.5f`) via `RoomPlacer.All` (tous les joueurs)
- `TogglePanel(playerIndex)` : un panel par joueur ou panel 0 en fallback
- Auto-découverte du/des `EmployeeHiringPanel` au `Start()` si `panels[]` non assigné dans l'Inspector

### EmployeeInteractionWheel.cs *(nouveau fichier)*
- Créé : roue d'interaction affichée quand le joueur est proche d'un employé
- 3 actions : Licencier (`Fire`), Envoyer en pause (`GoOnBreak`), Reprendre le travail (`ForceWork`)
- `RefreshButtons()` désactive les boutons selon l'état courant de l'employé (`Working`, `OnBreak`, `Resigning`…)
- Se ferme automatiquement si le joueur s'éloigne (`IsAnyPlayerNear()`)
- Le canvas s'oriente vers la caméra (`LookRotation`) à chaque frame
- **Setup Unity requis** : Canvas WorldSpace en enfant du prefab employé, refs boutons assignées

### EmployeeHiringPanel.cs
- Retiré le `void Start() => Show()` de debug
- Ajout de `gameObject.SetActive(false)` en fin d'Awake (panel caché par défaut)
- Retiré les logs de debug de `BuildHired()`
- `OnHire()` simplifié : copie simple des données, le scaling est maintenant dans `EmployeeInstance.Init()`

### EmployeeInstance.cs
- Ajout de `ApplyRatingScaling()` appelé depuis `Init()` — applique le scaling à TOUS les employés quelle que soit la source d'instanciation
- Ajout de `BlockBreak { get; set; }` — empêche la pause automatique de couper une tâche critique en cours (utilisé par CookEmployeeAI)
- La pause auto dans `UpdateSchedule()` vérifie désormais `!BlockBreak` avant de déclencher `GoingToBreak`

### HotelConfig.cs
- Ajout des paramètres d'efficacité par note (section "Employés — efficacité") :
  - `employeeBreakIntervalMinMult` / `employeeBreakIntervalMaxMult`
  - `employeeSpeedMinMult` / `employeeSpeedMaxMult`
  - `employeeFatigueRateMinMult` / `employeeFatigueRateMaxMult`
  - `employeeRecoveryRateMinMult` / `employeeRecoveryRateMaxMult`
  - `employeeCleanBaseDuration`
  - `employeeCheckInBaseDelay`
  - `employeeRatingCurveDivisor`
- Ajout des paramètres qualité cuisinier (section "Cuisine — qualité employé") :
  - `cookDeliveryBonusMin` (5f) — bonus satisfaction note 1
  - `cookDeliveryBonusMax` (25f) — bonus satisfaction note 20
- Ajout de `debugForceFullDirtyOnVacate` (debug) — force la chambre sale après départ monstre

### ReceptionEmployeeAI.cs
- Réécrit entièrement
- Utilise `ReceptionDesk.All` (plus de `FindAnyObjectByType` chaque frame)
- Flag `_wentToDesk` pour éviter les appels répétés à `GoToDesk()`
- Condition corrigée : traite `NextUnchecked` ET `NextCheckedInWaiting` (monstres check-in sans chambre)
- Délai de check-in scalé par la note via `employeeRatingCurveDivisor`

### ReservationSystem.cs
- `ProcessQueue()` étendu : gère maintenant à la fois les monstres déjà check-in (sans chambre) ET les monstres non check-in (check-in automatique si chambre disponible)

### RoomInstance.cs
- `AddFurniture()` : déclenche `ProcessQueue()` quand la chambre est entièrement meublée
- `SetState()` : déclenche `ProcessQueue()` quand l'état passe à `Empty`
- `ApplyPostMonsterEffects()` : respecte le flag `debugForceFullDirtyOnVacate`
- `SpawnDebris(bool forceMax)` : signature mise à jour pour le mode debug

### DebrisInstance.cs
- Ajout du registre global `static HashSet<DebrisInstance> All` (Awake/OnDestroy) — utilisé par `CleaningEmployeeAI.FindDebris()`
- Ajout de `Room { get; private set; }` et `Init(RoomInstance)` — lie chaque détritus à sa chambre d'origine
- `PickUp()` : notifie la chambre via `Room?.RemoveDebris(this)` avant `Destroy`, déclenchant `TryAutoClean()`
- `OnDestroy` libère aussi le slot dans `All` et dans `Room._debris` (sécurité si destruction externe)

### FurnitureInstance.cs
- `SetDirty(false)` et `SetDamaged(false)` appellent désormais `Room?.TryAutoClean()` — la chambre se remet automatiquement en `Empty` dès que tous ses meubles sont propres et non abimés (et 0 détritus)
- `RefreshStateVisual()` : teinte URP/Standard selon l'état (jaune = sale, rouge = abimé, blanc = propre)

### CleaningEmployeeAI.cs
- Réécrit pour gérer à la fois les **meubles sales/abîmés** ET les **débris au sol**
- Ajout de `FindDebris()` — itère `DebrisInstance.All`
- Ajout de `PickupDebrisRoutine()` — déplace vers le débris, attend l'arrivée, appelle `debris.PickUp()`
- Après ramassage : `TryAutoClean()` peut remettre la chambre en `Empty`

### EatingSpot.cs
- Ajout de `CompleteDelivery(float satisfactionBonus)` — API pour CookEmployeeAI (livraison sans interaction joueur)
- Ajout de `WaitingMonsterData` — expose le `MonsterData` du monstre assis pour la sélection de recette

### CookEmployeeAI.cs
- **Refonte complète** du flux de comportement :
  1. Attend au plan de travail (comptoir)
  2. Détecte un `EatingSpot.IsWaitingForFood`
  3. Détermine la recette via `HotelConfig.recipes` (matching `compatibleMonsters` ↔ `MonsterData`)
  4. Va au frigo (`FridgeStation`)
  5. Va au plan de travail (`WorkbenchStation`), cuisine pour `recipe.workDuration` × scaling note
  6. Va à table, appelle `CompleteDelivery(GetDeliveryBonus())`
  7. Retourne au comptoir
- `BlockBreak = true` pendant le cycle de service, `false` à la fin → la pause est différée mais jamais perdue
- Suppression des vérifications d'état internes au cycle (la pause ne peut plus couper le service)
- `GetDeliveryBonus()` : bonus qualité = `Lerp(cookDeliveryBonusMin, cookDeliveryBonusMax, (rating-1)/19)`
- `GetCookDuration()` : vitesse = `base × (1 - (rating-1) / divisor)`

---

---

## 2026-07-09

### ReceptionDesk.cs — Interaction joueur à la réception
- Ajout de la détection Interact joueur (même pattern que `FridgeStation`, `EmployeeBoardTrigger`)
- Le joueur peut valider l'arrivée d'un monstre en s'approchant du comptoir + Interact → `CheckInNext()`
- Cohérent avec le principe "le joueur peut faire tout ce que les employés font"

### ReservationSystem.cs — Fix auto-check-in bypass réceptionniste
- `ProcessQueue()` n'assigne désormais des chambres qu'aux monstres **déjà checkés** (par le joueur ou le réceptionniste)
- Suppression de l'auto-check-in qui se déclenchait quand une chambre devenait disponible, court-circuitant la réception

### EatingSpot.cs + DebrisInstance.cs — Saleté après repas (U3)
- `DebrisInstance` : ajout de `OnRemoved` (event déclenché dans `OnDestroy`) — permet à l'`EatingSpot` de tracker la destruction de ses débris sans couplage fort.
- `EatingSpot` : ajout de `mealDebrisPrefabs[]` et `maxMealDebris` (inspector). Après un repas (`EatRoutine` ou `CompleteDelivery`), `SpawnMealDebris()` instancie 1–N débris autour de la table.
- `_dirtyCount` : compteur decrementé par `OnRemoved` de chaque débris. Tant que `IsDirty = true`, `IsOccupied = true` → le slot est indisponible pour un nouveau monstre.
- Le `CleaningEmployeeAI` ramasse ces débris automatiquement via `DebrisInstance.All` — aucune modification de l'IA.
- Le timeout (monstre qui repart sans manger) ne génère pas de saleté.

### ReceptionEmployeeAI.cs — Fix check-in bloqué
- `CheckInRoutine` : retiré le guard `if (_employee.State == EmployeeState.Working)` avant `CheckInNext()` — un break pouvant se déclencher pendant le délai empêchait la validation. Remplacement par `BlockBreak = true` pendant toute la durée de la routine (pattern identique au cuisinier).
- `delay` protégé avec `Mathf.Max(0f, ...)` pour éviter un délai négatif sur des notes élevées avec un petit diviseur.

### MonsterMover.cs — Fix positionnement Y (deux bugs)
- **Bug 1 — spawn dans le sol** : le `NavMeshAgent` était actif dès l'`Awake`, ce qui déclenchait un auto-snap NavMesh à l'instanciation et pouvait enterrer certains prefabs selon leur pivot. Fix : `_agent.enabled = false` dans `Awake` ; `NavigateTo` le réactive quand nécessaire.
- **Bug 3 — agent laissé activé sur fallback DirectMove** : quand `NavMesh.SamplePosition` échoue, l'agent était mis à `enabled = true` puis abandonné avant le `DirectMove` de secours. Un agent activé résiste aux modifications manuelles de `transform.position`, bloquant le mouvement. Fix : `_agent.enabled = false` avant d'entrer dans le `DirectMove` fallback.
- **Bug 2 — réceptionniste monte à chaque réservation** : dans `NavigateTo`, `target = hit.position` (résultat de `NavMesh.SamplePosition`) écrasait le Y avec celui de la surface NavMesh. Chaque `CheckInRoutine` déclenchait un nouveau `MoveTo`, un nouveau Warp changeait le Y de référence, les corners du chemin héritaient du mauvais Y — drift cumulatif vers le haut. Fix : ne récupérer que XZ depuis le hit NavMesh, conserver `target.y = transform.position.y` ; restaurer le Y après `Warp`.

---

## Règles de log
- Chaque modification de fichier `.cs` est loguée ici avec la date et le fichier concerné.
- Les correctifs de bug incluent la cause identifiée.
- Les features complètes sont marquées dans TODO.md.
