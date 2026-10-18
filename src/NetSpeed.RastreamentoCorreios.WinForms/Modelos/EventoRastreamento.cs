using System.Text.Json.Serialization;

namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record EventoRastreamento
{
    [JsonPropertyName("status")]
    public required string Situacao { get; init; }
    [JsonPropertyName("descricao")]
    public string? Descricao { get; init; }
    [JsonPropertyName("dataHora")]
    public DateTime? DataHora { get; init; }
    [JsonPropertyName("local")]
    public string? Local { get; init; }
    [JsonPropertyName("origem")]
    public string? Origem { get; init; }
    [JsonPropertyName("destino")]
    public string? Destino { get; init; }
    [JsonPropertyName("mensagensComplementares")]
    public IReadOnlyList<string> MensagensComplementares { get; init; } = [];
    [JsonPropertyName("links")]
    public IReadOnlyList<string> Links { get; init; } = [];
    [JsonPropertyName("imagensComplementares")]
    public IReadOnlyList<string> ImagensComplementares { get; init; } = [];
    [JsonPropertyName("textoOriginal")]
    public string? TextoOriginal { get; init; }
}