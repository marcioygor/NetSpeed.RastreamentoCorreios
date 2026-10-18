using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Excecoes;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class ConsultaLoteServiceTests
{
    private static ConsultaLoteService CriarServico() => new(NullLogger<ConsultaLoteService>.Instance);
    private static ResultadoRastreamento Resultado(string codigo) => new() { CodigoRastreamento = codigo, Eventos = [new EventoRastreamento { Situacao = "Evento de teste" }] };

    [Fact]
    public void SeparaDelimitadoresSemEntradasVazias() => Assert.Equal(
        ["AA123456789BR", "BB123456789BR", "CC123456789BR", "DD123456789BR"],
        ConsultaLoteService.SepararCodigos(" AA123456789BR; BB123456789BR,\r\nCC123456789BR\tDD123456789BR; "));

    [Fact]
    public async Task MantemOrdemSequencialENaoInterrompePorCodigoInvalidoOuErroDoPortal()
    {
        var chamados = new List<string>();
        var ativos = 0;
        var resultados = new List<ResultadoConsultaObjeto>();
        await foreach (var item in CriarServico().ConsultarAsync(["aa123456789br", "inválido", "BB123456789BR", "CC123456789BR"], async (codigo, token) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref ativos));
            try
            {
                chamados.Add(codigo);
                await Task.Delay(10, token);
                if (codigo.StartsWith("BB", StringComparison.Ordinal))
                    throw new ConsultaRastreamentoException("Objeto não encontrado");
                return Resultado(codigo);
            }
            finally
            {
                Interlocked.Decrement(ref ativos);
            }
        }, CancellationToken.None))
            resultados.Add(item);
        Assert.Equal(["AA123456789BR", "BB123456789BR", "CC123456789BR"], chamados);
        Assert.Equal(4, resultados.Count);
        Assert.NotNull(resultados[0].Resultado);
        Assert.Equal("validacao", resultados[1].TipoErro);
        Assert.Equal("portal", resultados[2].TipoErro);
        Assert.Equal("Objeto não encontrado", resultados[2].Erro);
        Assert.NotNull(resultados[3].Resultado);
    }

    [Fact]
    public async Task CancelamentoPreservaItemEntregueEImpedeProximoObjeto()
    {
        using var cancelamento = new CancellationTokenSource();
        var chamadas = 0;
        await using var enumerador = CriarServico().ConsultarAsync(["AA123456789BR", "BB123456789BR"], (codigo, _) =>
        {
            chamadas++;
            return Task.FromResult(Resultado(codigo));
        }, cancelamento.Token).GetAsyncEnumerator();
        Assert.True(await enumerador.MoveNextAsync());
        Assert.Equal("AA123456789BR", enumerador.Current.Resultado?.CodigoRastreamento);
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerador.MoveNextAsync());
        Assert.Equal(1, chamadas);
    }

    [Fact]
    public async Task ErroInternoNaoEhClassificadoComoCodigoInvalido()
    {
        await foreach (var item in CriarServico().ConsultarAsync(["AA123456789BR"], (_, _) => throw new ArgumentException("Detalhe interno"), CancellationToken.None))
        {
            Assert.Equal("automacao", item.TipoErro);
            Assert.DoesNotContain("Detalhe interno", item.Erro);
        }
    }

    [Fact]
    public async Task FalhaTecnicaMantemContextoNoLogEMensagemAmigavelNoResultado()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var registro = new RegistroExecucaoProvider(diretorio);
            using var fabrica = LoggerFactory.Create(logging => logging.AddProvider(registro));
            var servico = new ConsultaLoteService(fabrica.CreateLogger<ConsultaLoteService>());
            await foreach (var item in servico.ConsultarAsync(["AA123456789BR"], (_, _) =>
                throw new InvalidOperationException("Stack técnico reservado", new IOException("Causa interna")), CancellationToken.None))
            {
                Assert.Equal("automacao", item.TipoErro);
                Assert.Equal("Não foi possível concluir a consulta. Consulte os logs para os detalhes.", item.Erro);
            }
            var log = File.ReadAllText(Assert.Single(Directory.GetFiles(diretorio, "*.log")));
            Assert.Contains("Stack técnico reservado", log);
            Assert.Contains("Causa interna", log);
            Assert.Contains(nameof(FalhaTecnicaMantemContextoNoLogEMensagemAmigavelNoResultado), log);
            Assert.Contains("AA123456789BR", log);
            Assert.All(registro.ObterHistorico(), mensagem => Assert.DoesNotContain("reservado", mensagem.Texto));
        }
        finally
        {
            Directory.Delete(diretorio, true);
        }
    }
}
