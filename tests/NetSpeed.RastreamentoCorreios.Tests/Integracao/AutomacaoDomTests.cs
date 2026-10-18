using System.Net;
using NetSpeed.RastreamentoCorreios.Excecoes;
using NetSpeed.RastreamentoCorreios.Interfaces;
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

    [Theory]
    [InlineData("sucesso", 1, 1)]
    [InlineData("captcha-invalido", 2, 2)]
    [InlineData("ocr-invalido", 2, 1)]
    [InlineData("timeout", 2, 1)]
    [InlineData("ocr-atrasado", 2, 1)]
    public Task ConsultaCompletaRecuperaFalhasTemporarias(string cenario, int leiturasEsperadas, int enviosEsperados) => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync(cenario, async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste(async (imagem, token) =>
            {
                Assert.NotEmpty(imagem);
                leituras++;
                if (leituras == 1 && cenario == "ocr-invalido")
                    throw new FalhaReconhecimentoCaptchaException("Imagem de teste ilegível");
                if (leituras == 1 && cenario == "timeout")
                    await Task.Delay(Timeout.Infinite, token);
                if (leituras == 1 && cenario == "ocr-atrasado")
                    await Task.Delay(1100);
                return new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1);
            });
            var resultado = await CriarConsultaLocal(endereco, ocr, 3).ConsultarRastreamentoAsync(navegador,
                "aa123456789br", new Progress<string>(), CancellationToken.None);
            Assert.Equal("AA123456789BR", resultado.CodigoRastreamento);
            Assert.Equal("Evento de teste", Assert.Single(resultado.Eventos).Situacao);
            Assert.Equal(leiturasEsperadas, leituras);
            Assert.Equal(enviosEsperados, await CriarJavascript().ExecutarAsync<int>(navegador,
                "(() => JSON.stringify(window.envios))()", CancellationToken.None));
        }));

    [Theory]
    [InlineData("captcha-esgotado")]
    [InlineData("ocr-invalido")]
    [InlineData("timeout")]
    public Task ConsultaRespeitaLimiteDeTentativas(string cenario) => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync(cenario, async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste(async (_, token) =>
            {
                leituras++;
                if (cenario == "ocr-invalido")
                    throw new FalhaReconhecimentoCaptchaException("Imagem de teste ilegível");
                if (cenario == "timeout")
                    await Task.Delay(Timeout.Infinite, token);
                return new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1);
            });
            var erro = await Assert.ThrowsAsync<ConsultaRastreamentoException>(() => CriarConsultaLocal(endereco, ocr, 2)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), CancellationToken.None));
            Assert.Contains("após 2 tentativas", erro.MensagemUsuario);
            Assert.Equal(2, leituras);
            Assert.Equal(cenario == "captcha-esgotado" ? 2 : 0, await CriarJavascript().ExecutarAsync<int>(navegador,
                "(() => JSON.stringify(window.envios))()", CancellationToken.None));
        }));

    [Theory]
    [InlineData("ocr")]
    [InlineData("renovacao")]
    public Task CancelamentoDoUsuarioImpedeNovasTentativasEEnvios(string etapa) => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync("sucesso", async endereco =>
        {
            using var cancelamento = new CancellationTokenSource();
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste(async (_, token) =>
            {
                leituras++;
                if (etapa == "ocr")
                {
                    cancelamento.CancelAfter(100);
                    await Task.Delay(Timeout.Infinite, token);
                }
                cancelamento.CancelAfter(50);
                throw new FalhaReconhecimentoCaptchaException("Imagem de teste ilegível");
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CriarConsultaLocal(endereco, ocr, 3)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), cancelamento.Token));
            Assert.Equal(1, leituras);
            Assert.Equal(0, await CriarJavascript().ExecutarAsync<int>(navegador,
                "(() => JSON.stringify(window.envios))()", CancellationToken.None));
        }));

    [Theory]
    [InlineData("falha-envio")]
    [InlineData("falha-renovacao")]
    public Task RespostaFalsaDoScriptInterrompeAConsulta(string cenario) => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync(cenario, async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste((_, _) =>
            {
                leituras++;
                if (cenario == "falha-renovacao")
                    throw new FalhaReconhecimentoCaptchaException("Imagem de teste ilegível");
                return Task.FromResult(new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1));
            });
            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => CriarConsultaLocal(endereco, ocr, 3)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), CancellationToken.None));
            Assert.Contains("O script não confirmou", erro.Message);
            Assert.Equal(1, leituras);
        }));

    [Fact]
    public Task RedirecionamentoParaOutroCaminhoImpedeReconhecimento() => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync("redirecionamento", async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste((_, _) =>
            {
                leituras++;
                return Task.FromResult(new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1));
            });
            var erro = await Assert.ThrowsAsync<ConsultaRastreamentoException>(() => CriarConsultaLocal(endereco, ocr, 3)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), CancellationToken.None));
            Assert.Contains("outro endereço", erro.MensagemUsuario);
            Assert.Equal(0, leituras);
        }));

    [Fact]
    public Task FalhaFatalDoMotorNaoEhRepetida() => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync("sucesso", async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste((_, _) =>
            {
                leituras++;
                throw new InvalidOperationException("Falha do motor de teste");
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CriarConsultaLocal(endereco, ocr, 3)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), CancellationToken.None));
            Assert.Equal(1, leituras);
        }));

    private static ConsultaRastreamentoService CriarConsultaLocal(Uri endereco, IProcessamentoCaptchaService ocr, int tentativas)
    {
        var configuracao = new ConfiguracaoAutomacao
        {
            UrlPortal = endereco,
            TimeoutNavegacaoSegundos = 5,
            TimeoutElementoSegundos = 3,
            TimeoutOcrSegundos = 1,
            IntervaloVerificacaoMilissegundos = 50,
            IntervaloTentativasMilissegundos = 100,
            MaximoTentativas = tentativas
        };
        var javascript = new JavascriptCorreiosService(configuracao);
        return new(configuracao, new NavegacaoCorreiosService(configuracao, NullLogger<NavegacaoCorreiosService>.Instance),
            javascript, ocr, new ExtracaoEventosService(javascript), NullLogger<ConsultaRastreamentoService>.Instance);
    }

    private static async Task ComPortalLocalAsync(string cenario, Func<Uri, Task> verificar)
    {
        var imagens = Enumerable.Range(0, 4).Select(indice =>
        {
            using var bitmap = new Bitmap(2, 2);
            bitmap.SetPixel(0, 0, Color.FromArgb(indice * 60, 0, 0));
            using var fluxo = new MemoryStream();
            bitmap.Save(fluxo, System.Drawing.Imaging.ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(fluxo.ToArray());
        }).ToArray();
        var html = $$"""
            <html><body>
            <input id="objeto"><input id="captcha"><img id="captcha_image" src="{{imagens[0]}}">
            <button id="b-pesquisar" type="button">Consultar</button><button id="captcha_refresh_btn" type="button">Renovar</button>
            <div id="alerta"><span class="msg"></span></div><div id="ver-rastro-unico"><ul class="ship-steps"></ul></div>
            <script>
            const imagens = {{JsonSerializer.Serialize(imagens)}};
            const cenario = {{JsonSerializer.Serialize(cenario)}};
            let indice = 0; window.envios = 0;
            function retornarFalso() {
                const serializar = JSON.stringify;
                JSON.stringify = valor => typeof valor === 'boolean' ? 'false' : serializar(valor);
            }
            document.getElementById('captcha_refresh_btn').onclick = () => {
                document.getElementById('captcha_image').src = imagens[++indice];
                document.getElementById('alerta').className = '';
                if (cenario === 'falha-renovacao') retornarFalso();
            };
            document.getElementById('b-pesquisar').onclick = () => {
                window.envios++;
                if (cenario === 'falha-envio') { retornarFalso(); return; }
                if (cenario === 'objeto-nao-encontrado') {
                    document.getElementById('alerta').className = 'aberto';
                    document.querySelector('#alerta .msg').textContent = 'Objeto não encontrado';
                } else if (cenario === 'captcha-esgotado' || (cenario === 'captcha-invalido' && window.envios === 1)) {
                    document.getElementById('alerta').className = 'aberto';
                    document.querySelector('#alerta .msg').textContent = 'CAPTCHA inválido';
                } else {
                    document.querySelector('.ship-steps').innerHTML = "<li class='step'><div class='step-content'><p class='text-head'>Evento de teste</p></div></li>";
                }
            };
            </script></body></html>
            """;
        var conteudo = System.Text.Encoding.UTF8.GetBytes(html);
        var servidor = new TcpListener(IPAddress.Loopback, 0);
        servidor.Start();
        var porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
        using var cancelamento = new CancellationTokenSource();
        async Task ResponderAsync(TcpClient cliente)
        {
            using (cliente)
            try
            {
                await using var fluxo = cliente.GetStream();
                using var leitor = new StreamReader(fluxo, leaveOpen: true);
                var pedido = await leitor.ReadLineAsync(cancelamento.Token);
                while (!string.IsNullOrEmpty(await leitor.ReadLineAsync(cancelamento.Token))) { }
                var redirecionar = cenario == "redirecionamento" && pedido?.Contains(" /app/index.php ") == true;
                var cabecalho = redirecionar
                    ? "HTTP/1.1 302 Found\r\nLocation: /outra-pagina\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {conteudo.Length}\r\nConnection: close\r\n\r\n";
                await fluxo.WriteAsync(System.Text.Encoding.ASCII.GetBytes(cabecalho), cancelamento.Token);
                if (!redirecionar)
                    await fluxo.WriteAsync(conteudo, cancelamento.Token);
            }
            catch (OperationCanceledException) when (cancelamento.IsCancellationRequested) { }
            catch (IOException) when (cancelamento.IsCancellationRequested) { }
        }
        var atendimento = Task.Run(async () =>
        {
            var respostas = new List<Task>();
            try
            {
                while (!cancelamento.IsCancellationRequested)
                {
                    var cliente = await servidor.AcceptTcpClientAsync(cancelamento.Token);
                    respostas.Add(ResponderAsync(cliente));
                }
            }
            catch (OperationCanceledException) when (cancelamento.IsCancellationRequested) { }
            catch (SocketException) when (cancelamento.IsCancellationRequested) { }
            finally
            {
                await Task.WhenAll(respostas);
            }
        });
        try
        {
            await verificar(new Uri($"http://127.0.0.1:{porta}/app/index.php"));
        }
        finally
        {
            cancelamento.Cancel();
            servidor.Stop();
            await atendimento;
        }
    }


    [Fact]
    public Task ConsultaPreservaMensagemDeNegocioSemRepetirOCR() => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync("objeto-nao-encontrado", async endereco =>
        {
            var leituras = 0;
            var ocr = new ProcessamentoCaptchaTeste((_, _) =>
            {
                leituras++;
                return Task.FromResult(new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1));
            });
            var erro = await Assert.ThrowsAsync<ConsultaRastreamentoException>(() => CriarConsultaLocal(endereco, ocr, 3)
                .ConsultarRastreamentoAsync(navegador, "AA123456789BR", new Progress<string>(), CancellationToken.None));
            Assert.Equal("Objeto não encontrado", erro.MensagemUsuario);
            Assert.Equal(1, leituras);
        }));

    [Fact]
    public Task DiagnosticoGravaOsMesmosBytesEnviadosAoOCR() => ambiente.ExecutarAsync(async navegador =>
        await ComPortalLocalAsync("sucesso", async endereco =>
        {
            var diretorio = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(diretorio);
            try
            {
                var ocr = new ProcessamentoCaptchaTeste((imagem, _) =>
                {
                    Assert.Equal(imagem, File.ReadAllBytes(Path.Combine(diretorio, "captcha-1.png")));
                    return Task.FromResult(new ResultadoOcrCaptcha("abcd", 0.9f, "teste", 1));
                });
                await CriarConsultaLocal(endereco, ocr, 3).ConsultarRastreamentoAsync(navegador,
                    "AA123456789BR", new Progress<string>(), CancellationToken.None, diretorio);
                Assert.Single(Directory.GetFiles(diretorio));
            }
            finally
            {
                Directory.Delete(diretorio, true);
            }
        }));

}
