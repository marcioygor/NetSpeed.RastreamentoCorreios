using System.Text.Json;
using CefSharp;
using CefSharp.WinForms;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class JavascriptCorreiosService(ConfiguracaoAutomacao configuracao)
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public async Task<T?> ExecutarAsync<T>(ChromiumWebBrowser navegador, string script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resposta = await navegador.EvaluateScriptAsPromiseAsync("return " + script,
            TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos)).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!resposta.Success)
            throw new InvalidOperationException("Não foi possível executar o JavaScript de automação: " + resposta.Message);
        if (resposta.Result is null)
            return default;
        if (resposta.Result is not string json)
            throw new InvalidDataException("O JavaScript não retornou JSON no formato esperado.");
        return JsonSerializer.Deserialize<T>(json, Opcoes);
    }

    public async Task<T> AguardarAsync<T>(ChromiumWebBrowser navegador, string script, CancellationToken cancellationToken) where T : class
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos));
        try
        {
            while (true)
            {
                limite.Token.ThrowIfCancellationRequested();
                if (navegador.CanExecuteJavascriptInMainFrame && !navegador.IsLoading)
                {
                    var resultado = await ExecutarAsync<T>(navegador, script, limite.Token);
                    if (resultado is not null)
                        return resultado;
                }
                await Task.Delay(configuracao.IntervaloVerificacaoMilissegundos, limite.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("O portal não apresentou o estado esperado dentro do prazo configurado.");
        }
    }
}
