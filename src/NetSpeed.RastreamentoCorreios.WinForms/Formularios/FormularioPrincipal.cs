using System.Runtime.InteropServices;
using System.Text.Json;
using CefSharp;
using NetSpeed.RastreamentoCorreios.Excecoes;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Services;
using NetSpeed.RastreamentoCorreios.Validacoes;

namespace NetSpeed.RastreamentoCorreios.Formularios;

public partial class FormularioPrincipal : Form
{
    private readonly RegistroExecucaoProvider registro = null!;
    private readonly ConsultaRastreamentoService consulta = null!;
    private readonly ConsultaLoteService consultaLote = null!;
    private readonly ConfiguracaoAutomacao configuracao = null!;
    private readonly ExportacaoJsonService exportacao = null!;
    private readonly DiagnosticoNavegadorService diagnostico = null!;
    private readonly ILogger<FormularioPrincipal> logger = null!;
    private CancellationTokenSource? operacaoAtual;
    private ResultadoRastreamento? resultadoAtual = null;
    private bool fechando;
    private bool consultaEmAndamento;
    private bool NavegadorDisponivel => navegador is { IsDisposed: false, IsBrowserInitialized: true };
    private string? caminhoVerificacao;
    private bool inspecionarPortal;
    private string? codigoConsultaDiagnostico;
    private string? caminhoTesteExtracao;
    private bool verificarCancelamento;
    private readonly TaskCompletionSource navegadorPronto = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Usado apenas pelo Windows Forms Designer. O navegador requer Cef.Initialize em execução.
    public FormularioPrincipal()
    {
        InitializeComponent();
    }

    public FormularioPrincipal(RegistroExecucaoProvider registro, ConsultaRastreamentoService consulta,
        ExportacaoJsonService exportacao, DiagnosticoNavegadorService diagnostico, ConfiguracaoAutomacao configuracao,
        ConsultaLoteService consultaLote, ILogger<FormularioPrincipal> logger)
    {
        this.registro = registro;
        this.consulta = consulta;
        this.consultaLote = consultaLote;
        this.configuracao = configuracao;
        this.exportacao = exportacao;
        this.diagnostico = diagnostico;
        this.logger = logger;
        InitializeComponent();
        InicializarNavegador();
        ConectarEventos();
        navegador.IsBrowserInitializedChanged += (_, _) =>
        {
            if (navegador.IsBrowserInitialized)
                navegadorPronto.TrySetResult();
            AtualizarInterface(AtualizarBotoes);
        };
        if (navegador.IsBrowserInitialized)
            navegadorPronto.TrySetResult();
        navegador.AddressChanged += (_, evento) => AtualizarInterface(() =>
            enderecoAtual.Text = evento.Address.StartsWith("data:", StringComparison.Ordinal) ? "NAVEGADOR  ·  Página inicial" : "NAVEGADOR  ·  " + evento.Address);
    }

    private void ConectarEventos()
    {
        botaoAbrirPortal.Click += AoAbrirPortalAsync;
        botaoCancelar.Click += (_, _) => operacaoAtual?.Cancel();
        campoCodigo.TextChanged += (_, _) => AtualizarBotoes();
        botaoCopiar.Click += AoCopiarJson;
        botaoExportar.Click += AoExportarJsonAsync;
        listaObjetos.SelectedIndexChanged += AoSelecionarObjeto;
        listaObjetos.SizeChanged += (_, _) => AjustarColunasObjetos();
        listaEventos.SizeChanged += (_, _) => AjustarColunasEventos();
        AjustarColunasEventos();
        Shown += AoExibirFormulario;
        FormClosing += AoFecharFormulario;
    }
    private void InicializarNavegador()
    {
        var visualizacao = painelNavegador.GetControlFromPosition(0, 1)!;
        painelNavegador.Controls.Remove(visualizacao);
        visualizacao.Dispose();
        navegador = new CefSharp.WinForms.ChromiumWebBrowser("about:blank")
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            MenuHandler = new CefSharp.Handler.ContextMenuHandler()
        };
        painelNavegador.Controls.Add(navegador, 0, 1);
    }
    public void HabilitarTesteExtracao(string caminho) => caminhoTesteExtracao = caminho;
    public void HabilitarVerificacaoCancelamento() => verificarCancelamento = true;
    public void DefinirCodigoInicial(string codigo) => campoCodigo.Text = codigo;
    public void HabilitarConsultaDiagnostico(string codigo) => codigoConsultaDiagnostico = codigo;
    public void HabilitarVerificacaoInicializacao(string caminho, bool inspecionarPortal = false)
    {
        caminhoVerificacao = caminho;
        this.inspecionarPortal = inspecionarPortal;
        logger.LogInformation("Modo de verificação de inicialização ativado.");
    }

    private async void AoExibirFormulario(object? sender, EventArgs e)
    {
        registro.MensagemRegistrada += AoRegistrarMensagem;
        foreach (var mensagem in registro.ObterHistorico())
            AoRegistrarMensagem(mensagem);
        logger.LogInformation("Aplicação iniciada. Aguardando o navegador integrado.");
        try
        {
            await navegadorPronto.Task.WaitAsync(TimeSpan.FromSeconds(configuracao.TimeoutNavegacaoSegundos));
            if (IsDisposed || Disposing)
                return;
            navegador.LoadHtml(RecursosAplicacao.LerTexto("Paginas.Inicio.html"));
            DefinirProcessando(false);
            DefinirStatus("Aguardando", Color.FromArgb(0, 123, 174));
            logger.LogInformation("Chromium pronto. CefSharp {Versao}; Chromium {Chromium}.", Cef.CefSharpVersion, Cef.ChromiumVersion);
            if (caminhoVerificacao is not null)
                await VerificarInicializacaoAsync();
            else if (caminhoTesteExtracao is not null)
                await ExecutarTesteExtracaoAsync();
            else if (verificarCancelamento)
                await VerificarCancelamentoAsync();
            else if (codigoConsultaDiagnostico is not null)
                await ExecutarConsultaDiagnosticoAsync();
        }
        catch (TimeoutException excecao)
        {
            logger.LogError(excecao, "O navegador não ficou pronto dentro do prazo.");
            DefinirStatus("Erro ao iniciar", Color.Firebrick);
            if (caminhoVerificacao is not null || caminhoTesteExtracao is not null || verificarCancelamento || codigoConsultaDiagnostico is not null)
            {
                Environment.ExitCode = 1;
                Close();
            }
        }
    }

    private async Task VerificarInicializacaoAsync()
    {
        try
        {
            await diagnostico.VerificarInicializacaoAsync(navegador, caminhoVerificacao!, inspecionarPortal);
        }
        catch (Exception excecao)
        {
            // Fronteira do diagnóstico: qualquer falha deve produzir um código de saída não zero.
            logger.LogError(excecao, "Falha na verificação de inicialização.");
            Environment.ExitCode = 1;
        }
        finally
        {
            Close();
        }
    }
    private async void AoAbrirPortalAsync(object? sender, EventArgs e)
    {
        if (operacaoAtual is not null || !NavegadorDisponivel || fechando)
            return;
        var codigos = ConsultaLoteService.SepararCodigos(campoCodigo.Text);
        if (codigos.Count > 1)
        {
            await ConsultarLoteAsync(codigos);
            return;
        }
        if (codigos.Count == 1)
            campoCodigo.Text = codigos[0];
        try
        {
            campoCodigo.Text = ValidadorCodigoRastreamento.ValidarCodigo(campoCodigo.Text);
        }
        catch (ArgumentException)
        {
            MessageBox.Show(this, ValidadorCodigoRastreamento.MensagemFormatoInvalido, "Confira o código", MessageBoxButtons.OK, MessageBoxIcon.Information);
            campoCodigo.Focus();
            return;
        }

        using var cancelamento = new CancellationTokenSource();
        operacaoAtual = cancelamento;
        DefinirProcessando(true);
        DefinirStatus("Navegando", Color.FromArgb(0, 123, 174));
        try
        {
            resultadoAtual = null;
            resultadoLoteAtual = null;
            listaObjetos.Items.Clear();
            campoJson.Clear();
            listaEventos.Items.Clear();
            contadorEventos.Text = "RESULTADO  ·  Consultando";
            var progressoConsulta = new Progress<string>(texto =>
            {
                if (ReferenceEquals(operacaoAtual, cancelamento) && !cancelamento.IsCancellationRequested)
                    DefinirStatus(texto, Color.FromArgb(0, 123, 174));
            });
            resultadoAtual = await consulta.ConsultarRastreamentoAsync(navegador, campoCodigo.Text, progressoConsulta, cancelamento.Token);
            campoJson.Text = exportacao.Serializar(resultadoAtual);
            AdicionarObjeto(new ResultadoConsultaObjeto { CodigoInformado = campoCodigo.Text, Resultado = resultadoAtual });
            ApresentarEventos(resultadoAtual);
            abas.SelectedTab = abaEventos;
            logger.LogInformation("JSON gerado para {Codigo}.", resultadoAtual.CodigoRastreamento);
            DefinirStatus("Concluído", Color.SeaGreen);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Consulta cancelada pelo usuário.");
            contadorEventos.Text = "RESULTADO  ·  Consulta cancelada";
            DefinirStatus("Cancelado", Color.SlateGray);
        }
        catch (Exception excecao) when (excecao is IOException or TimeoutException or InvalidOperationException or JsonException or ArgumentException)
        {
            logger.LogError(excecao, "Falha na consulta de rastreamento.");
            DefinirStatus("Falha na consulta", Color.Firebrick);
            contadorEventos.Text = "RESULTADO  ·  Sem resultado";
            if (!fechando)
                MessageBox.Show(this, excecao is ConsultaRastreamentoException ? excecao.Message : "Não foi possível concluir a consulta. Confira a conexão e consulte os logs para os detalhes.",
                    "Consulta não concluída", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            operacaoAtual = null;
            DefinirProcessando(false);
            if (fechando)
                Close();
        }
    }

    private void AoCopiarJson(object? sender, EventArgs e)
    {
        if (!PossuiResultado || operacaoAtual is not null || fechando)
            return;
        try
        {
            Clipboard.SetText(ObterJsonAtual());
            logger.LogInformation("JSON copiado para a área de transferência.");
        }
        catch (ExternalException excecao)
        {
            logger.LogWarning(excecao, "Área de transferência indisponível.");
            MessageBox.Show(this, "A área de transferência está ocupada. Tente novamente.");
        }
    }

    private async void AoExportarJsonAsync(object? sender, EventArgs e)
    {
        if (!PossuiResultado || operacaoAtual is not null)
            return;
        var nomeArquivo = resultadoLoteAtual is not null ? $"rastreamentos-{DateTime.Now:yyyyMMdd-HHmmss}.json" : $"rastreamento-{resultadoAtual!.CodigoRastreamento}.json";
        using var dialogo = new SaveFileDialog { Filter = "Arquivo JSON (*.json)|*.json", FileName = nomeArquivo, DefaultExt = "json", AddExtension = true };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;
        using var cancelamento = new CancellationTokenSource();
        operacaoAtual = cancelamento;
        DefinirProcessando(true, consulta: false);
        try
        {
            if (resultadoLoteAtual is not null)
                await exportacao.ExportarLoteJsonAsync(resultadoLoteAtual, dialogo.FileName, cancelamento.Token);
            else
                await exportacao.ExportarJsonAsync(resultadoAtual!, dialogo.FileName, cancelamento.Token);
            logger.LogInformation("Resultado exportado para {Arquivo}.", dialogo.FileName);
            DefinirStatus("JSON exportado", Color.SeaGreen);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Exportação cancelada.");
        }
        catch (Exception excecao) when (excecao is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogError(excecao, "Falha na exportação.");
            if (!fechando)
                MessageBox.Show(this, "Não foi possível salvar o arquivo. Confira a pasta de destino.");
        }
        finally
        {
            operacaoAtual = null;
            DefinirProcessando(false);
            if (fechando)
                Close();
        }
    }

    private void AoFecharFormulario(object? sender, FormClosingEventArgs e)
    {
        if (operacaoAtual is null)
            return;
        e.Cancel = true;
        fechando = true;
        operacaoAtual.Cancel();
    }

    private void AjustarColunasEventos()
    {
        if (listaEventos.Columns.Count != 3 || listaEventos.ClientSize.Width <= 0)
            return;
        var largura = Math.Max(0, listaEventos.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        listaEventos.Columns[0].Width = Math.Max(100, largura / 4);
        listaEventos.Columns[1].Width = Math.Max(120, largura * 2 / 5);
        listaEventos.Columns[2].Width = Math.Max(100, largura - listaEventos.Columns[0].Width - listaEventos.Columns[1].Width);
    }

    private void DefinirProcessando(bool processando, bool consulta = true)
    {
        consultaEmAndamento = processando && consulta;
        campoCodigo.Enabled = !processando && !fechando;
        progresso.Visible = processando;
        AtualizarBotoes();
        AtualizarMensagemEventos();
    }

    private void AtualizarBotoes()
    {
        var ociosa = operacaoAtual is null && !fechando;
        botaoAbrirPortal.Enabled = ociosa && NavegadorDisponivel && !string.IsNullOrWhiteSpace(campoCodigo.Text);
        botaoCancelar.Enabled = !ociosa && consultaEmAndamento && !fechando;
        botaoCopiar.Enabled = ociosa && PossuiResultado;
        botaoExportar.Enabled = ociosa && PossuiResultado;
    }

    private void AtualizarMensagemEventos()
    {
        rotuloSemEventos.Visible = listaEventos.Items.Count == 0;
        rotuloSemEventos.Text = consultaEmAndamento
            ? "Aguardando eventos da consulta..."
            : "Nenhum evento disponível. Inicie uma consulta ou selecione um objeto concluído.";
        if (rotuloSemEventos.Visible)
            rotuloSemEventos.BringToFront();
    }

    private void DefinirStatus(string texto, Color cor)
    {
        indicadorStatus.Text = "●  " + texto;
        indicadorStatus.ForeColor = cor;
    }

    private void AoRegistrarMensagem(MensagemLog mensagem)
    {
        var texto = mensagem.Texto;
        if (texto.StartsWith("Inicializando Chromium.") || texto.StartsWith("Modo de verificação") ||
            texto.StartsWith("Fluxo de consulta finalizado.") || texto.StartsWith("JSON gerado para") ||
            texto.StartsWith("JSON de lote gerado"))
            return;
        if (texto.StartsWith("Chromium pronto."))
            texto = "Aplicação pronta para consultar.";
        else if (texto.StartsWith("OCR selecionou"))
            texto = "CAPTCHA reconhecido. Aguardando validação do portal.";
        else if (texto.StartsWith("Abrindo portal dos Correios:"))
            texto = "Abrindo portal dos Correios.";
        else if (texto.StartsWith("Documento principal carregado."))
            texto = "Portal carregado.";
        else if (texto.StartsWith("Resultado exportado para"))
            texto = "Resultado exportado em JSON.";
        if (mensagem.Texto.Contains("[Não foi possível gravar o log em disco.]") &&
            !texto.Contains("[Não foi possível gravar o log em disco.]"))
            texto += " [Não foi possível gravar o log em disco.]";
        AtualizarInterface(() =>
        {
            if (campoLogs.Lines.Length > 500)
                campoLogs.Clear();
            campoLogs.AppendText($"{mensagem.DataHora:HH:mm:ss}  {texto}{Environment.NewLine}");
            campoLogs.SelectionStart = campoLogs.TextLength;
            campoLogs.ScrollToCaret();
        });
    }

    private void AtualizarInterface(Action atualizacao)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;
        if (!InvokeRequired)
        {
            atualizacao();
            return;
        }
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing)
                    atualizacao();
            }));
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated)
        {
            System.Diagnostics.Debug.WriteLine("Atualização descartada durante o encerramento da janela.");
        }
    }
}
