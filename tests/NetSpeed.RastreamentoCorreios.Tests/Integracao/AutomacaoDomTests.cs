using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests.Integracao;

[Trait("Categoria", "Integracao")]
public sealed class AutomacaoDomTests(NavegadorIntegracao ambiente) : IClassFixture<NavegadorIntegracao>
{
    private static JavascriptCorreiosService CriarJavascript(int timeout = 5) => new(new ConfiguracaoAutomacao
    {
        TimeoutElementoSegundos = timeout,
        IntervaloVerificacaoMilissegundos = 50
    });

    private static string Historico(string itens) => $"<div id='ver-rastro-unico'><ul class='ship-steps'>{itens}</ul></div>";
    private static string Evento(string conteudo) => $"<li class='step'><div class='step-content'>{conteudo}</div></li>";

    [Fact]
    public Task ExtraiTodosOsEventosPreservandoCamposOrdemESemDuplicarResumo() => ambiente.ExecutarAsync(async navegador =>
    {
        var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "historico-fixture.html"));
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, html);
        var eventos = await new ExtracaoEventosService(CriarJavascript()).ExtrairEventosAsync(navegador, CancellationToken.None);
        Assert.Equal(3, eventos.Count);
        Assert.Equal("Entrega de teste.", eventos[0].Situacao);
        Assert.Equal("São José do Rio Preto - SP", eventos[0].Local);
        Assert.Equal(new DateTime(2026, 8, 31, 16, 4, 0), eventos[0].DataHora);
        Assert.Equal("Unidade A, Cidade A - SP", eventos[1].Origem);
        Assert.Equal("Unidade B, Cidade B - SP", eventos[1].Destino);
        Assert.Contains("Mensagem complementar detalhe", eventos[1].MensagensComplementares);
        Assert.Equal("https://exemplo.test/detalhe", Assert.Single(eventos[1].Links));
        Assert.Null(eventos[2].DataHora);
        Assert.Null(eventos[2].Origem);
        Assert.Null(eventos[2].Destino);
        Assert.Contains("31/02/2026", eventos[2].TextoOriginal);
    });

    [Fact]
    public Task SuportaClassesConhecidasEmOutrasTagsEDataSeparadaPorQuebra() => ambiente.ExecutarAsync(async navegador =>
    {
        var html = Historico(Evento("""
            <div class='text-head'>Evento alternativo de teste</div>
            <div class='text-content'>São Paulo - SP<br>01/10/2026 09:30:45</div>
            <p>Informação adicional com ç e &amp;.</p>
            <a href='https://exemplo.test/comprovante'>Comprovante</a>
            <img src='data:image/png;base64,AA==' alt='Imagem de teste'>
            """));
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, html);
        var evento = Assert.Single(await new ExtracaoEventosService(CriarJavascript()).ExtrairEventosAsync(navegador, CancellationToken.None));
        Assert.Equal("Evento alternativo de teste", evento.Situacao);
        Assert.Equal("São Paulo - SP", evento.Local);
        Assert.Equal(new DateTime(2026, 10, 1, 9, 30, 45), evento.DataHora);
        Assert.Equal("Informação adicional com ç e &.", evento.Descricao);
        Assert.Single(evento.ImagensComplementares);
        Assert.Contains("Comprovante", evento.TextoOriginal);
    });

    [Theory]
    [InlineData("de Unidade A", "Unidade A", null)]
    [InlineData("para Unidade B", null, "Unidade B")]
    public Task PreservaOrigemEDestinoIndependentemente(string linha, string? origem, string? destino) => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, Historico(Evento($"<p class='text-head'>Teste</p><p class='text-content'>{linha}</p>")));
        var evento = Assert.Single(await new ExtracaoEventosService(CriarJavascript()).ExtrairEventosAsync(navegador, CancellationToken.None));
        Assert.Equal(origem, evento.Origem);
        Assert.Equal(destino, evento.Destino);
        Assert.Null(evento.Local);
    });

    [Theory]
    [InlineData("<html><body>Sem histórico</body></html>")]
    [InlineData("<ul class='ship-steps'></ul>")]
    [InlineData("<div id='ver-rastro-unico'><ul class='ship-steps'></ul></div>")]
    [InlineData("<div id='ver-rastro-unico'><ul class='ship-steps'><li class='step'></li></ul></div>")]
    [InlineData("<div id='ver-rastro-unico'><ul class='ship-steps'><li class='step'><div class='step-content'><p>Sem situação</p></div></li></ul></div>")]
    public Task RejeitaHistoricoAusenteVazioOuIncompativel(string html) => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, html);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ExtracaoEventosService(CriarJavascript()).ExtrairEventosAsync(navegador, CancellationToken.None));
    });

    [Fact]
    public Task NaoOmiteSilenciosamenteUmItemComClasseAlterada() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, Historico(Evento("<p class='text-head'>Evento válido</p>") + "<li class='novo-evento'>Evento que seria ignorado</li>"));
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => new ExtracaoEventosService(CriarJavascript()).ExtrairEventosAsync(navegador, CancellationToken.None));
        Assert.Contains("resultado parcial", erro.Message);
    });

    [Theory]
    [InlineData("CAPTCHA inválido. Tente novamente.", "captcha-invalido")]
    [InlineData("Captcha incorreto", "captcha-invalido")]
    [InlineData("Objeto não encontrado", "erro")]
    [InlineData("Período inválido", "erro")]
    [InlineData("Código inválido", "erro")]
    [InlineData("Serviço indisponível", "erro")]
    [InlineData("Mensagem inesperada", "erro")]
    public Task DiferenciaCaptchaDeOutrasMensagensDoPortal(string mensagem, string esperado) => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador,
            $"<input id='captcha'><input id='objeto'><div id='alerta' class='aberto'><span class='msg'>{WebUtility.HtmlEncode(mensagem)}</span></div>" + Historico(Evento("<p class='text-head'>Histórico antigo</p>")));
        var estado = await CriarJavascript().ExecutarAsync<EstadoConsultaPagina>(navegador, RecursosAplicacao.LerTexto("Scripts.ObterEstadoConsulta.js"), CancellationToken.None);
        Assert.Equal(esperado, estado?.Estado);
        Assert.Equal(mensagem, estado?.Mensagem);
    });

    [Fact]
    public Task NaoConfundeBuscaEmAndamentoComHistoricoAntigo() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, "<div id='alerta' class='aberto'><span class='msg'>Buscando...</span></div>" + Historico(Evento("<p class='text-head'>Histórico antigo</p>")));
        Assert.Null(await CriarJavascript().ExecutarAsync<EstadoConsultaPagina>(navegador, RecursosAplicacao.LerTexto("Scripts.ObterEstadoConsulta.js"), CancellationToken.None));
    });

    [Fact]
    public Task IdentificaRejeicaoNoCampoCaptcha() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, "<input id='captcha'><input id='objeto'>");
        var javascript = CriarJavascript();
        await javascript.ExecutarAsync<bool>(navegador, "(() => { document.getElementById('captcha').setCustomValidity('Captcha inválido!'); return JSON.stringify(true); })()", CancellationToken.None);
        Assert.Equal("captcha-invalido", (await javascript.ExecutarAsync<EstadoConsultaPagina>(navegador, RecursosAplicacao.LerTexto("Scripts.ObterEstadoConsulta.js"), CancellationToken.None))?.Estado);
    });

    [Fact]
    public Task TimeoutDeEstadoEsperadoProduzExcecaoEspecifica() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, "<p>Estado que nunca estará pronto</p>");
        await Assert.ThrowsAsync<TimeoutException>(() => CriarJavascript(1).AguardarAsync<string>(navegador, "(() => null)()", CancellationToken.None));
    });

    [Fact]
    public Task CancelamentoInterrompeEsperaSemNovasVerificacoes() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, "<p>Teste local de cancelamento</p>");
        var javascript = CriarJavascript();
        using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => javascript.AguardarAsync<string>(navegador,
            "(() => { globalThis.contagem = (globalThis.contagem ?? 0) + 1; return null; })()", cancelamento.Token));
        var antes = await javascript.ExecutarAsync<int>(navegador, "(() => JSON.stringify(globalThis.contagem))()", CancellationToken.None);
        await Task.Delay(200);
        var depois = await javascript.ExecutarAsync<int>(navegador, "(() => JSON.stringify(globalThis.contagem))()", CancellationToken.None);
        Assert.True(antes > 0);
        Assert.Equal(antes, depois);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExtracaoEventosService(javascript).ExtrairEventosAsync(navegador, cancelamento.Token));
    });

    [Fact]
    public Task TimeoutDeNavegacaoInterrompeConexaoLocalSemResposta() => ambiente.ExecutarAsync(async navegador =>
    {
        var servidor = new TcpListener(IPAddress.Loopback, 0);
        servidor.Start();
        try
        {
            var porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
            var configuracao = new ConfiguracaoAutomacao { UrlPortal = new Uri($"https://127.0.0.1:{porta}/"), TimeoutNavegacaoSegundos = 1 };
            var navegacao = new NavegacaoCorreiosService(configuracao, NullLogger<NavegacaoCorreiosService>.Instance);
            await Assert.ThrowsAsync<TimeoutException>(() => navegacao.AbrirPortalAsync(navegador, CancellationToken.None));
        }
        finally
        {
            servidor.Stop();
        }
    });

    [Fact]
    public Task CancelamentoInterrompeNavegacaoLocalEmAndamento() => ambiente.ExecutarAsync(async navegador =>
    {
        var servidor = new TcpListener(IPAddress.Loopback, 0);
        servidor.Start();
        try
        {
            var porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
            var configuracao = new ConfiguracaoAutomacao { UrlPortal = new Uri($"https://127.0.0.1:{porta}/") };
            using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var navegacao = new NavegacaoCorreiosService(configuracao, NullLogger<NavegacaoCorreiosService>.Instance);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => navegacao.AbrirPortalAsync(navegador, cancelamento.Token));
        }
        finally
        {
            servidor.Stop();
        }
    });

    [Fact]
    public Task ConexaoLocalRecusadaProduzFalhaDeNavegacao() => ambiente.ExecutarAsync(async navegador =>
    {
        var servidor = new TcpListener(IPAddress.Loopback, 0);
        servidor.Start();
        var porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
        servidor.Stop();
        var configuracao = new ConfiguracaoAutomacao { UrlPortal = new Uri($"https://127.0.0.1:{porta}/") };
        var navegacao = new NavegacaoCorreiosService(configuracao, NullLogger<NavegacaoCorreiosService>.Instance);
        await Assert.ThrowsAsync<IOException>(() => navegacao.AbrirPortalAsync(navegador, CancellationToken.None));
    });

    [Fact]
    public Task RejeitaRespostaJavascriptForaDoContrato() => ambiente.ExecutarAsync(async navegador =>
    {
        await NavegadorIntegracao.CarregarHtmlAsync(navegador, "<p>Teste de contrato</p>");
        await Assert.ThrowsAsync<InvalidDataException>(() => CriarJavascript().ExecutarAsync<bool>(navegador, "(() => true)()", CancellationToken.None));
        await Assert.ThrowsAsync<JsonException>(() => CriarJavascript().ExecutarAsync<EstadoConsultaPagina>(navegador, "(() => 'conteúdo sem JSON')()", CancellationToken.None));
    });
}
