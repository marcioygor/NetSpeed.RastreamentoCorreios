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
        await navegacao.AbrirPortalAsync(navegador, cancellationToken);
        if (!Uri.TryCreate(navegador.Address, UriKind.Absolute, out var endereco) || endereco.GetLeftPart(UriPartial.Authority) != configuracao.UrlPortal.GetLeftPart(UriPartial.Authority))
            throw new ConsultaRastreamentoException("O portal redirecionou a navegação para outro endereço. A consulta foi interrompida.");

        string? origemAnterior = null;
        for (var tentativa = 1; tentativa <= configuracao.MaximoTentativas; tentativa++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progresso.Report($"Lendo CAPTCHA · {tentativa}/{configuracao.MaximoTentativas}");
            var scriptCaptura = RecursosAplicacao.LerTexto("Scripts.CapturarCaptcha.js")
                .Replace("{{origemAnterior}}", JsonSerializer.Serialize(origemAnterior));
            var imagem = await javascript.AguardarAsync<ImagemCaptcha>(navegador, scriptCaptura, cancellationToken);
            if (diretorioDiagnostico is not null)
                await File.WriteAllBytesAsync(Path.Combine(diretorioDiagnostico, $"captcha-{tentativa}.png"), Convert.FromBase64String(imagem.ConteudoBase64), cancellationToken);
            logger.LogInformation("Tentativa {Tentativa}/{Limite} do CAPTCHA.", tentativa, configuracao.MaximoTentativas);
            ResultadoOcrCaptcha? reconhecimento = null;
            try
            {
                using var limiteOcr = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limiteOcr.CancelAfter(TimeSpan.FromSeconds(configuracao.TimeoutOcrSegundos));
                try
                {
                    reconhecimento = await processamentoCaptcha.ReconhecerAsync(Convert.FromBase64String(imagem.ConteudoBase64), limiteOcr.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("O processamento do CAPTCHA excedeu o tempo configurado.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation("OCR selecionou '{Texto}' por {Concordancias} concordâncias. Isso ainda depende da validação do portal.", reconhecimento.Texto, reconhecimento.Concordancias);
            }
            catch (FalhaReconhecimentoCaptchaException excecao)
            {
                logger.LogWarning(excecao, "A imagem não produziu uma sequência válida; nenhuma consulta foi enviada nesta tentativa.");
            }

            if (reconhecimento is not null)
            {
                progresso.Report("Consultando objeto");
                var preencher = RecursosAplicacao.LerTexto("Scripts.PreencherConsulta.js")
                    .Replace("{{origemImagem}}", JsonSerializer.Serialize(imagem.Origem))
                    .Replace("{{codigo}}", JsonSerializer.Serialize(codigoNormalizado))
                    .Replace("{{captcha}}", JsonSerializer.Serialize(reconhecimento.Texto));
                await javascript.ExecutarAsync<bool>(navegador, preencher, cancellationToken);
                var estado = await javascript.AguardarAsync<EstadoConsultaPagina>(navegador,
                    RecursosAplicacao.LerTexto("Scripts.ObterEstadoConsulta.js"), cancellationToken);
                if (estado.Estado == "eventos")
                {
                    logger.LogInformation("CAPTCHA aceito pelo portal na tentativa {Tentativa}.", tentativa);
                    progresso.Report("Extraindo histórico");
                    cancellationToken.ThrowIfCancellationRequested();
                    var eventos = await extracao.ExtrairEventosAsync(navegador, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    logger.LogInformation("Consulta de {Codigo} concluída: {Quantidade} eventos extraídos.", codigoNormalizado, eventos.Count);
                    return new ResultadoRastreamento { CodigoRastreamento = codigoNormalizado, Eventos = eventos };
                }
                if (estado.Estado != "captcha-invalido")
                {
                    logger.LogWarning("O portal respondeu: {Mensagem}", estado.Mensagem);
                    throw new ConsultaRastreamentoException(estado.Mensagem ?? "O portal não retornou um histórico para o objeto informado.");
                }
                logger.LogWarning("CAPTCHA rejeitado pelo portal na tentativa {Tentativa}.", tentativa);
            }
            if (tentativa < configuracao.MaximoTentativas)
            {
                origemAnterior = imagem.Origem;
                await RenovarImagemAsync(navegador, imagem.Origem, cancellationToken);
                await Task.Delay(configuracao.IntervaloTentativasMilissegundos, cancellationToken);
            }
        }
        throw new ConsultaRastreamentoException($"Não foi possível validar o CAPTCHA após {configuracao.MaximoTentativas} tentativas. Tente uma nova consulta.");
    }

    private async Task RenovarImagemAsync(ChromiumWebBrowser navegador, string origem, CancellationToken cancellationToken)
    {
        var script = """
            (() => {
                const imagem = document.querySelector('#captcha_image');
                const renovar = document.querySelector('#captcha_refresh_btn');
                if (!imagem || !renovar) throw new Error('Os controles do CAPTCHA não foram encontrados.');
                if (imagem.currentSrc === {{origem}}) renovar.click();
                return JSON.stringify(true);
            })()
            """.Replace("{{origem}}", JsonSerializer.Serialize(origem));
        await javascript.ExecutarAsync<bool>(navegador, script, cancellationToken);
    }
}
