using CefSharp;
using CefSharp.WinForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Formularios;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Interfaces;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;

namespace NetSpeed.RastreamentoCorreios;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var diretorioDados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeed", "RastreamentoCorreios");
        using var registro = new RegistroExecucaoProvider(Path.Combine(diretorioDados, "logs"));
        var inicializado = false;
        try
        {
            Directory.CreateDirectory(diretorioDados);
            Directory.CreateDirectory(Path.Combine(diretorioDados, "logs"));
            var configuracao = ConfiguracaoAutomacao.Carregar(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            if (args.Length == 2 && args[0] is "--ocr" or "--ocr-onnx")
                return ExecutarOcrArquivoAsync(args[1], configuracao, registro).GetAwaiter().GetResult();
            if (args.Length == 1 && args[0] == "--verificar-designer")
            {
                using var visualizacao = new FormularioPrincipal();
                if (visualizacao.Controls.Count == 0)
                    throw new InvalidOperationException("A prévia do formulário não foi criada.");
                Console.WriteLine("Prévia do formulário criada sem inicializar o Chromium.");
                return 0;
            }
            CefSharpSettings.ShutdownOnExit = false;
            Cef.EnableWaitForBrowsersToClose();
            var diretorioPerfil = Path.Combine(diretorioDados, "chromium", $"sessao-{Environment.ProcessId}");
            var caminhoSubprocesso = Path.Combine(AppContext.BaseDirectory, "CefSharp.BrowserSubprocess.exe");
            var loggerInicializacao = registro.CreateLogger("Inicializacao");
            loggerInicializacao.LogInformation("Inicializando Chromium. PID {Processo}; base {Base}; perfil {Perfil}; depurador {Depurador}; x64 {X64}.",
                Environment.ProcessId, AppContext.BaseDirectory, diretorioPerfil, System.Diagnostics.Debugger.IsAttached, Environment.Is64BitProcess);
            if (!File.Exists(caminhoSubprocesso))
                throw new FileNotFoundException("O subprocesso do CefSharp não foi encontrado. Restaure as dependências e recompile o projeto x64.", caminhoSubprocesso);
            using var settings = new CefSettings
            {
                RootCachePath = diretorioPerfil,
                CachePath = Path.Combine(diretorioPerfil, "perfil"),
                BrowserSubprocessPath = caminhoSubprocesso,
                LogFile = Path.Combine(diretorioDados, "logs", $"chromium-{Environment.ProcessId}.log"),
                Locale = "pt-BR"
            };
            inicializado = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
            if (!inicializado)
                throw new InvalidOperationException($"O Chromium não pôde ser inicializado. Código: {Cef.GetExitCode()}.");

            var servicos = new ServiceCollection();
            servicos.AddSingleton(configuracao);
            servicos.AddSingleton(registro);
            servicos.AddLogging(logging => logging.AddProvider(registro));
            servicos.AddSingleton<JavascriptCorreiosService>();
            servicos.AddSingleton<ConsultaRastreamentoService>();
            servicos.AddSingleton<ConsultaLoteService>();
            servicos.AddSingleton<ExtracaoEventosService>();
            servicos.AddSingleton<PreProcessamentoImagemService>();
            servicos.AddSingleton<IProcessamentoCaptchaService, ProcessamentoCaptchaService>();
            servicos.AddSingleton<NavegacaoCorreiosService>();
            servicos.AddSingleton<ExportacaoJsonService>();
            servicos.AddSingleton<DiagnosticoNavegadorService>();
            servicos.AddTransient<FormularioPrincipal>();
            using var provedor = servicos.BuildServiceProvider();
            using var formulario = provedor.GetRequiredService<FormularioPrincipal>();
            if (args.Length == 1 && args[0] is "--verificar-inicializacao" or "--inspecionar-portal")
                formulario.HabilitarVerificacaoInicializacao(Path.Combine(AppContext.BaseDirectory, "verificacao-inicializacao.json"), args[0] == "--inspecionar-portal");
            else if (args.Length == 2 && args[0] == "--verificar-extracao")
                formulario.HabilitarTesteExtracao(args[1]);
            else if (args.Length == 1 && args[0] == "--verificar-cancelamento")
                formulario.HabilitarVerificacaoCancelamento();
            else if (args.Length == 2 && args[0] == "--consultar")
                formulario.HabilitarConsultaDiagnostico(args[1]);
            else if (args.Length == 1)
                formulario.DefinirCodigoInicial(args[0]);
            Application.Run(formulario);
            return Environment.ExitCode;
        }
        catch (Exception excecao)
        {
            registro.CreateLogger(nameof(Program)).LogCritical(excecao, "Falha ao iniciar a aplicação.");
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Não foi possível concluir a execução. Consulte os logs da aplicação.");
                return 1;
            }
            MessageBox.Show("Não foi possível iniciar a aplicação. Tente novamente e, se o problema persistir, consulte os logs.\n\nLogs: " + Path.Combine(diretorioDados, "logs"),
                "Falha na inicialização", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            if (inicializado)
            {
                Cef.WaitForBrowsersToClose();
                Cef.Shutdown();
            }
        }
    }
    private static async Task<int> ExecutarOcrArquivoAsync(string caminho, ConfiguracaoAutomacao configuracao,
        RegistroExecucaoProvider registro)
    {
        using var logging = LoggerFactory.Create(construtor => construtor.AddProvider(registro));
        using var servico = new ProcessamentoCaptchaService(configuracao, logging.CreateLogger<ProcessamentoCaptchaService>());
        var imagem = await File.ReadAllBytesAsync(caminho);
        var diretorioVariantes = caminho + ".variantes";
        Directory.CreateDirectory(diretorioVariantes);
        foreach (var variante in new PreProcessamentoImagemService().PrepararVariantes(imagem))
            await File.WriteAllBytesAsync(Path.Combine(diretorioVariantes, variante.Nome + ".png"), variante.Conteudo);
        var resultado = await servico.ReconhecerAsync(imagem, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(resultado, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(caminho + ".ocr.json", json);
        Console.WriteLine(json);
        return 0;
    }
}
