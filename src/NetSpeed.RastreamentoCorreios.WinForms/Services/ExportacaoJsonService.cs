using System.Text.Encodings.Web;
using System.Text.Json;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class ExportacaoJsonService
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    public string Serializar(ResultadoRastreamento resultado) => JsonSerializer.Serialize(resultado, Opcoes);

    public string SerializarLote(IReadOnlyList<ResultadoConsultaObjeto> resultados) => JsonSerializer.Serialize(new { resultados }, Opcoes);

    public Task ExportarJsonAsync(ResultadoRastreamento resultado, string caminho, CancellationToken cancellationToken)
        => SalvarAsync(Serializar(resultado), caminho, cancellationToken);

    public Task ExportarLoteJsonAsync(IReadOnlyList<ResultadoConsultaObjeto> resultados, string caminho, CancellationToken cancellationToken)
        => SalvarAsync(SerializarLote(resultados), caminho, cancellationToken);

    private static async Task SalvarAsync(string json, string caminho, CancellationToken cancellationToken)
    {
        var caminhoAbsoluto = Path.GetFullPath(caminho);
        var caminhoTemporario = caminhoAbsoluto + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(caminhoTemporario, json, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(caminhoTemporario, caminhoAbsoluto, overwrite: true);
        }
        finally
        {
            if (File.Exists(caminhoTemporario))
                File.Delete(caminhoTemporario);
        }
    }
}
