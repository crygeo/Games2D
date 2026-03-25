using System.IO;

namespace Gusanito.Config;

using System.Text.Json;

public static class GameSettingsLoader
{
    public static GameSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró el archivo: {path}");

        var json = File.ReadAllText(path);

        var settings = JsonSerializer.Deserialize<GameSettings>(json);

        if (settings == null)
            throw new Exception("Error al deserializar configuración");

        return settings;
    }
    
    public static GameSettings LoadOrDefault(string path)
    {
        try
        {
            return Load(path);
        }
        catch(Exception e)
        {
            Console.WriteLine($"No se pudo cargar configuración desde {path}, usando valores por defecto.");
            Console.WriteLine($"Error: {e.Message}");
            
            return new GameSettings
            {
                Width = 20,
                Height = 20,
                SpeedMs = 150,
                WrapAround = false,
                ScorePerFood = 10
            };
        }
    }
}