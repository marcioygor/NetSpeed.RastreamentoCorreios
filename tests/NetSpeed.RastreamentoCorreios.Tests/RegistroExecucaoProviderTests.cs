using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Modelos;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class RegistroExecucaoProviderTests
{
    [Fact]
    public void FalhaDeDiscoNaoImpedeHistoricoNemNotificacao()
    {
        var caminhoArquivo = Path.GetTempFileName();
        try
        {
            using var registro = new RegistroExecucaoProvider(caminhoArquivo);
            var notificado = false;
            registro.MensagemRegistrada += _ => notificado = true;
            registro.CreateLogger("Teste").LogInformation("Consulta de teste");
            Assert.True(notificado);
            Assert.Contains("Não foi possível gravar", Assert.Single(registro.ObterHistorico()).Texto);
        }
        finally
        {
            File.Delete(caminhoArquivo);
        }
    }

    [Fact]
    public void ExcecaoCompletaFicaNoArquivoSemExporDetalhesNaNotificacao()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var registro = new RegistroExecucaoProvider(diretorio);
            MensagemLog? notificada = null;
            registro.MensagemRegistrada += mensagem => notificada = mensagem;
            try
            {
                throw new InvalidOperationException("Detalhe técnico reservado", new IOException("Causa interna reservada"));
            }
            catch (InvalidOperationException excecao)
            {
                registro.CreateLogger("Consulta").LogError(excecao, "Falha na consulta de {Codigo}.", "AA123456789BR");
                var log = File.ReadAllText(Assert.Single(Directory.GetFiles(diretorio, "*.log")));
                Assert.Contains(excecao.ToString(), log);
                Assert.Contains(nameof(ExcecaoCompletaFicaNoArquivoSemExporDetalhesNaNotificacao), log);
                Assert.Contains("Consulta: Falha na consulta de AA123456789BR.", log);
            }
            Assert.NotNull(notificada);
            Assert.Equal("Falha na consulta de AA123456789BR.", notificada.Texto);
            Assert.DoesNotContain("reservad", Assert.Single(registro.ObterHistorico()).Texto);
        }
        finally
        {
            Directory.Delete(diretorio, true);
        }
    }
}
