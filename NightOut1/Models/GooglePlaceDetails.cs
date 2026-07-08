namespace NightOut.Models;

public class GooglePlaceDetails
{
    public string StreetNumber { get; set; } = string.Empty;
    public string StreetName { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
}