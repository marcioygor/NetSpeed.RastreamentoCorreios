using System.Text.Json.Serialization;

namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record ResultadoConsultaObjeto
{
    [JsonPropertyName("codigoInformado")]
    public required string CodigoInformado { get; init; }
    [JsonPropertyName("resultado")]
    public ResultadoRastreamento? Resultado { get; init; }
    [JsonPropertyName("tipoErro")]
    public string? TipoErro { get; init; }
    [JsonPropertyName("erro")]
    public string? Erro { get; init; }
}
