# Monster Hotel — Données d'équilibrage (Game Design)

Extraction brute de toutes les valeurs numériques présentes dans les ScriptableObjects du projet (`Assets/Data/`, `Assets/Resources/HotelConfig.asset`), au 2026-08-14. Sert de base de réflexion pour l'équilibrage — aucune valeur n'a été modifiée.

---

## 0. Config globale — `Assets/Resources/HotelConfig.asset`

### Économie / progression
| Champ | Valeur | Description |
|---|---|---|
| startingGold | 4000 | Or de départ du joueur en début de partie |
| initialSatisfaction | 80 | Satisfaction de départ (sur 100) d'un client à son arrivée |
| leaveThreshold | 10 | Satisfaction en dessous de laquelle un client quitte l'hôtel (mécontent) |
| renownSpawnSpeedupThreshold | 10 | Seuil de renommée à partir duquel les clients arrivent plus vite |
| renownMaxSpawnReduction | 0.4 | Réduction max (40%) de l'intervalle de spawn apportée par la renommée |
| renownLegendaryThreshold | 30 | Seuil de renommée à partir duquel des clients légendaires peuvent apparaître |
| comfortToSatisfactionBonus | 0.5 | Conversion du confort d'une pièce/meuble en bonus de satisfaction client |

### Réception / pourboires
| Champ | Valeur | Description |
|---|---|---|
| receptionGoodWaitTime | 20 | Temps d'attente (s) en dessous duquel l'accueil est jugé "bon" |
| receptionWaitBonus | 15 | Bonus de satisfaction si accueilli rapidement |
| receptionWaitPenalty | 10 | Pénalité de satisfaction si attente trop longue à la réception |
| tipGoodThreshold | 70 | Satisfaction min pour obtenir le pourboire "bon" |
| tipNormalThreshold | 40 | Satisfaction min pour obtenir le pourboire "normal" |
| tipGoodMultiplier | 0.3 | Pourboire = 30% du revenu si satisfaction "bonne" |
| tipNormalMultiplier | 0.1 | Pourboire = 10% du revenu si satisfaction "normale" |
| waitFoodSatisfactionDecay | 0.5 | Vitesse de perte de satisfaction pendant l'attente d'un repas |
| waitGaugeMaxTime | 40 | Temps (s) au bout duquel la jauge d'attente est pleine |

### Cuisine / livraison
| Champ | Valeur | Description |
|---|---|---|
| deliveryRange | 2 | Distance max pour qu'une livraison de plat au client soit valide |
| cookDeliveryBonusMin | 5 | Bonus minimum donné au cuisinier pour une livraison |
| cookDeliveryBonusMax | 25 | Bonus maximum donné au cuisinier pour une livraison (rapide/parfaite) |

### Temps / journée
| Champ | Valeur | Description |
|---|---|---|
| dayDuration | 250 | Durée d'une journée de jeu (en secondes réelles) |
| startHour | 8 | Heure (in-game) à laquelle démarre chaque journée |
| nightStayDuration | 60 | Durée in-game (probablement en secondes/unité de temps) représentant une nuit d'hôtel |
| mealVisitorChance | 0.6 | Probabilité qu'un client de passage (non résident) vienne pour un repas |
| mealVisitorMaxDuration | 90 | Durée max de présence d'un visiteur "repas" |
| defaultSpawnInterval | 20 | Intervalle par défaut (s) entre deux apparitions de client si non précisé par le monstre |
| spawnOpenHour | 8 | Heure d'ouverture à partir de laquelle les clients peuvent apparaître |
| spawnCloseHour | 20 | Heure de fermeture après laquelle les clients cessent d'apparaître |

### Employés
| Champ | Valeur | Description |
|---|---|---|
| employeeRoomRatio | 3 | Nombre de chambres géré par employé avant besoin d'en embaucher un autre |
| employeeMaxOverride | 6 | Nombre max d'employés autorisés (plafond) |
| employeeFatigueRate | 2 | Vitesse à laquelle la fatigue d'un employé augmente en travaillant |
| employeeRecoveryRate | 5 | Vitesse à laquelle la fatigue diminue en pause/repos |
| employeeResignThreshold | 10 | Niveau de fatigue (ou satisfaction) en dessous duquel l'employé démissionne |
| employeeForceWorkPenalty | 20 | Pénalité appliquée si on force un employé à travailler malgré la fatigue |
| employeeSalaryPerRating | 8 | Salaire journalier par point de "rating" (note) de l'employé |
| employeeFeeMultiplier | 1.5 | Multiplicateur appliqué aux frais d'embauche |
| employeeBreakIntervalMinMult / MaxMult | 0.5 / 1.5 | Bornes min/max du multiplicateur aléatoire de fréquence des pauses |
| employeeSpeedMinMult / MaxMult | 0.75 / 1.25 | Bornes min/max du multiplicateur aléatoire de vitesse de déplacement |
| employeeFatigueRateMinMult / MaxMult | 1.5 / 0.5 | Bornes min/max du multiplicateur aléatoire de vitesse de fatigue |
| employeeRecoveryRateMinMult / MaxMult | 0.75 / 1.25 | Bornes min/max du multiplicateur aléatoire de vitesse de récupération |
| employeeCleanBaseDuration | 4 | Durée de base d'une tâche de nettoyage |
| employeeCheckInBaseDelay | 2 | Délai de base pour effectuer un check-in client |
| employeeRatingCurveDivisor | 38 | Diviseur utilisé dans la courbe de calcul du rating d'un employé |

⚠️ À vérifier dans le code : `employeeSalaryPerRating` (8) × `rating` (10 sur chaque employé, cf. section 5) donnerait 80/jour, alors que le champ `dailySalary` sur chaque EmployeeData est fixé à 30. Il faut savoir laquelle des deux valeurs est réellement utilisée en jeu.

### Déplacement joueur / build mode
| Champ | Valeur | Description |
|---|---|---|
| playerMoveSpeed | 10 | Vitesse de déplacement normale du joueur |
| playerSprintSpeed | 20 | Vitesse de déplacement en sprint |
| chairSnapRange | 1.5 | Distance à laquelle un client/joueur s'accroche automatiquement à une chaise |
| furnitureCursorSpeed | 8 | Vitesse du curseur de placement de meuble |
| furnitureWallMargin | 0.1 | Marge minimale entre un meuble et un mur lors du placement |
| roomCursorSpeed | 10 | Vitesse du curseur de placement de chambre |
| roomPlacementOffset | 2 | Décalage appliqué lors du placement d'une chambre |
| buildModeExtraDistance | 12 | Distance supplémentaire de la caméra en mode construction |
| buildModeZoomLerpSpeed | 4 | Vitesse d'interpolation du zoom en mode construction |
| buildModeCameraFollowSpeed | 15 | Vitesse à laquelle la caméra suit le curseur en mode construction |

### Roaming / errance monstres
| Champ | Valeur | Description |
|---|---|---|
| roamMinWait / roamMaxWait | 1 / 1 | Bornes min/max du temps d'attente avant qu'un monstre parte se balader |
| roamDuration | 40 | Durée d'une session d'errance |
| roamWaypointInterval | 10 | Intervalle entre deux points de passage pendant l'errance |
| roamReturnWait | 25 | Temps d'attente avant le retour du monstre après errance |
| blockInteractRange | 3 | Distance d'interaction avec un bloc (excavation) |
| blockHeight | 3 | Hauteur d'un bloc de terrain |
| blockTargetHysteresis | 0.75 | Marge évitant de changer de cible de bloc trop souvent (anti-oscillation) |

---

## 1. Décoration

| Objet | Coût | Bonus confort | Bonus renommée |
|---|---|---|---|
| Plant1 | 100 | 0.2 | 0.3 |
| Rug1 | 100 | 0 | 0 |
| Rug2 | 100 | 0 | 0 |
| Rug3 | 100 | 0 | 0 |

**Description des champs :** `Coût` = prix d'achat en gold ; `Bonus confort` = confort ajouté à la pièce (influence la satisfaction via `comfortToSatisfactionBonus`) ; `Bonus renommée` = renommée ajoutée à l'hôtel (influence la vitesse de spawn et l'accès aux légendaires).

⚠️ Les 3 tapis n'ont aucun bonus (0/0) — probablement des valeurs placeholder à remplir.

---

## 2. Employés (`Assets/Data/Employee/`)

Les 3 rôles (Réception, Cuisine, Nettoyage) ont **exactement les mêmes stats** actuellement :

| Champ | Valeur | Description |
|---|---|---|
| rating | 10 | Note/compétence de base de l'employé (sert au calcul du salaire et de la qualité de service) |
| dailySalary | 30 | Salaire versé chaque jour à l'employé |
| hiringFee | 50 | Coût unique pour embaucher cet employé |
| moveSpeed | 3.5 | Vitesse de déplacement de l'employé |
| workStartHour / workEndHour | 0 / 24 | Plage horaire de disponibilité pour travailler |
| breakInterval | 120 | Temps de travail avant une pause |
| breakDuration | 30 | Durée d'une pause |

⚠️ Aucune différenciation de coût/salaire entre les 3 métiers pour l'instant.

---

## 3. Objets à tenir en main / Cuisine (`Assets/Data/HoldableObjects/`)

### Station de nettoyage
- ItemBalai (balai), ItemCle (clé) — pas de champ coût/économie, juste type + prefab.

### Ingrédients
| Objet | fillAmount | Description |
|---|---|---|
| Blood | 0.8 | Quantité de besoin (faim) comblée une fois transformé en plat |
| RawBrain | 0.8 | Quantité de besoin (faim) comblée une fois transformé en plat |

### Plats cuisinés
| Objet | fillAmount | Besoin comblé | Description |
|---|---|---|---|
| CookedBrain | 0.8 | Faim | Plat servi au client, comble 80% de la jauge de faim |
| HotBlood | 0.8 | Faim | Plat servi au client, comble 80% de la jauge de faim |

(Le revenu généré par un repas est en fait défini sur `MonsterData.mealRevenue`, voir section 5, pas sur l'objet lui-même.)

### Recettes
| Recette | Durée de préparation | Description |
|---|---|---|
| BrainRecipe (Zombie) | 5 | Temps (s) nécessaire au cuisinier pour préparer le plat à partir de l'ingrédient |
| BloodRecipe (Vampire) | 5 | Temps (s) nécessaire au cuisinier pour préparer le plat à partir de l'ingrédient |

---

## 4. Hôtel — Excavation (`Assets/Data/Hotel/`)

| Bloc | Coût | Durée de creusage | Description |
|---|---|---|---|
| Terre | 40 | 0.5 | Coût en gold et temps pour creuser un bloc de terre (extension de l'hôtel) |
| Roche | 80 | 1.5 | Coût en gold et temps pour creuser un bloc de roche (extension de l'hôtel) |

Roche coûte 2x plus cher et prend 3x plus longtemps à creuser que Terre.

---

## 5. Monstres (`Assets/Data/Monsters/`)

| Monstre | Revenu/nuit | Revenu repas | Séjour (nuits) | Attente max | Poids spawn | Intervalle spawn | Max en attente | Fenêtre checkout |
|---|---|---|---|---|---|---|---|---|
| Zombie | 30 | 10 | 1–1 | 60 | 2 | 20 | 5 | 8h–15.8h |
| Werewolf | 50 | 15 | 1–2 | 30 | 1 | 70 | 3 | 8h–10h |
| Vampire | 80 | 25 | 2–3 | 40 | 1 | 60 | 1 | 8h–10h |

**Description des champs :**
- `Revenu/nuit` : gold rapporté par nuit passée en chambre par ce type de client.
- `Revenu repas` : gold rapporté quand ce client se fait servir un repas.
- `Séjour (nuits)` : nombre de nuits min/max que réserve ce client à chaque venue.
- `Attente max` : temps max (s) que le client patiente avant de partir s'il n'est pas pris en charge.
- `Poids spawn` : poids relatif dans le tirage aléatoire du prochain client à apparaître (plus haut = plus fréquent).
- `Intervalle spawn` : temps moyen (s) entre deux apparitions possibles de ce type de client.
- `Max en attente` : nombre max de clients de ce type pouvant être en file d'attente simultanément.
- `Fenêtre checkout` : plage horaire pendant laquelle ce client peut quitter sa chambre.

Autres champs communs : `moveSpeed` (Zombie 2, Werewolf 4, Vampire 3 — vitesse de déplacement), `isLegendary: 0` pour les 3 (indique une variante légendaire rare), `roamLitterChance` (Zombie 1, Werewolf/Vampire 0.1 — probabilité que le client salisse en se baladant), besoins : Zombie=[Faim], Vampire=[Faim], Werewolf=[] (aucun besoin assigné — probablement à compléter).

---

## 6. Besoins (`Assets/Data/Needs/`)

| Besoin | decayRate | satisfactionDecayPerSecond | unsatisfiedThreshold | criticalThreshold | bonus | pénalité | goodServiceWaitTime | goodQualityThreshold |
|---|---|---|---|---|---|---|---|---|
| Hunger (générique) | 0.01 | 10 | 0.3 | 0.1 | 10 | 5 | 30 | 0.6 |
| Relaxation | 0.05 | 10 | 0.3 | 0.1 | 10 | 5 | 30 | 0.6 |
| HungerVampire | 0.01 | 10 | 0.3 | 0.1 | 10 | 5 | 30 | 0.6 |
| Besoin Zombie ("NeedType_New") | 0.005 | 2 | 0.3 | 0.1 | 10 | 5 | 30 | 0.6 |

**Description des champs :**
- `decayRate` : vitesse à laquelle la jauge de besoin (faim, relaxation...) diminue avec le temps.
- `satisfactionDecayPerSecond` : vitesse à laquelle la satisfaction du client chute une fois le besoin non comblé.
- `unsatisfiedThreshold` : seuil de jauge en dessous duquel le besoin est considéré "non satisfait".
- `criticalThreshold` : seuil de jauge en dessous duquel le besoin devient "critique" (urgence).
- `bonus` : bonus de satisfaction accordé quand le besoin est bien comblé.
- `pénalité` : malus de satisfaction infligé quand le besoin n'est pas comblé à temps.
- `goodServiceWaitTime` : temps de service (s) en dessous duquel la prise en charge du besoin est jugée "bonne".
- `goodQualityThreshold` : qualité de service minimale pour être jugée "bonne".

Relaxation décroît 5x plus vite que la Faim. Le besoin Zombie décroît 2x plus lentement que la Faim générique et a une pénalité de décroissance de satisfaction bien plus faible (2 vs 10).

---

## 7. Meubles (`Assets/Data/Rooms/Furnitures/`)

### Cuisine
| Meuble | Coût | Bonus revenu | Bonus attractivité | Coût upgrade |
|---|---|---|---|---|
| Chaise | 50 | 0 | 0 | 0 |
| Comptoir livraison | 50 | 0 | 0 | 0 |
| Station de cuisine | 50 | 0 | 0 | 0 |
| Frigo | 50 | 0 | 0 | 0 |
| Table | 50 | 0 | 0 | 0 |
| Poubelle | 50 | **5** | **2** | 0 |
| Réception restaurant | 50 | 0 | 0 | 0 |

### Chambre Vampire
| Meuble | Coût | Bonus revenu | Bonus attractivité |
|---|---|---|---|
| Commode | 50 | 0 | 0 |
| Lit Vampire | 50 | 0 | 0 |

### Chambre Zombie
| Meuble | Coût | Bonus revenu | Bonus attractivité |
|---|---|---|---|
| Commode | 50 | **5** | **2** |
| Lit (niv 1) | 50 | 0 | 0 |
| Lit (upgrade) | 50 | 0 | 0 |

**Description des champs :** `Coût` = prix d'achat/placement du meuble ; `Bonus revenu` = gold supplémentaire généré par nuit/service grâce à ce meuble ; `Bonus attractivité` = attractivité ajoutée à la pièce (influence l'affluence/la satisfaction) ; `Coût upgrade` = prix pour améliorer ce meuble vers une version supérieure.

⚠️ Tous les meubles coûtent 50 gold, quel que soit le type. Seule la Poubelle (cuisine) et la Commode Zombie ont un bonus (+5 revenu / +2 attractivité) — la Commode Vampire équivalente est à 0/0. Aucun coût d'upgrade défini sur les meubles actuellement.

---

## 8. Salles de service (`Assets/Data/Rooms/Rooms/Facilities/`)

| Salle | Coût | Revenu base | Attractivité | Qualité | Taille | Capacité | Temps service | Qualité service | % sale | % dégât | Debris max |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Salle de pause | 100 | 20 | 1 | 1 | 2x2 (h1) | 2 | 10 | 0.8 | 0.7 | 0.15 | 2 |
| Cuisine | 50 | 20 | 1 | 1 | 20x20 (h8) | 2 | 10 | 0.8 | 0.7 | 0.15 | 2 |
| Salon (Lounge) | 100 | 20 | 1 | 1 | 15x20 (h5) | 2 | 10 | 0.8 | 0.7 | 0.15 | 2 |

**Description des champs :**
- `Coût` : prix de construction de la salle.
- `Revenu base` : gold généré par défaut par cette salle (avant bonus meubles).
- `Attractivité` : attractivité de base apportée à l'hôtel.
- `Qualité` : niveau de qualité de base de la salle.
- `Taille` : dimensions en unités de grille (largeur x profondeur, hauteur).
- `Capacité` : nombre de clients pouvant être servis simultanément.
- `Temps service` : durée (s) d'un service dans cette salle.
- `Qualité service` : qualité de service de base rendue (0 à 1).
- `% sale` : probabilité que le mobilier de la salle se salisse avec l'usage.
- `% dégât` : probabilité que le mobilier de la salle s'abîme avec l'usage.
- `Debris max` : nombre max de débris/saletés pouvant s'accumuler avant nettoyage obligatoire.

---

## 9. Types de chambre (labels uniquement, pas de valeurs numériques)
Standard, VampireCoffin, Loup-Garou.

---

## 10. Chambres — niveaux (`Assets/Data/Rooms/Rooms/Rooms/`)

| Chambre | Niveau | Coût construction | Revenu base | Attractivité | Qualité | Coût upgrade | Capacité | Taille | % sale | % dégât | Debris max |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Vampire | Niv1 | 100 | 120 | 1 | 1 | 135 | 2 | 10x10 (h8) | 0.7 | 0.15 | 2 |
| Vampire | Niv2 | — | *fichier absent* | — | — | — | — | — | — | — | — |
| Werewolf | Niv1 | 100 | 120 | 1 | 1 | 125 | 2 | 10x10 (h8) | 0.7 | 0.15 | 4 |
| Werewolf | Niv2 | 100 | **30** | 2 | 2 | 0 (max) | — | 10x15 (h5) | 0.7 | 0.15 | 2 |
| Zombie | Niv1 | 100 | 80 | 1 | 1 | 95 | 2 | 10x10 (h8) | **1.0** | **1.0** | 5 |
| Zombie | Niv2 | 100 | **45** | 2 | 2 | 0 (max) | 2 | 10x15 (h5) | 0.7 | 0.15 | 2 |

**Description des champs :**
- `Coût construction` : prix pour bâtir cette chambre pour la première fois.
- `Revenu base` : gold généré par nuit à ce niveau (avant bonus meubles/décoration).
- `Attractivité` / `Qualité` : influencent respectivement l'affluence de clients et la satisfaction générée.
- `Coût upgrade` : prix pour passer au niveau suivant (0 = déjà au niveau maximum).
- `Capacité` : nombre de clients logeables simultanément dans cette chambre.
- `Taille` : dimensions en unités de grille (largeur x profondeur, hauteur).
- `% sale` / `% dégât` : probabilités que la chambre se salisse / s'abîme avec l'usage, nécessitant l'intervention d'un employé.
- `Debris max` : nombre max de débris/saletés accumulables avant nettoyage forcé.

⚠️ **Points à vérifier / incohérences potentielles :**
- Le `baseRevenue` **chute** au niveau 2 par rapport au niveau 1 (Werewolf 120→30, Zombie 80→45), alors que la qualité/attractivité montent (1→2) et que le niveau 2 coûte de l'argent à débloquer. Soit c'est un bug de saisie, soit le revenu final est calculé autrement en code (ex: multiplicateur additif ailleurs) — à vérifier avant de baser l'équilibrage dessus.
- Aucun fichier de niveau 2 n'existe pour la chambre Vampire, alors que son niveau 1 pointe déjà vers un `nextUpgrade` (même GUID que celui partagé par Werewolf/Zombie niveau 2) — chambre Vampire niveau 2 semble non créée.
- La chambre Zombie niveau 1 a `furnitureDirtyChance`/`furnitureDamageChance` à 100%/100%, largement au-dessus du reste du jeu (~70%/15%) — les chambres zombies niveau 1 se salissent/s'abîment systématiquement.

---

## Synthèse des grands motifs économiques actuels

- **Meubles** : prix plat de 50 gold, peu importe le type.
- **Décorations** : prix plat de 100 gold.
- **Construction de chambre** : prix plat de 100 gold pour toutes (Cuisine à 50, moins chère).
- **Upgrades de chambre** : seuls les passages Niv1→Niv2 ont un coût (Vampire 135, Werewolf 125, Zombie 95 — le Zombie, pourtant le moins rentable des 3 monstres, est le moins cher à upgrader).
- **Revenu/nuit par monstre** : Zombie 30 < Werewolf 50 < Vampire 80, globalement proportionnel à la durée de séjour et au revenu repas (10/15/25).
- **Employés** : aucune différenciation de coût/salaire entre les 3 métiers (50 embauche / 30 par jour chacun).
- **Besoins** : constantes de seuils/bonus/pénalité identiques partout (0.3 / 0.1 / +10 / -5 / 30s / 0.6) ; seuls `decayRate` et `satisfactionDecayPerSecond` varient par besoin.
