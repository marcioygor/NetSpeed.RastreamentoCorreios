using CefSharp;
using CefSharp.WinForms;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Excecoes;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class NavegacaoCorreiosService(ConfiguracaoAutomacao configuracao, ILogger<NavegacaoCorreiosService> logger)
{
    public async Task AbrirPortalAsync(ChromiumWebBrowser navegador, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarNavegador(navegador);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromSeconds(configuracao.TimeoutNavegacaoSegundos));
        var conclusao = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<FrameLoadEndEventArgs> carregado = (_, evento) =>
        {
            if (!evento.Frame.IsMain || !Uri.TryCreate(evento.Url, UriKind.Absolute, out var enderecoCarregado) ||
                enderecoCarregado.GetLeftPart(UriPartial.Authority) != configuracao.UrlPortal.GetLeftPart(UriPartial.Authority))
                return;
            conclusao.TrySetResult(evento.HttpStatusCode);
        };
        EventHandler<LoadErrorEventArgs> falhou = (_, evento) =>
        {
            if (evento.Frame.IsMain && evento.ErrorCode != CefErrorCode.Aborted)
                conclusao.TrySetException(new IOException($"Falha de navegação: {evento.ErrorCode}."));
        };

        navegador.FrameLoadEnd += carregado;
        navegador.LoadError += falhou;
        try
        {
            logger.LogInformation("Abrindo portal dos Correios: {Url}", configuracao.UrlPortal);
            navegador.Load(configuracao.UrlPortal.AbsoluteUri);
            var statusHttp = await conclusao.Task.WaitAsync(limite.Token);
            await ValidarRespostaPortalAsync(navegador, statusHttp, limite.Token, cancellationToken);
            logger.LogInformation("Documento principal carregado. A disponibilidade dos resultados ainda precisa ser verificada.");
        }
        catch (OperationCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            navegador.Stop();
            throw new TimeoutException("O portal demorou mais que o limite configurado para carregar.", excecao);
        }
        catch (OperationCanceledException)
        {
            navegador.Stop();
            throw;
        }
        finally
        {
            navegador.FrameLoadEnd -= carregado;
            navegador.LoadError -= falhou;
        }
    }

    private static void ValidarNavegador(ChromiumWebBrowser navegador)
    {
        if (navegador.InvokeRequired)
            throw new InvalidOperationException("A navegação deve ser iniciada na thread da interface.");
        if (!navegador.IsBrowserInitialized)
            throw new InvalidOperationException("O navegador ainda não está pronto.");
    }

    private async Task ValidarRespostaPortalAsync(ChromiumWebBrowser navegador, int statusHttp,
        CancellationToken limite, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (statusHttp == 429)
            throw await CriarErroLimiteHttpAsync(navegador, limite, cancellationToken);

        try
        {
            var resposta = await JavascriptCorreiosService.ExecutarScriptVerificadoAsync(navegador,
                "(() => JSON.stringify(true))()", TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos), limite);
            if (!resposta.Success)
                throw new InvalidOperationException("Não foi possível verificar a página do portal: " + resposta.Message);
        }
        catch (ConsultaRastreamentoException excecao) when (excecao.TipoErro == "limite_requisicoes")
        {
            throw ConsultaRastreamentoException.PorLimiteRequisicoes($"HTTP {statusHttp} em {navegador.Address}.", excecao);
        }
        if (statusHttp >= 400)
            throw new IOException($"O portal respondeu com HTTP {statusHttp}.");
    }

    private async Task<ConsultaRastreamentoException> CriarErroLimiteHttpAsync(ChromiumWebBrowser navegador,
        CancellationToken limite, CancellationToken cancellationToken)
    {
        var contexto = $"HTTP 429 em {navegador.Address}.";
        try
        {
            // A leitura nativa preserva a mensagem original sem executar JavaScript na página bloqueada.
            using var frame = navegador.GetMainFrame();
            var mensagemOriginal = await frame.GetTextAsync().WaitAsync(limite);
            cancellationToken.ThrowIfCancellationRequested();
            return ConsultaRastreamentoException.PorLimiteRequisicoes($"{contexto} Página: {mensagemOriginal}");
        }
        catch (OperationCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            return ConsultaRastreamentoException.PorLimiteRequisicoes(contexto, excecao);
        }
        catch (InvalidOperationException excecao)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ConsultaRastreamentoException.PorLimiteRequisicoes(contexto, excecao);
        }
    }
}
