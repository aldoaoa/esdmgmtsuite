namespace ESDSuite.Core.Helpers;

public static class AuditEvaluationEngine
{
    public static string EvaluateIonizer(double tiempoDescarga, int voltajeBalance)
    {
        bool cumpleTiempo = tiempoDescarga > 0 && tiempoDescarga <= 10.0;
        bool cumpleBalance = Math.Abs(voltajeBalance) <= 35;
        return (cumpleTiempo && cumpleBalance) ? "PASA" : "PENDIENTE";
    }

    public static string EvaluateFurnitureOrMachinery(double resistencia, int voltajeCampo)
    {
        bool cumpleResistencia = resistencia > 0 && resistencia <= 1.0e9;
        bool cumpleVoltaje = Math.Abs(voltajeCampo) <= 100;
        return (cumpleResistencia && cumpleVoltaje) ? "PASA" : "PENDIENTE";
    }

    public static string EvaluateEventMeter(double voltajeMaximo)
    {
        return voltajeMaximo <= 100.0 ? "APROBADO" : "RECHAZADO";
    }

    public static string EvaluateWalkingTest(double voltajeMaximo)
    {
        return voltajeMaximo < 100.0 ? "PASS" : "FAIL";
    }
}
