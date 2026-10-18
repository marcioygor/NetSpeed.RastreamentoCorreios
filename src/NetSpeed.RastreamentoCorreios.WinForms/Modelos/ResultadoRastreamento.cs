using System.Text.Json.Serialization;

namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record ResultadoRastreamento
{
    [JsonPropertyName("codigoObjeto")]
    public required string CodigoRastreamento { get; init; }
    [JsonPropertyName("eventos")]
    public required IReadOnlyList<EventoRastreamento> Eventos { get; init; }
}