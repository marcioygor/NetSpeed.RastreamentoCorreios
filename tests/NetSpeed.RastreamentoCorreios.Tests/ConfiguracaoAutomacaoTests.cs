using NetSpeed.RastreamentoCorreios.Modelos;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class ConfiguracaoAutomacaoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void RejeitaTimeoutForaDosLimites(int timeout)
    {
        var configuracao = new ConfiguracaoAutomacao { TimeoutNavegacaoSegundos = timeout };
        Assert.Throws<InvalidDataException>(configuracao.Validar);
    }

    [Fact]
    public void RejeitaPortalSemHttps()
    {
        var configuracao = new ConfiguracaoAutomacao { UrlPortal = new Uri("http://exemplo.test/") };
        Assert.Throws<InvalidDataException>(configuracao.Validar);
    }

    [Fact]
    public void AceitaConfiguracaoPadrao() => new ConfiguracaoAutomacao().Validar();
    [Fact]
    public void LimitaCaptchaADezTentativasPorPadrao()
    {
        Assert.Equal(10, new ConfiguracaoAutomacao().MaximoTentativas);
    }

    [Fact]
    public void AceitaDezTentativas() => new ConfiguracaoAutomacao { MaximoTentativas = 10 }.Validar();

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void RejeitaTimeoutOcrForaDosLimites(int timeout) => Assert.Throws<InvalidDataException>(
        () => new ConfiguracaoAutomacao { TimeoutOcrSegundos = timeout }.Validar());

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void RejeitaQuantidadeDeTentativasForaDoLimite(int tentativas)
    {
        Assert.Throws<InvalidDataException>(() => new ConfiguracaoAutomacao { MaximoTentativas = tentativas }.Validar());
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void RejeitaConfiancaMinimaInvalida(float confianca)
    {
        Assert.Throws<InvalidDataException>(() => new ConfiguracaoAutomacao { ConfiancaMinimaCaptcha = confianca }.Validar());
    }
}
