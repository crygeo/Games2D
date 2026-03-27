namespace Gusanito.Config;

public class GameSettings
{
    public int WidthMap { get; set; }
    public int HeightMap { get; set; }
    public int Walls { get; set; }
    public int LaneSize { get; set; }
    public int SpeedMs { get; set; }
    public bool WrapAround { get; set; }
    public int ScorePerFood { get; set; }
    
    public int Width  => WidthMap  + Walls * 2;
    public int Height => HeightMap + Walls * 2;
}