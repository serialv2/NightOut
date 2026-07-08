namespace NightOut.Models;

public class GooglePlacePrediction
{
    public string Description { get; set; } = string.Empty;
    public string PlaceId { get; set; } = string.Empty;

    public override string ToString()
    {
        return Description;
    }
}