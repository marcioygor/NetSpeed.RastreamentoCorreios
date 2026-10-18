using CefSharp;
using CefSharp.WinForms;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class NavegacaoCorreiosService(ConfiguracaoAutomacao configuracao, ILogger<NavegacaoCorreiosService> logger)
{
    public async Task AbrirPortalAsync(ChromiumWebBrowser navegador, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (navegador.InvokeRequired)
            throw new InvalidOperationException("A navegação deve ser iniciada na thread da interface.");
        if (!navegador.IsBrowserInitialized)
            throw new InvalidOperationException("O navegador ainda não está pronto.");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromSeconds(configuracao.TimeoutNavegacaoSegundos));
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<FrameLoadEndEventArgs> carregado = (_, evento) =>
        {
            if (!evento.Frame.IsMain || !Uri.TryCreate(evento.Url, UriKind.Absolute, out var enderecoCarregado) ||
                enderecoCarregado.GetLeftPart(UriPartial.Authority) != configuracao.UrlPortal.GetLeftPart(UriPartial.Authority))
                return;
            if (evento.HttpStatusCode >= 400)
                conclusao.TrySetException(new IOException($"O portal respondeu com HTTP {evento.HttpStatusCode}."));
            else
                conclusao.TrySetResult();
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
            await conclusao.Task.WaitAsync(limite.Token);
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
}