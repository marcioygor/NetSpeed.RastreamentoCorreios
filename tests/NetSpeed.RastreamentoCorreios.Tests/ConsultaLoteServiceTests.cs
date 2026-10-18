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
    public async Task TimeoutMantemClassificacaoEMensagemSemDetalhesTecnicos()
    {
        await foreach (var item in CriarServico().ConsultarAsync(["AA123456789BR"],
            (_, _) => throw new TimeoutException("Detalhe técnico reservado"), CancellationToken.None))
        {
            Assert.Equal("timeout", item.TipoErro);
            Assert.Equal("A consulta excedeu o tempo configurado.", item.Erro);
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

    [Fact]
    public async Task LimiteInterrompeNovasConsultasEPreservaResultadosEMensagensSeguras()
    {
        const string original = "We are sorry, but the site has received too many requests. Please try again later.";
        var diretorio = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var registro = new RegistroExecucaoProvider(diretorio);
            using var fabrica = LoggerFactory.Create(logging => logging.AddProvider(registro));
            var servico = new ConsultaLoteService(fabrica.CreateLogger<ConsultaLoteService>());
            var chamadas = new List<string>();
            var anterior = Resultado("AA123456789BR");
            var itens = new List<ResultadoConsultaObjeto>();
            await foreach (var item in servico.ConsultarAsync(["AA123456789BR", "BB123456789BR", "CC123456789BR", "inválido"], (codigo, _) =>
            {
                chamadas.Add(codigo);
                if (chamadas.Count == 1)
                    return Task.FromResult(anterior);
                throw ConsultaRastreamentoException.PorLimiteRequisicoes("HTTP 429: " + original);
            }, CancellationToken.None))
                itens.Add(item);
            Assert.Equal(["AA123456789BR", "BB123456789BR"], chamadas);
            Assert.Equal(4, itens.Count);
            Assert.Same(anterior, itens[0].Resultado);
            Assert.Equal("limite_requisicoes", itens[1].TipoErro);
            Assert.Equal("O portal dos Correios limitou temporariamente as consultas. Tente novamente mais tarde.", itens[1].Erro);
            Assert.All(itens.Skip(2), item =>
            {
                Assert.Null(item.Resultado);
                Assert.Equal("nao_consultado", item.TipoErro);
                Assert.Contains("não consultado", item.Erro);
                Assert.DoesNotContain(original, item.Erro);
            });
            var json = new ExportacaoJsonService().SerializarLote(itens);
            Assert.DoesNotContain(original, json);
            Assert.DoesNotContain("HTTP 429", json);
            var log = File.ReadAllText(Assert.Single(Directory.GetFiles(diretorio, "*.log")));
            Assert.Contains(original, log);
            Assert.Contains("HTTP 429", log);
            Assert.Contains("BB123456789BR", log);
            Assert.Contains(nameof(LimiteInterrompeNovasConsultasEPreservaResultadosEMensagensSeguras), log);
            Assert.All(registro.ObterHistorico(), mensagem => Assert.DoesNotContain(original, mensagem.Texto));
        }
        finally
        {
            Directory.Delete(diretorio, true);
        }
    }

    [Fact]
    public async Task BloqueioNoPrimeiroObjetoNaoConsultaOsRestantesENaoContaminaNovoLote()
    {
        var servico = CriarServico();
        var chamadas = 0;
        var itens = new List<ResultadoConsultaObjeto>();
        await foreach (var item in servico.ConsultarAsync(["AA123456789BR", "BB123456789BR"], (_, _) =>
        {
            chamadas++;
            throw ConsultaRastreamentoException.PorLimiteRequisicoes("HTTP 429");
        }, CancellationToken.None))
            itens.Add(item);
        Assert.Equal(1, chamadas);
        Assert.Equal("limite_requisicoes", itens[0].TipoErro);
        Assert.Equal("nao_consultado", itens[1].TipoErro);
        await foreach (var item in servico.ConsultarAsync(["CC123456789BR"], (codigo, _) => Task.FromResult(Resultado(codigo)), CancellationToken.None))
            Assert.NotNull(item.Resultado);
    }


    [Fact]
    public async Task CodigosRepetidosReutilizamSucessoMantendoEntradasEOrdem()
    {
        var chamados = new List<string>();
        var itens = new List<ResultadoConsultaObjeto>();
        await foreach (var item in CriarServico().ConsultarAsync(["aa123456789br", "BB123456789BR", "AA123456789BR", "aa123456789br"], (codigo, _) =>
        {
            chamados.Add(codigo);
            return Task.FromResult(Resultado(codigo));
        }, CancellationToken.None))
            itens.Add(item);
        Assert.Equal(["AA123456789BR", "BB123456789BR"], chamados);
        Assert.Equal(["aa123456789br", "BB123456789BR", "AA123456789BR", "aa123456789br"], itens.Select(item => item.CodigoInformado));
        Assert.Same(itens[0].Resultado, itens[2].Resultado);
        Assert.Same(itens[0].Resultado, itens[3].Resultado);
        Assert.NotSame(itens[0].Resultado, itens[1].Resultado);
    }

    [Fact]
    public async Task NovaExecucaoDoLoteNaoReaproveitaDadosAnteriores()
    {
        var servico = CriarServico();
        var chamadas = 0;
        for (var execucao = 0; execucao < 2; execucao++)
        {
            await foreach (var item in servico.ConsultarAsync(["AA123456789BR", "AA123456789BR"], (codigo, _) =>
            {
                chamadas++;
                return Task.FromResult(Resultado(codigo));
            }, CancellationToken.None))
                Assert.NotNull(item.Resultado);
        }
        Assert.Equal(2, chamadas);
    }

    [Fact]
    public async Task FalhasNaoSaoArmazenadasComoResultadoConcluido()
    {
        var chamadas = 0;
        var itens = new List<ResultadoConsultaObjeto>();
        await foreach (var item in CriarServico().ConsultarAsync(["AA123456789BR", "AA123456789BR"], (codigo, _) =>
        {
            if (++chamadas == 1)
                throw new IOException("Falha temporária de teste");
            return Task.FromResult(Resultado(codigo));
        }, CancellationToken.None))
            itens.Add(item);
        Assert.Equal(2, chamadas);
        Assert.Equal("automacao", itens[0].TipoErro);
        Assert.NotNull(itens[1].Resultado);
    }

    [Fact]
    public async Task BloqueioImpedeContinuidadeMesmoComResultadoAnteriorEmMemoria()
    {
        var chamadas = 0;
        var itens = new List<ResultadoConsultaObjeto>();
        await foreach (var item in CriarServico().ConsultarAsync(["AA123456789BR", "BB123456789BR", "AA123456789BR"], (codigo, _) =>
        {
            if (++chamadas == 2)
                throw ConsultaRastreamentoException.PorLimiteRequisicoes("HTTP 429");
            return Task.FromResult(Resultado(codigo));
        }, CancellationToken.None))
            itens.Add(item);
        Assert.Equal(2, chamadas);
        Assert.NotNull(itens[0].Resultado);
        Assert.Equal("limite_requisicoes", itens[1].TipoErro);
        Assert.Equal("nao_consultado", itens[2].TipoErro);
    }

    [Fact]
    public async Task CancelamentoImpedeProximaEntradaMesmoSeEstiverEmCache()
    {
        using var cancelamento = new CancellationTokenSource();
        var chamadas = 0;
        await using var enumerador = CriarServico().ConsultarAsync(["AA123456789BR", "AA123456789BR"], (codigo, _) =>
        {
            chamadas++;
            return Task.FromResult(Resultado(codigo));
        }, cancelamento.Token).GetAsyncEnumerator();
        Assert.True(await enumerador.MoveNextAsync());
        cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerador.MoveNextAsync());
        Assert.Equal(1, chamadas);
    }

}
