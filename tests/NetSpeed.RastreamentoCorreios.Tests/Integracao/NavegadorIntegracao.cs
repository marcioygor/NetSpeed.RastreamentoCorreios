using CefSharp;
using CefSharp.WinForms;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests.Integracao;

public sealed class NavegadorIntegracao : IAsyncLifetime
{
    private readonly TaskCompletionSource pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource encerrado = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Form formulario = null!;
    private ChromiumWebBrowser navegador = null!;

    public async Task InitializeAsync()
    {
        var thread = new Thread(ExecutarJanela) { IsBackground = true, Name = "Chromium de integração" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await pronto.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private void ExecutarJanela()
    {
        var inicializado = false;
        try
        {
            CefSharpSettings.ShutdownOnExit = false;
            Cef.EnableWaitForBrowsersToClose();
            var perfil = Path.Combine(Path.GetTempPath(), "NetSpeed.Integracao", Guid.NewGuid().ToString("N"));
            using var configuracao = new CefSettings
            {
                RootCachePath = perfil,
                CachePath = Path.Combine(perfil, "cache"),
                BrowserSubprocessPath = Path.Combine(AppContext.BaseDirectory, "CefSharp.BrowserSubprocess.exe"),
                LogFile = Path.Combine(perfil, "chromium.log")
            };
            inicializado = Cef.Initialize(configuracao);
            if (!inicializado)
                throw new InvalidOperationException("O Chromium de integração não inicializou.");
            using (formulario = new Form { ShowInTaskbar = false, Opacity = 0, Width = 640, Height = 480 })
            {
                navegador = new ChromiumWebBrowser("about:blank") { Dock = DockStyle.Fill };
                navegador.IsBrowserInitializedChanged += (_, _) =>
                {
                    if (navegador.IsBrowserInitialized)
                        pronto.TrySetResult();
                };
                formulario.Controls.Add(navegador);
                Application.Run(formulario);
            }
        }
        catch (Exception excecao)
        {
            pronto.TrySetException(excecao);
            encerrado.TrySetException(excecao);
        }
        finally
        {
            if (inicializado)
            {
                Cef.WaitForBrowsersToClose();
                Cef.Shutdown();
            }
            encerrado.TrySetResult();
        }
    }

    public async Task ExecutarAsync(Func<ChromiumWebBrowser, Task> verificacao)
    {
        await pronto.Task;
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        formulario.BeginInvoke((Action)(async () =>
        {
            try
            {
                await verificacao(navegador);
                conclusao.TrySetResult();
            }
            catch (Exception excecao)
            {
                conclusao.TrySetException(excecao);
            }
        }));
        await conclusao.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public static async Task CarregarHtmlAsync(ChromiumWebBrowser navegador, string html)
    {
        var marcador = "fixture-" + Guid.NewGuid().ToString("N");
        if (!html.Contains("charset", StringComparison.OrdinalIgnoreCase))
            html = "<!doctype html><html><head><meta charset='utf-8'></head><body>" + html + "</body></html>";
        var carregado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<FrameLoadEndEventArgs> aoCarregar = (_, evento) =>
        {
            if (evento.Frame.IsMain && evento.Url.Contains(marcador, StringComparison.Ordinal))
                carregado.TrySetResult();
        };
        navegador.FrameLoadEnd += aoCarregar;
        try
        {
            navegador.LoadHtml(html + $"<div id='{marcador}' hidden></div>");
            await carregado.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // A preparação deve aceitar páginas que a automação precisa rejeitar.
            using var frame = navegador.GetMainFrame();
            var resposta = await frame.EvaluateScriptAsPromiseAsync(
                $"return document.getElementById('{marcador}') ? 'pronto' : null").WaitAsync(TimeSpan.FromSeconds(5));
            if (!resposta.Success || resposta.Result is not "pronto")
                throw new InvalidOperationException("A fixture HTML não terminou de carregar.");
        }
        finally
        {
            navegador.FrameLoadEnd -= aoCarregar;
        }
    }

    public async Task DisposeAsync()
    {
        if (pronto.Task.IsCompletedSuccessfully && !formulario.IsDisposed)
            formulario.BeginInvoke((Action)formulario.Close);
        await encerrado.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
