using System.Text.Json;
using CefSharp;
using Microsoft.Extensions.Logging;

namespace NetSpeed.RastreamentoCorreios.Formularios;

public partial class FormularioPrincipal
{
    private async Task ExecutarConsultaDiagnosticoAsync()
    {
        var diretorio = Path.Combine(AppContext.BaseDirectory, "diagnosticos", "consulta-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        using var cancelamento = new CancellationTokenSource();
        operacaoAtual = cancelamento;
        DefinirProcessando(true);
        try
        {
            Directory.CreateDirectory(diretorio);
            var progressoConsulta = new Progress<string>(texto => DefinirStatus(texto, Color.FromArgb(0, 123, 174)));
            var resultado = await consulta.ConsultarRastreamentoAsync(navegador, codigoConsultaDiagnostico!, progressoConsulta,
                cancelamento.Token, diretorio);
            await File.WriteAllTextAsync(Path.Combine(diretorio, "resultado.json"), exportacao.Serializar(resultado));
            logger.LogInformation("Diagnóstico da consulta concluído. Evidências em {Diretorio}.", diretorio);
        }
        catch (Exception excecao)
        {
            // Fronteira do diagnóstico: uma execução malsucedida precisa encerrar com erro e guardar evidências.
            Environment.ExitCode = 1;
            logger.LogError(excecao, "Diagnóstico da consulta falhou. Evidências em {Diretorio}.", diretorio);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(diretorio, "falha.json"), JsonSerializer.Serialize(new
                {
                    tipo = excecao.GetType().Name, mensagem = excecao.Message
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception falha) when (falha is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(falha, "Não foi possível salvar a evidência da falha do diagnóstico.");
            }
        }
        finally
        {
            try
            {
                if (navegador.IsBrowserInitialized)
                {
                    using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(configuracao.TimeoutElementoSegundos));
                    var html = await navegador.GetSourceAsync().WaitAsync(limite.Token);
                    await File.WriteAllTextAsync(Path.Combine(diretorio, "pagina-final.html"), html, limite.Token);
                }
            }
            catch (Exception falha)
            {
                // A coleta de evidências não pode impedir o encerramento do diagnóstico.
                Environment.ExitCode = 1;
                logger.LogWarning(falha, "Não foi possível salvar o HTML final do diagnóstico.");
            }
            finally
            {
                operacaoAtual = null;
                Close();
            }
        }
    }
    private async Task VerificarCancelamentoAsync()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "verificacao-cancelamento.json");
        using var cancelamento = new CancellationTokenSource();
        using var relogio = new System.Windows.Forms.Timer { Interval = 50 };
        var batimentos = 0;
        var botaoAcionado = false;
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        operacaoAtual = cancelamento;
        DefinirProcessando(true);
        DefinirStatus("Verificando cancelamento", Color.FromArgb(0, 123, 174));
        relogio.Tick += (_, _) =>
        {
            batimentos++;
            if (batimentos < 4)
                return;
            relogio.Stop();
            botaoAcionado = botaoCancelar.Enabled;
            if (botaoAcionado)
                botaoCancelar.PerformClick();
        };
        relogio.Start();
        try
        {
            await diagnostico.VerificarEsperaCancelavelAsync(navegador, cancelamento.Token)
                .WaitAsync(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("A espera terminou antes do cancelamento.");
        }
        catch (OperationCanceledException) when (botaoAcionado && cancelamento.IsCancellationRequested && batimentos >= 4)
        {
            cronometro.Stop();
            DefinirStatus("Cancelado", Color.SlateGray);
            await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(new
            {
                interfaceRespondeu = true,
                botaoCancelou = true,
                batimentos,
                duracaoMilissegundos = cronometro.ElapsedMilliseconds
            }, new JsonSerializerOptions { WriteIndented = true }));
            logger.LogInformation("Cancelamento local validado pelo botão após {Batimentos} batimentos da interface.", batimentos);
        }
        catch (Exception excecao)
        {
            Environment.ExitCode = 1;
            logger.LogError(excecao, "Falha no diagnóstico local de cancelamento.");
            await File.WriteAllTextAsync(caminho, JsonSerializer.Serialize(new
            {
                interfaceRespondeu = batimentos >= 4,
                botaoCancelou = botaoAcionado && cancelamento.IsCancellationRequested,
                batimentos,
                erro = excecao.Message
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            relogio.Stop();
            cancelamento.Cancel();
            operacaoAtual = null;
            DefinirProcessando(false);
            Close();
        }
    }

    private async Task ExecutarTesteExtracaoAsync()
    {
        try
        {
            await diagnostico.TestarExtracaoAsync(navegador, caminhoTesteExtracao!);
        }
        catch (Exception excecao)
        {
            Environment.ExitCode = 1;
            logger.LogError(excecao, "Falha no teste local de extração.");
            var arquivo = Path.Combine(AppContext.BaseDirectory, "extracao-" + Path.GetFileNameWithoutExtension(caminhoTesteExtracao) + ".falha.json");
            await File.WriteAllTextAsync(arquivo, JsonSerializer.Serialize(new { erro = excecao.Message }));
        }
        finally
        {
            Close();
        }
    }
}
