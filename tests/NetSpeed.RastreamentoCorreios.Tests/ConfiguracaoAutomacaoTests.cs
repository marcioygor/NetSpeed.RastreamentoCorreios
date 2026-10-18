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

    [Theory]
    [InlineData(0, 8, "abc", "O número mínimo de caracteres do CAPTCHA deve ser pelo menos 1.")]
    [InlineData(4, 13, "abc", "O número máximo de caracteres do CAPTCHA não pode exceder 12.")]
    [InlineData(8, 4, "abc", "O número mínimo de caracteres do CAPTCHA não pode exceder o máximo.")]
    [InlineData(4, 8, null, "Informe os caracteres permitidos no CAPTCHA.")]
    [InlineData(4, 8, " ", "Informe os caracteres permitidos no CAPTCHA.")]
    [InlineData(4, 8, "ábc", "Os caracteres permitidos no CAPTCHA devem conter apenas letras ASCII e números.")]
    public void RejeitaFormatoCaptchaInvalido(int minimo, int maximo, string? caracteres, string mensagem)
    {
        var configuracao = new ConfiguracaoAutomacao
        {
            MinimoCaracteresCaptcha = minimo,
            MaximoCaracteresCaptcha = maximo,
            CaracteresCaptcha = caracteres!
        };
        var erro = Assert.Throws<InvalidDataException>(configuracao.Validar);
        Assert.Equal(mensagem, erro.Message);
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
    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void RejeitaTimeoutElementoForaDosLimites(int timeout) => Assert.Throws<InvalidDataException>(
        () => new ConfiguracaoAutomacao { TimeoutElementoSegundos = timeout }.Validar());

    [Theory]
    [InlineData(49)]
    [InlineData(5001)]
    public void RejeitaIntervaloVerificacaoForaDosLimites(int intervalo) => Assert.Throws<InvalidDataException>(
        () => new ConfiguracaoAutomacao { IntervaloVerificacaoMilissegundos = intervalo }.Validar());

    [Theory]
    [InlineData(99)]
    [InlineData(30001)]
    public void RejeitaIntervaloTentativasForaDosLimites(int intervalo) => Assert.Throws<InvalidDataException>(
        () => new ConfiguracaoAutomacao { IntervaloTentativasMilissegundos = intervalo }.Validar());

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejeitaConfiancaNaoFinita(float confianca) => Assert.Throws<InvalidDataException>(
        () => new ConfiguracaoAutomacao { ConfiancaMinimaCaptcha = confianca }.Validar());

    [Fact]
    public void RejeitaPortalNuloOuRelativo()
    {
        Assert.Throws<InvalidDataException>(() => new ConfiguracaoAutomacao { UrlPortal = null! }.Validar());
        Assert.Throws<InvalidDataException>(() => new ConfiguracaoAutomacao { UrlPortal = new Uri("/app", UriKind.Relative) }.Validar());
    }

    [Theory]
    [InlineData(1, 50, 100, 0f)]
    [InlineData(300, 5000, 30000, 1f)]
    public void AceitaLimitesInclusivos(int timeout, int verificacao, int intervaloTentativas, float confianca)
    {
        new ConfiguracaoAutomacao
        {
            TimeoutNavegacaoSegundos = timeout,
            TimeoutElementoSegundos = timeout,
            TimeoutOcrSegundos = timeout,
            IntervaloVerificacaoMilissegundos = verificacao,
            IntervaloTentativasMilissegundos = intervaloTentativas,
            ConfiancaMinimaCaptcha = confianca,
            MinimoCaracteresCaptcha = 1,
            MaximoCaracteresCaptcha = 12,
            CaracteresCaptcha = "aZ09"
        }.Validar();
    }
}
