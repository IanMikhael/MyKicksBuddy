namespace MyKicksBuddy.Services;

public class DistanceService
{
    // Koordinat tetap lokasi toko mykicksbuddy
    private const double StoreLat = -7.821653;
    private const double StoreLng = 110.129172;
    private const double MaxRadiusKm = 5.0;

    public double CalculateDistanceKm(double customerLat, double customerLng)
    {
        const double earthRadiusKm = 6371;

        var dLat = ToRad(customerLat - StoreLat);
        var dLng = ToRad(customerLng - StoreLng);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(StoreLat)) * Math.Cos(ToRad(customerLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(earthRadiusKm * c, 2);
    }

    public bool IsWithinRadius(double distanceKm) => distanceKm <= MaxRadiusKm;

    private static double ToRad(double degrees) => degrees * Math.PI / 180;
}