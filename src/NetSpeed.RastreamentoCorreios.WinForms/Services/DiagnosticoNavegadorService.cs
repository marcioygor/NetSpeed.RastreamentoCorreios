using System.Runtime.InteropServices;
using System.Text.Json;
using CefSharp;
using CefSharp.WinForms;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class DiagnosticoNavegadorService(ConfiguracaoAutomacao configuracao,
    NavegacaoCorreiosService navegacao, JavascriptCorreiosService javascript, ExtracaoEventosService extracao,
    ILogger<DiagnosticoNavegadorService> logger)
{
    public async Task VerificarInicializacaoAsync(ChromiumWebBrowser navegador, string caminho, bool inspecionarPortal)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos));
        var resposta = await AguardarScriptAsync(navegador,
            "document.querySelector('h1')?.textContent", limite.Token);
        if (resposta.Result is not string titulo || !titulo.Contains("rastreamento", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O HTML de inicialização não foi renderizado como esperado.");

        await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(new
        {
            processo = Environment.ProcessId,
            depuradorAnexado = System.Diagnostics.Debugger.IsAttached,
            raizPerfil = Cef.GetGlobalRequestContext().CachePath,
            inicializado = navegador.IsBrowserInitialized,
            javascriptExecutado = resposta.Success,
            titulo,
            cefSharp = Cef.CefSharpVersion,
            chromium = Cef.ChromiumVersion,
            runtime = RuntimeInformation.FrameworkDescription
        }, new JsonSerializerOptions { WriteIndented = true }), limite.Token);
        logger.LogInformation("Verificação concluída: Chromium renderizou HTML e executou JavaScript.");

        if (inspecionarPortal)
            await InspecionarPortalAsync(navegador, Path.GetDirectoryName(caminho)!);
    }

    public async Task TestarExtracaoAsync(ChromiumWebBrowser navegador, string caminhoHtml)
    {
        navegador.LoadHtml(await File.ReadAllTextAsync(caminhoHtml));
        await javascript.AguardarAsync<string>(navegador,
            "(() => document.querySelector('.ship-steps') ? JSON.stringify('pronto') : null)()", CancellationToken.None);
        var eventos = await extracao.ExtrairEventosAsync(navegador, CancellationToken.None);
        var caminho = Path.Combine(AppContext.BaseDirectory, "extracao-" + Path.GetFileNameWithoutExtension(caminhoHtml) + ".json");
        await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(new { fonte = "fixture local", eventos },
            new JsonSerializerOptions { WriteIndented = true }));
        logger.LogInformation("Teste de extração local concluído: {Quantidade} eventos. Saída: {Arquivo}.", eventos.Count, caminho);
    }
    public async Task VerificarEsperaCancelavelAsync(ChromiumWebBrowser navegador, CancellationToken cancellationToken)
    {
        await javascript.AguardarAsync<string>(navegador, "(() => null)()", cancellationToken);
        throw new InvalidOperationException("A espera de diagnóstico retornou sem cancelamento.");
    }

    private async Task InspecionarPortalAsync(ChromiumWebBrowser navegador, string diretorio)
    {
        await navegacao.AbrirPortalAsync(navegador, CancellationToken.None);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos));
        var resposta = await AguardarScriptAsync(navegador, """
            document.readyState === 'complete' && document.querySelector('input') ? JSON.stringify({
                url: location.href,
                titulo: document.title,
                campos: Array.from(document.querySelectorAll('input')).map(elemento => ({
                    id: elemento.id, nome: elemento.name, tipo: elemento.type, placeholder: elemento.placeholder
                })),
                botoes: Array.from(document.querySelectorAll('button')).map(elemento => ({
                    id: elemento.id, tipo: elemento.type, texto: elemento.textContent.trim()
                })),
                scripts: Array.from(document.scripts).map(elemento => elemento.src).filter(Boolean)
            }, null, 2) : null
            """, limite.Token);
        await File.WriteAllTextAsync(Path.Combine(diretorio, "inspecao-portal.json"), (string)resposta.Result, limite.Token);
        var html = await navegador.GetSourceAsync().WaitAsync(limite.Token);
        await File.WriteAllTextAsync(Path.Combine(diretorio, "inspecao-portal.html"), html, limite.Token);
        var imagem = await AguardarScriptAsync(navegador, """
            (() => {
                const imagem = document.querySelector('#captcha_image');
                if (!imagem?.complete || !imagem.naturalWidth) return null;
                const canvas = document.createElement('canvas');
                canvas.width = imagem.naturalWidth; canvas.height = imagem.naturalHeight;
                canvas.getContext('2d').drawImage(imagem, 0, 0);
                return canvas.toDataURL('image/png').split(',')[1];
            })()
            """, limite.Token);
        await File.WriteAllBytesAsync(Path.Combine(diretorio, "captcha-investigacao.png"), Convert.FromBase64String((string)imagem.Result), limite.Token);
        var scriptPortal = await navegador.EvaluateScriptAsPromiseAsync("return fetch('/app/index.js?v2.103').then(r => { if (!r.ok) throw new Error('HTTP ' + r.status); return r.text(); });",
            TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos)).WaitAsync(limite.Token);
        if (!scriptPortal.Success || scriptPortal.Result is not string fonte)
            throw new InvalidOperationException("Não foi possível ler o JavaScript público do portal: " + scriptPortal.Message);
        await File.WriteAllTextAsync(Path.Combine(diretorio, "portal-index.js"), fonte, limite.Token);
        foreach (var recurso in new[] { "/core/js/rastroUnico.js", "/core/templates/bunker/js/forms.js", "/core/templates/bunker/js/alerta.js" })
        {
            var respostaRecurso = await navegador.EvaluateScriptAsPromiseAsync("return fetch(" + JsonSerializer.Serialize(recurso) + ").then(r => r.text());", TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos)).WaitAsync(limite.Token);
            if (respostaRecurso.Success && respostaRecurso.Result is string codigoRecurso)
                await File.WriteAllTextAsync(Path.Combine(diretorio, "portal-" + Path.GetFileName(recurso)), codigoRecurso, limite.Token);
        }
        logger.LogInformation("HTML, JavaScript público e imagem do CAPTCHA registrados para investigação. Nenhuma consulta foi enviada.");
    }

    private async Task<JavascriptResponse> AguardarScriptAsync(ChromiumWebBrowser navegador, string script,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (navegador.CanExecuteJavascriptInMainFrame && !navegador.IsLoading)
            {
                var resposta = await navegador.EvaluateScriptAsync(script).WaitAsync(cancellationToken);
                if (!resposta.Success)
                    throw new InvalidOperationException($"Falha no JavaScript de diagnóstico: {resposta.Message}");
                if (resposta.Result is not null)
                    return resposta;
            }
            await Task.Delay(configuracao.IntervaloVerificacaoMilissegundos, cancellationToken);
        }
    }
}