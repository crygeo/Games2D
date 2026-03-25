namespace Gusanito.Enum;

public enum SnakeTile
{
    // Cabeza
    HeadUp, HeadDown, HeadLeft, HeadRight,

    // Cuerpo recto
    BodyHorizontal, BodyVertical,

    // Curvas (entrada - salida)
    CurveRightDown,  // (0,0)
    CurveDownLeft,   // (2,0)
    CurveUpRight,    // (0,1)
    CurveLeftUp,     // (2,2)

    // Cola
    TailUp, TailDown, TailLeft, TailRight,

    // Comida
    Food
}
