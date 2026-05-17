using System;
using System.Collections.Generic;

/// <summary>
/// Contrato de telemetria que Unity envia al API Core RehabiAPP.
/// Debe coincidir con TelemetriaSesionRequest en /api.
/// </summary>
[Serializable]
public class TelemetryPayload
{
    public string dniPaciente;
    public string videojuegoCodigo;
    public string disabilityId;
    public string codTratamiento;
    public string parteCuerpo;
    public string inicio;
    public string fin;
    public long duracionMs;
    public int puntuacion;
    public Dictionary<string, object> metricas;
}
