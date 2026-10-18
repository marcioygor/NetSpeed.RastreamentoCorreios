using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
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
}
