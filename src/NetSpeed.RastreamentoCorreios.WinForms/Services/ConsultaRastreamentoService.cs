using System.Text.Json;
using CefSharp;
using CefSharp.WinForms;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Excecoes;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Interfaces;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Validacoes;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class ConsultaRastreamentoService(ConfiguracaoAutomacao configuracao, NavegacaoCorreiosService navegacao,
    JavascriptCorreiosService javascript, IProcessamentoCaptchaService processamentoCaptcha, ExtracaoEventosService extracao,
    ILogger<ConsultaRastreamentoService> logger)
{
    private static readonly string ScriptCaptura = RecursosAplicacao.LerTexto("Scripts.CapturarCaptcha.js");
    private static readonly string ScriptPreenchimento = RecursosAplicacao.LerTexto("Scripts.PreencherConsulta.js");
    private static readonly string ScriptEstado = RecursosAplicacao.LerTexto("Scripts.ObterEstadoConsulta.js");

    public async Task<ResultadoRastreamento> ConsultarRastreamentoAsync(ChromiumWebBrowser navegador, string codigo,
        IProgress<string> progresso, CancellationToken cancellationToken, string? diretorioDiagnostico = null)
    {
        try
        {
            return await ExecutarConsultaAsync(navegador, codigo, progresso, cancellationToken, diretorioDiagnostico);
        }
        catch (OperationCanceledException)
        {
            navegador.Stop();
            logger.LogInformation("Execução interrompida por cancelamento; nenhuma nova tentativa será iniciada.");
            throw;
        }
        finally
        {
            logger.LogInformation("Fluxo de consulta finalizado.");
        }
    }

    private async Task<ResultadoRastreamento> ExecutarConsultaAsync(ChromiumWebBrowser navegador, string codigo,
        IProgress<string> progresso, CancellationToken cancellationToken, string? diretorioDiagnostico)
    {
        var codigoNormalizado = ValidadorCodigoRastreamento.ValidarCodigo(codigo);
        logger.LogInformation("Iniciando consulta de {Codigo}.", codigoNormalizado);
        progresso.Report("Abrindo portal");
        await AbrirPortalAsync(navegador, cancellationToken);

        string? origemAnterior = null;

        for (var tentativa = 1; tentativa <= configuracao.MaximoTentativas; tentativa++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progresso.Report($"Lendo CAPTCHA · {tentativa}/{configuracao.MaximoTentativas}");
            var (imagem, conteudo) = await CapturarCaptchaAsync(navegador, origemAnterior,
                tentativa, diretorioDiagnostico, cancellationToken);
            var reconhecimento = await ReconhecerCaptchaAsync(conteudo, tentativa, cancellationToken);

            if (reconhecimento is not null)
            {
                progresso.Report("Consultando objeto");
                var estado = await EnviarConsultaAsync(navegador, codigoNormalizado, imagem.Origem, reconhecimento.Texto,
                    cancellationToken);
                if (estado.Estado == "eventos")
                {
                    logger.LogInformation("CAPTCHA aceito pelo portal na tentativa {Tentativa}.", tentativa);
                    progresso.Report("Extraindo histórico");
                    return await ExtrairResultadoAsync(navegador, codigoNormalizado, cancellationToken);
                }
                if (estado.Estado != "captcha-invalido")
                {
                    logger.LogWarning("O portal respondeu: {Mensagem}", estado.Mensagem);
                    throw new ConsultaRastreamentoException(estado.Mensagem ?? "O portal não retornou um histórico para o objeto informado.");
                }
                logger.LogWarning("CAPTCHA rejeitado pelo portal na tentativa {Tentativa}.", tentativa);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (tentativa < configuracao.MaximoTentativas)
            {
                origemAnterior = imagem.Origem;
                await PrepararProximaTentativaAsync(navegador, imagem.Origem, cancellationToken);
            }
        }
        throw new ConsultaRastreamentoException($"Não foi possível validar o CAPTCHA após {configuracao.MaximoTentativas} tentativas. Tente uma nova consulta.");
    }

    private async Task AbrirPortalAsync(ChromiumWebBrowser navegador, CancellationToken cancellationToken)
    {
        await navegacao.AbrirPortalAsync(navegador, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ValidarEnderecoPortal(navegador);
    }

    private void ValidarEnderecoPortal(ChromiumWebBrowser navegador)
    {
        if (!Uri.TryCreate(navegador.Address, UriKind.Absolute, out var endereco) ||
            !string.IsNullOrEmpty(endereco.UserInfo) ||
            endereco.GetLeftPart(UriPartial.Authority) != configuracao.UrlPortal.GetLeftPart(UriPartial.Authority) ||
            !string.Equals(endereco.AbsolutePath, configuracao.UrlPortal.AbsolutePath, StringComparison.Ordinal))
            throw new ConsultaRastreamentoException("O portal redirecionou a navegação para outro endereço. A consulta foi interrompida.");
    }

    private async Task<(ImagemCaptcha Imagem, byte[] Conteudo)> CapturarCaptchaAsync(ChromiumWebBrowser navegador,
        string? origemAnterior, int tentativa, string? diretorioDiagnostico, CancellationToken cancellationToken)
    {
        ValidarEnderecoPortal(navegador);
        var script = ScriptCaptura.Replace("{{origemAnterior}}", JsonSerializer.Serialize(origemAnterior));
        var imagem = await javascript.AguardarAsync<ImagemCaptcha>(navegador, script, cancellationToken);
        var conteudo = Convert.FromBase64String(imagem.ConteudoBase64);
        if (diretorioDiagnostico is not null)
            await File.WriteAllBytesAsync(Path.Combine(diretorioDiagnostico, $"captcha-{tentativa}.png"), conteudo, cancellationToken);
        logger.LogInformation("Tentativa {Tentativa}/{Limite} do CAPTCHA.", tentativa, configuracao.MaximoTentativas);
        return (imagem, conteudo);
    }

    private async Task<ResultadoOcrCaptcha?> ReconhecerCaptchaAsync(byte[] conteudo, int tentativa, CancellationToken cancellationToken)
    {
        using var limiteOcr = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limiteOcr.CancelAfter(TimeSpan.FromSeconds(configuracao.TimeoutOcrSegundos));
        try
        {
            var reconhecimento = await processamentoCaptcha.ReconhecerAsync(conteudo, limiteOcr.Token);
            cancellationToken.ThrowIfCancellationRequested();
            limiteOcr.Token.ThrowIfCancellationRequested();
            logger.LogInformation("OCR selecionou '{Texto}' por {Concordancias} concordâncias. Isso ainda depende da validação do portal.", reconhecimento.Texto, reconhecimento.Concordancias);
            return reconhecimento;
        }
        catch (OperationCanceledException excecao) when (limiteOcr.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(excecao, "O processamento do CAPTCHA excedeu o tempo configurado na tentativa {Tentativa}/{Limite}; nenhuma consulta foi enviada nesta tentativa.",
                tentativa, configuracao.MaximoTentativas);
        }
        catch (FalhaReconhecimentoCaptchaException excecao)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(excecao, "A imagem não produziu uma sequência válida; nenhuma consulta foi enviada nesta tentativa.");
        }
        return null;
    }

    private async Task<EstadoConsultaPagina> EnviarConsultaAsync(ChromiumWebBrowser navegador, string codigo,
        string origemImagem, string captcha, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarEnderecoPortal(navegador);
        var script = ScriptPreenchimento.Replace("{{origemImagem}}", JsonSerializer.Serialize(origemImagem))
            .Replace("{{codigo}}", JsonSerializer.Serialize(codigo))
            .Replace("{{captcha}}", JsonSerializer.Serialize(captcha));
        if (!await javascript.ExecutarAsync<bool>(navegador, script, cancellationToken))
            throw new InvalidOperationException("O script não confirmou o envio da consulta ao portal.");
        return await javascript.AguardarAsync<EstadoConsultaPagina>(navegador, ScriptEstado, cancellationToken);
    }

    private async Task<ResultadoRastreamento> ExtrairResultadoAsync(ChromiumWebBrowser navegador, string codigo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarEnderecoPortal(navegador);
        var eventos = await extracao.ExtrairEventosAsync(navegador, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Consulta de {Codigo} concluída: {Quantidade} eventos extraídos.", codigo, eventos.Count);
        return new ResultadoRastreamento { CodigoRastreamento = codigo, Eventos = eventos };
    }

    private async Task PrepararProximaTentativaAsync(ChromiumWebBrowser navegador, string origem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarEnderecoPortal(navegador);
        var script = """
            (() => {
                const imagem = document.querySelector('#captcha_image');
                const renovar = document.querySelector('#captcha_refresh_btn');
                if (!imagem || !renovar) throw new Error('Os controles do CAPTCHA não foram encontrados.');
                if (imagem.currentSrc === {{origem}}) renovar.click();
                return JSON.stringify(true);
            })()
            """.Replace("{{origem}}", JsonSerializer.Serialize(origem));
        if (!await javascript.ExecutarAsync<bool>(navegador, script, cancellationToken))
            throw new InvalidOperationException("O script não confirmou a renovação do CAPTCHA.");
        await Task.Delay(configuracao.IntervaloTentativasMilissegundos, cancellationToken);
    }
}
