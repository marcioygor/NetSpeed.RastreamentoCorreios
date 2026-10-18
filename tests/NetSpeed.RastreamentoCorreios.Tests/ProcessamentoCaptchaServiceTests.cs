using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

[Trait("Categoria", "Ocr")]
public sealed class ProcessamentoCaptchaServiceTests
{
    private static ProcessamentoCaptchaService CriarServico() => new(
        new ConfiguracaoAutomacao(), NullLogger<ProcessamentoCaptchaService>.Instance);

    [Theory]
    [InlineData("ab23")]
    [InlineData("ab234")]
    [InlineData("ab2345")]
    public async Task ExecutaOcrNativoEmImagemSinteticaRotulada(string texto)
    {
        using var imagem = new Bitmap(330, 90);
        using (var desenho = Graphics.FromImage(imagem))
        using (var fonte = new Font("Consolas", 38, FontStyle.Bold))
        {
            desenho.Clear(Color.White);
            desenho.DrawString(texto, fonte, Brushes.Black, new PointF(12, 8));
        }
        using var fluxo = new MemoryStream();
        imagem.Save(fluxo, ImageFormat.Png);
        using var servico = CriarServico();
        var resultado = await servico.ReconhecerAsync(fluxo.ToArray(), CancellationToken.None);
        Assert.Equal(texto, resultado.Texto);
        Assert.InRange(resultado.Confianca, 0, 1);
        Assert.True(resultado.Concordancias > 0);
    }

    [Theory]
    [InlineData("amostra-02.png", "bs3t")]
    [InlineData("amostra-03.png", "598h8")]
    [InlineData("amostra-aceita.png", "wccr")]
    public async Task ReconheceAmostrasLocaisRotuladasManualmente(string arquivo, string esperado)
    {
        var imagem = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Captcha", arquivo));
        using var servico = CriarServico();
        var resultado = await servico.ReconhecerAsync(imagem, CancellationToken.None);
        Assert.Equal(esperado, resultado.Texto);
    }
    [Fact]
    public async Task RecusaReconhecimentoAbaixoDaConfiancaConfigurada()
    {
        var imagem = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Captcha", "amostra-02.png"));
        using var servico = new ProcessamentoCaptchaService(new ConfiguracaoAutomacao { ConfiancaMinimaCaptcha = 1 },
            NullLogger<ProcessamentoCaptchaService>.Instance);
        await Assert.ThrowsAsync<NetSpeed.RastreamentoCorreios.Excecoes.FalhaReconhecimentoCaptchaException>(
            () => servico.ReconhecerAsync(imagem, CancellationToken.None));
    }
    [Fact]
    public async Task RespeitaCancelamentoAntesDeCarregarMotor()
    {
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();
        using var servico = CriarServico();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servico.ReconhecerAsync([], cancelamento.Token));
    }

    [Fact]
    public void RejeitaImagemVazia() => Assert.Throws<InvalidDataException>(() => new PreProcessamentoImagemService().PrepararVariantes([]));

    [Fact]
    public void RejeitaImagemComDimensoesExcessivas()
    {
        using var imagem = new Bitmap(1300, 20);
        using var fluxo = new MemoryStream();
        imagem.Save(fluxo, ImageFormat.Png);
        Assert.Throws<InvalidDataException>(() => new PreProcessamentoImagemService().PrepararVariantes(fluxo.ToArray()));
    }
}
