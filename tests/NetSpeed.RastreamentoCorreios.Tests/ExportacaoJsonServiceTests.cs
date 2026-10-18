using System.Text.Json;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class ExportacaoJsonServiceTests : IDisposable
{
    private readonly ExportacaoJsonService servico = new();
    private readonly string diretorio = Path.Combine(Path.GetTempPath(), "NetSpeed.Tests", Guid.NewGuid().ToString("N"));

    private static ResultadoRastreamento CriarResultado() => new()
    {
        CodigoRastreamento = "AA123456789BR",
        Eventos =
        [
            new EventoRastreamento { Situacao = "Evento ilustrativo de teste", DataHora = new DateTime(2026, 8, 31, 16, 4, 0), Local = "São José do Rio Preto - SP", MensagensComplementares = ["Mensagem adicional", "Outra informação"] },
            new EventoRastreamento { Situacao = "Transferência de teste", Origem = "Unidade A", Destino = "Unidade B", TextoOriginal = "Texto integral do evento de teste" }
        ]
    };

    [Fact]
    public void PreservaContratoCamposOpcionaisMensagensEOrdem()
    {
        using var documento = JsonDocument.Parse(servico.Serializar(CriarResultado()));
        var raiz = documento.RootElement;
        Assert.Equal("AA123456789BR", raiz.GetProperty("codigoObjeto").GetString());
        Assert.False(raiz.TryGetProperty("codigoRastreamento", out _));
        var eventos = raiz.GetProperty("eventos");
        Assert.Equal(2, eventos.GetArrayLength());
        Assert.Equal("São José do Rio Preto - SP", eventos[0].GetProperty("local").GetString());
        Assert.Equal(JsonValueKind.Null, eventos[0].GetProperty("descricao").ValueKind);
        Assert.Equal("2026-08-31T16:04:00", eventos[0].GetProperty("dataHora").GetString());
        Assert.Equal(2, eventos[0].GetProperty("mensagensComplementares").GetArrayLength());
        Assert.Equal("Unidade A", eventos[1].GetProperty("origem").GetString());
        Assert.Equal("Unidade B", eventos[1].GetProperty("destino").GetString());
        Assert.Equal(JsonValueKind.Null, eventos[1].GetProperty("dataHora").ValueKind);
        Assert.Equal("Texto integral do evento de teste", eventos[1].GetProperty("textoOriginal").GetString());
    }

    [Fact]
    public void SerializaListaVaziaSemInventarEventos()
    {
        using var documento = JsonDocument.Parse(servico.Serializar(new ResultadoRastreamento { CodigoRastreamento = "AA123456789BR", Eventos = [] }));
        Assert.Empty(documento.RootElement.GetProperty("eventos").EnumerateArray());
    }

    [Fact]
    public void PreservaTodosOsCamposEmRoundTrip()
    {
        var resultado = CriarResultado() with
        {
            Eventos = [new EventoRastreamento
            {
                Situacao = "Situação & descrição de teste",
                Descricao = "Acentos: ç, ã, é; aspas: \"valor\"; quebra\nsegunda linha",
                Links = ["https://exemplo.test/?a=1&b=2"],
                ImagensComplementares = ["data:image/png;base64,AA=="],
                TextoOriginal = "Texto original integral"
            }]
        };
        var json = servico.Serializar(resultado);
        var recuperado = JsonSerializer.Deserialize<ResultadoRastreamento>(json)!;
        var evento = Assert.Single(recuperado.Eventos);
        Assert.Equal(resultado.Eventos[0].Descricao, evento.Descricao);
        Assert.Equal(resultado.Eventos[0].Links, evento.Links);
        Assert.Equal(resultado.Eventos[0].ImagensComplementares, evento.ImagensComplementares);
        Assert.Equal(resultado.Eventos[0].TextoOriginal, evento.TextoOriginal);
        Assert.Contains("Situação", json);
    }

    [Fact]
    public async Task ExportaLoteComResultadoEErroIndependentes()
    {
        Directory.CreateDirectory(diretorio);
        var caminho = Path.Combine(diretorio, "lote.json");
        var itens = new ResultadoConsultaObjeto[]
        {
            new() { CodigoInformado = "AA123456789BR", Resultado = CriarResultado() },
            new() { CodigoInformado = "inválido", TipoErro = "validacao", Erro = "Código inválido" }
        };
        await servico.ExportarLoteJsonAsync(itens, caminho, CancellationToken.None);
        using var documento = JsonDocument.Parse(await File.ReadAllTextAsync(caminho));
        var resultados = documento.RootElement.GetProperty("resultados");
        Assert.Equal(2, resultados.GetArrayLength());
        Assert.Equal(2, resultados[0].GetProperty("resultado").GetProperty("eventos").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, resultados[1].GetProperty("resultado").ValueKind);
        Assert.Equal("validacao", resultados[1].GetProperty("tipoErro").GetString());
    }

    [Fact]
    public async Task ExportaJsonUtf8SubstituindoArquivoExistente()
    {
        Directory.CreateDirectory(diretorio);
        var caminho = Path.Combine(diretorio, "resultado.json");
        await File.WriteAllTextAsync(caminho, "anterior");
        await servico.ExportarJsonAsync(CriarResultado(), caminho, CancellationToken.None);
        var conteudo = await File.ReadAllTextAsync(caminho);
        Assert.Equal(servico.Serializar(CriarResultado()), conteudo);
        Assert.Single(Directory.GetFiles(diretorio));
    }

    [Fact]
    public async Task CancelamentoPreservaArquivoExistenteELimpaTemporario()
    {
        Directory.CreateDirectory(diretorio);
        var caminho = Path.Combine(diretorio, "resultado.json");
        await File.WriteAllTextAsync(caminho, "anterior");
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servico.ExportarJsonAsync(CriarResultado(), caminho, cancelamento.Token));
        Assert.Equal("anterior", await File.ReadAllTextAsync(caminho));
        Assert.Single(Directory.GetFiles(diretorio));
    }

    [Fact]
    public async Task PropagaFalhaDeDestinoInexistente()
    {
        var caminho = Path.Combine(diretorio, "inexistente", "resultado.json");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => servico.ExportarJsonAsync(CriarResultado(), caminho, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(diretorio))
            Directory.Delete(diretorio, recursive: true);
    }
}
