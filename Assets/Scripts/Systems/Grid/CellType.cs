/// <summary>
/// Type d'une cellule dans la grille de l'hôtel.
/// </summary>
public enum CellType
{
    Empty,   // Aucun sol défini
    Floor,   // Sol praticable (couloir, zone ouverte)
    Wall,    // Mur impraticable
    Room,    // Chambre de monstre
}
