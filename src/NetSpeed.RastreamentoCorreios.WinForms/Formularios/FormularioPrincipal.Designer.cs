#nullable enable

namespace NetSpeed.RastreamentoCorreios.Formularios;

partial class FormularioPrincipal
{
    private System.ComponentModel.IContainer? components = null;
    private TableLayoutPanel estrutura = null!;
    private TableLayoutPanel cabecalho = null!;
    private TableLayoutPanel painelConsulta = null!;
    private SplitContainer areaTrabalho = null!;
    private TableLayoutPanel painelNavegador = null!;
    private Label visualizacaoNavegador = null!;
    private TableLayoutPanel areaResultado = null!;
    private TabControl abas = null!;
    private TabPage abaEventos = null!;
    private ComboBox seletorEventos = null!;
    private TableLayoutPanel areaEventos = null!;
    private Panel painelEventos = null!;
    private TabPage abaJson = null!;
    private ComboBox seletorJson = null!;
    private TableLayoutPanel areaJson = null!;
    private TabPage abaObjetos = null!;
    private ListView listaObjetos = null!;
    private FlowLayoutPanel acoesResultado = null!;
    private TableLayoutPanel areaLogs = null!;
    private Label rotuloTitulo = null!;
    private Label rotuloCodigo = null!;
    private Label rotuloAtividade = null!;
    private TextBox campoCodigo = null!;
    private Button botaoAbrirPortal = null!;
    private Button botaoCancelar = null!;
    private Label rotuloSemEventos = null!;
    private Button botaoCopiar = null!;
    private Button botaoExportar = null!;
    private Label indicadorStatus = null!;
    private Label contadorEventos = null!;
    private Label enderecoAtual = null!;
    private ProgressBar progresso = null!;
    private RichTextBox campoJson = null!;
    private RichTextBox campoLogs = null!;
    private ListView listaEventos = null!;
    private ColumnHeader colunaData = null!;
    private ColumnHeader colunaSituacao = null!;
    private ColumnHeader colunaLocal = null!;
    private ColumnHeader colunaObjeto = null!;
    private ColumnHeader colunaEstado = null!;
    private ColumnHeader colunaDetalhe = null!;

    private CefSharp.WinForms.ChromiumWebBrowser navegador = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (registro is not null)
                registro.MensagemRegistrada -= AoRegistrarMensagem;
            operacaoAtual?.Cancel();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        colunaData = new ColumnHeader();
        colunaSituacao = new ColumnHeader();
        colunaLocal = new ColumnHeader();
        colunaObjeto = new ColumnHeader();
        colunaEstado = new ColumnHeader();
        colunaDetalhe = new ColumnHeader();
        estrutura = new TableLayoutPanel();
        cabecalho = new TableLayoutPanel();
        rotuloTitulo = new Label();
        indicadorStatus = new Label();
        painelConsulta = new TableLayoutPanel();
        rotuloCodigo = new Label();
        campoCodigo = new TextBox();
        botaoAbrirPortal = new Button();
        botaoCancelar = new Button();
        progresso = new ProgressBar();
        areaTrabalho = new SplitContainer();
        painelNavegador = new TableLayoutPanel();
        enderecoAtual = new Label();
        visualizacaoNavegador = new Label();
        areaResultado = new TableLayoutPanel();
        contadorEventos = new Label();
        abas = new TabControl();
        abaEventos = new TabPage();
        seletorEventos = new ComboBox();
        areaEventos = new TableLayoutPanel();
        painelEventos = new Panel();
        listaEventos = new ListView();
        rotuloSemEventos = new Label();
        abaJson = new TabPage();
        seletorJson = new ComboBox();
        areaJson = new TableLayoutPanel();
        campoJson = new RichTextBox();
        abaObjetos = new TabPage();
        listaObjetos = new ListView();
        acoesResultado = new FlowLayoutPanel();
        botaoExportar = new Button();
        botaoCopiar = new Button();
        areaLogs = new TableLayoutPanel();
        rotuloAtividade = new Label();
        campoLogs = new RichTextBox();
        estrutura.SuspendLayout();
        cabecalho.SuspendLayout();
        painelConsulta.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)areaTrabalho).BeginInit();
        areaTrabalho.Panel1.SuspendLayout();
        areaTrabalho.Panel2.SuspendLayout();
        areaTrabalho.SuspendLayout();
        painelNavegador.SuspendLayout();
        areaResultado.SuspendLayout();
        abas.SuspendLayout();
        abaEventos.SuspendLayout();
        areaEventos.SuspendLayout();
        painelEventos.SuspendLayout();
        abaJson.SuspendLayout();
        areaJson.SuspendLayout();
        abaObjetos.SuspendLayout();
        acoesResultado.SuspendLayout();
        areaLogs.SuspendLayout();
        SuspendLayout();
        // 
        // colunaData
        // 
        colunaData.Text = "Data e hora";
        colunaData.Width = 130;
        // 
        // colunaSituacao
        // 
        colunaSituacao.Text = "Situação";
        colunaSituacao.Width = 240;
        // 
        // colunaLocal
        // 
        colunaLocal.Text = "Local / unidade";
        colunaLocal.Width = 230;
        // 
        // colunaObjeto
        // 
        colunaObjeto.Text = "Objeto";
        colunaObjeto.Width = 140;
        // 
        // colunaEstado
        // 
        colunaEstado.Text = "Estado";
        colunaEstado.Width = 100;
        // 
        // colunaDetalhe
        // 
        colunaDetalhe.Text = "Eventos / detalhe";
        colunaDetalhe.Width = 200;
        // 
        // estrutura
        // 
        estrutura.ColumnCount = 1;
        estrutura.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        estrutura.Controls.Add(cabecalho, 0, 0);
        estrutura.Controls.Add(painelConsulta, 0, 1);
        estrutura.Controls.Add(areaTrabalho, 0, 2);
        estrutura.Controls.Add(areaLogs, 0, 3);
        estrutura.Dock = DockStyle.Fill;
        estrutura.Location = new Point(0, 0);
        estrutura.Name = "estrutura";
        estrutura.Padding = new Padding(16);
        estrutura.RowCount = 4;
        estrutura.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        estrutura.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
        estrutura.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        estrutura.RowStyles.Add(new RowStyle(SizeType.Absolute, 166F));
        estrutura.Size = new Size(1360, 860);
        estrutura.TabIndex = 0;
        // 
        // cabecalho
        // 
        cabecalho.ColumnCount = 2;
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        cabecalho.Controls.Add(rotuloTitulo, 0, 0);
        cabecalho.Controls.Add(indicadorStatus, 1, 0);
        cabecalho.Dock = DockStyle.Fill;
        cabecalho.Location = new Point(19, 19);
        cabecalho.Name = "cabecalho";
        cabecalho.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        cabecalho.Size = new Size(1322, 56);
        cabecalho.TabIndex = 0;
        // 
        // rotuloTitulo
        // 
        rotuloTitulo.Dock = DockStyle.Fill;
        rotuloTitulo.Font = new Font("Segoe UI Semibold", 18F);
        rotuloTitulo.Location = new Point(3, 0);
        rotuloTitulo.Name = "rotuloTitulo";
        rotuloTitulo.Size = new Size(1136, 56);
        rotuloTitulo.TabIndex = 0;
        rotuloTitulo.Text = "Rastreamento Correios";
        rotuloTitulo.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // indicadorStatus
        // 
        indicadorStatus.Dock = DockStyle.Fill;
        indicadorStatus.ForeColor = Color.SlateGray;
        indicadorStatus.Location = new Point(1145, 0);
        indicadorStatus.Name = "indicadorStatus";
        indicadorStatus.Size = new Size(174, 56);
        indicadorStatus.TabIndex = 1;
        indicadorStatus.Text = "●  Inicializando";
        indicadorStatus.TextAlign = ContentAlignment.MiddleRight;
        // 
        // painelConsulta
        // 
        painelConsulta.BackColor = Color.White;
        painelConsulta.ColumnCount = 3;
        painelConsulta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        painelConsulta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136F));
        painelConsulta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
        painelConsulta.Controls.Add(rotuloCodigo, 0, 0);
        painelConsulta.Controls.Add(campoCodigo, 0, 1);
        painelConsulta.Controls.Add(botaoAbrirPortal, 1, 1);
        painelConsulta.Controls.Add(botaoCancelar, 2, 1);
        painelConsulta.Controls.Add(progresso, 0, 2);
        painelConsulta.Dock = DockStyle.Fill;
        painelConsulta.Location = new Point(16, 78);
        painelConsulta.Margin = new Padding(0, 0, 0, 10);
        painelConsulta.Name = "painelConsulta";
        painelConsulta.Padding = new Padding(12, 8, 12, 8);
        painelConsulta.RowCount = 3;
        painelConsulta.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        painelConsulta.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        painelConsulta.RowStyles.Add(new RowStyle(SizeType.Absolute, 4F));
        painelConsulta.Size = new Size(1328, 80);
        painelConsulta.TabIndex = 1;
        // 
        // rotuloCodigo
        // 
        rotuloCodigo.Dock = DockStyle.Fill;
        rotuloCodigo.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        rotuloCodigo.ForeColor = Color.SlateGray;
        rotuloCodigo.Location = new Point(15, 8);
        rotuloCodigo.Name = "rotuloCodigo";
        rotuloCodigo.Size = new Size(1054, 20);
        rotuloCodigo.TabIndex = 0;
        rotuloCodigo.Text = "Códigos de rastreamento (separe por ponto e vírgula)";
        // 
        // campoCodigo
        // 
        campoCodigo.AccessibleName = "Código de rastreamento";
        campoCodigo.CharacterCasing = CharacterCasing.Upper;
        campoCodigo.Dock = DockStyle.Fill;
        campoCodigo.Font = new Font("Segoe UI", 12F);
        campoCodigo.Location = new Point(12, 28);
        campoCodigo.Margin = new Padding(0, 0, 10, 0);
        campoCodigo.MaxLength = 5000;
        campoCodigo.Name = "campoCodigo";
        campoCodigo.PlaceholderText = "Ex.: AA123456789BR; BB123456789BR";
        campoCodigo.Size = new Size(1050, 34);
        campoCodigo.TabIndex = 1;
        // 
        // botaoAbrirPortal
        // 
        botaoAbrirPortal.BackColor = Color.FromArgb(0, 123, 174);
        botaoAbrirPortal.Dock = DockStyle.Fill;
        botaoAbrirPortal.Enabled = false;
        botaoAbrirPortal.FlatAppearance.BorderSize = 0;
        botaoAbrirPortal.FlatStyle = FlatStyle.Flat;
        botaoAbrirPortal.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        botaoAbrirPortal.ForeColor = Color.White;
        botaoAbrirPortal.Location = new Point(1072, 28);
        botaoAbrirPortal.Margin = new Padding(0, 0, 8, 0);
        botaoAbrirPortal.Name = "botaoAbrirPortal";
        botaoAbrirPortal.Size = new Size(128, 38);
        botaoAbrirPortal.TabIndex = 2;
        botaoAbrirPortal.Text = "Consultar";
        botaoAbrirPortal.UseVisualStyleBackColor = false;
        // 
        // botaoCancelar
        // 
        botaoCancelar.BackColor = Color.FromArgb(238, 244, 249);
        botaoCancelar.Dock = DockStyle.Fill;
        botaoCancelar.Enabled = false;
        botaoCancelar.FlatAppearance.BorderSize = 0;
        botaoCancelar.FlatStyle = FlatStyle.Flat;
        botaoCancelar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        botaoCancelar.ForeColor = Color.FromArgb(36, 69, 96);
        botaoCancelar.Location = new Point(1208, 28);
        botaoCancelar.Margin = new Padding(0);
        botaoCancelar.Name = "botaoCancelar";
        botaoCancelar.Size = new Size(108, 38);
        botaoCancelar.TabIndex = 3;
        botaoCancelar.Text = "Cancelar";
        botaoCancelar.UseVisualStyleBackColor = false;
        // 
        // progresso
        // 
        painelConsulta.SetColumnSpan(progresso, 3);
        progresso.Dock = DockStyle.Fill;
        progresso.Location = new Point(12, 67);
        progresso.Margin = new Padding(0, 1, 0, 0);
        progresso.MarqueeAnimationSpeed = 30;
        progresso.Name = "progresso";
        progresso.Size = new Size(1304, 5);
        progresso.Style = ProgressBarStyle.Marquee;
        progresso.TabIndex = 4;
        progresso.Visible = false;
        // 
        // areaTrabalho
        // 
        areaTrabalho.Dock = DockStyle.Fill;
        areaTrabalho.Location = new Point(19, 171);
        areaTrabalho.Name = "areaTrabalho";
        // 
        // areaTrabalho.Panel1
        // 
        areaTrabalho.Panel1.Controls.Add(painelNavegador);
        areaTrabalho.Panel1MinSize = 280;
        // 
        // areaTrabalho.Panel2
        // 
        areaTrabalho.Panel2.Controls.Add(areaResultado);
        areaTrabalho.Panel2MinSize = 330;
        areaTrabalho.Size = new Size(1322, 504);
        areaTrabalho.SplitterDistance = 708;
        areaTrabalho.SplitterWidth = 8;
        areaTrabalho.TabIndex = 2;
        // 
        // painelNavegador
        // 
        painelNavegador.BackColor = Color.White;
        painelNavegador.ColumnCount = 1;
        painelNavegador.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        painelNavegador.Controls.Add(enderecoAtual, 0, 0);
        painelNavegador.Controls.Add(visualizacaoNavegador, 0, 1);
        painelNavegador.Dock = DockStyle.Fill;
        painelNavegador.Location = new Point(0, 0);
        painelNavegador.Margin = new Padding(0);
        painelNavegador.Name = "painelNavegador";
        painelNavegador.RowCount = 2;
        painelNavegador.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        painelNavegador.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        painelNavegador.Size = new Size(708, 504);
        painelNavegador.TabIndex = 0;
        // 
        // enderecoAtual
        // 
        enderecoAtual.AutoEllipsis = true;
        enderecoAtual.Dock = DockStyle.Fill;
        enderecoAtual.Font = new Font("Segoe UI", 9F);
        enderecoAtual.ForeColor = Color.SlateGray;
        enderecoAtual.Location = new Point(3, 0);
        enderecoAtual.Name = "enderecoAtual";
        enderecoAtual.Padding = new Padding(10, 0, 10, 0);
        enderecoAtual.Size = new Size(702, 36);
        enderecoAtual.TabIndex = 0;
        enderecoAtual.Text = "NAVEGADOR  ·  Página inicial";
        enderecoAtual.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // visualizacaoNavegador
        // 
        visualizacaoNavegador.BackColor = Color.FromArgb(250, 252, 255);
        visualizacaoNavegador.Dock = DockStyle.Fill;
        visualizacaoNavegador.ForeColor = Color.SlateGray;
        visualizacaoNavegador.Location = new Point(3, 36);
        visualizacaoNavegador.Name = "visualizacaoNavegador";
        visualizacaoNavegador.Size = new Size(702, 468);
        visualizacaoNavegador.TabIndex = 1;
        visualizacaoNavegador.Text = "Navegador disponível ao executar a aplicação";
        visualizacaoNavegador.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // areaResultado
        // 
        areaResultado.BackColor = Color.White;
        areaResultado.ColumnCount = 1;
        areaResultado.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        areaResultado.Controls.Add(contadorEventos, 0, 0);
        areaResultado.Controls.Add(abas, 0, 1);
        areaResultado.Controls.Add(acoesResultado, 0, 2);
        areaResultado.Dock = DockStyle.Fill;
        areaResultado.Location = new Point(0, 0);
        areaResultado.Name = "areaResultado";
        areaResultado.Padding = new Padding(8);
        areaResultado.RowCount = 3;
        areaResultado.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        areaResultado.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        areaResultado.RowStyles.Add(new RowStyle());
        areaResultado.Size = new Size(606, 504);
        areaResultado.TabIndex = 1;
        // 
        // contadorEventos
        // 
        contadorEventos.AutoEllipsis = true;
        contadorEventos.Dock = DockStyle.Fill;
        contadorEventos.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        contadorEventos.ForeColor = Color.SlateGray;
        contadorEventos.Location = new Point(11, 8);
        contadorEventos.Name = "contadorEventos";
        contadorEventos.Size = new Size(584, 30);
        contadorEventos.TabIndex = 0;
        contadorEventos.Text = "RESULTADO  ·  0 eventos";
        // 
        // abas
        // 
        abas.Controls.Add(abaEventos);
        abas.Controls.Add(abaJson);
        abas.Controls.Add(abaObjetos);
        abas.Dock = DockStyle.Fill;
        abas.Location = new Point(11, 41);
        abas.Name = "abas";
        abas.SelectedIndex = 0;
        abas.Size = new Size(584, 408);
        abas.TabIndex = 1;
        // 
        // abaEventos
        // 
        abaEventos.BackColor = Color.White;
        abaEventos.Controls.Add(areaEventos);
        abaEventos.Location = new Point(4, 32);
        abaEventos.Name = "abaEventos";
        abaEventos.Padding = new Padding(4);
        abaEventos.Size = new Size(576, 372);
        abaEventos.TabIndex = 0;
        abaEventos.Text = "Eventos";
        // 
        // areaEventos
        //
        areaEventos.ColumnCount = 1;
        areaEventos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        areaEventos.RowCount = 2;
        areaEventos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        areaEventos.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        areaEventos.Dock = DockStyle.Fill;
        areaEventos.Margin = Padding.Empty;
        areaEventos.Name = "areaEventos";
        areaEventos.Controls.Add(seletorEventos, 0, 0);
        areaEventos.Controls.Add(painelEventos, 0, 1);
        //
        // seletorEventos
        //
        seletorEventos.AccessibleName = "Código de rastreamento dos eventos";
        seletorEventos.DisplayMember = "CodigoInformado";
        seletorEventos.Dock = DockStyle.Fill;
        seletorEventos.DropDownStyle = ComboBoxStyle.DropDownList;
        seletorEventos.Enabled = false;
        seletorEventos.Margin = new Padding(0, 0, 0, 6);
        seletorEventos.Name = "seletorEventos";
        seletorEventos.TabIndex = 0;
        //
        // painelEventos
        //
        painelEventos.Dock = DockStyle.Fill;
        painelEventos.Margin = Padding.Empty;
        painelEventos.Name = "painelEventos";
        painelEventos.TabIndex = 1;
        painelEventos.Controls.Add(listaEventos);
        painelEventos.Controls.Add(rotuloSemEventos);
        //
        // listaEventos
        // 
        listaEventos.BorderStyle = BorderStyle.None;
        listaEventos.Columns.AddRange(new ColumnHeader[] { colunaData, colunaSituacao, colunaLocal });
        listaEventos.Dock = DockStyle.Fill;
        listaEventos.FullRowSelect = true;
        listaEventos.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listaEventos.Location = new Point(4, 4);
        listaEventos.Name = "listaEventos";
        listaEventos.Size = new Size(568, 364);
        listaEventos.TabIndex = 0;
        listaEventos.UseCompatibleStateImageBehavior = false;
        listaEventos.View = View.Details;
        // 
        // rotuloSemEventos
        // 
        rotuloSemEventos.Dock = DockStyle.Fill;
        rotuloSemEventos.ForeColor = Color.SlateGray;
        rotuloSemEventos.Location = new Point(4, 4);
        rotuloSemEventos.Name = "rotuloSemEventos";
        rotuloSemEventos.Padding = new Padding(16);
        rotuloSemEventos.Size = new Size(568, 364);
        rotuloSemEventos.TabIndex = 1;
        rotuloSemEventos.Text = "Nenhum evento disponível. Inicie uma consulta.";
        rotuloSemEventos.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // abaJson
        // 
        abaJson.BackColor = Color.White;
        abaJson.Controls.Add(areaJson);
        abaJson.Location = new Point(4, 29);
        abaJson.Name = "abaJson";
        abaJson.Padding = new Padding(8);
        abaJson.Size = new Size(576, 375);
        abaJson.TabIndex = 1;
        abaJson.Text = "JSON";
        // 
        // areaJson
        //
        areaJson.ColumnCount = 1;
        areaJson.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        areaJson.RowCount = 2;
        areaJson.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        areaJson.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        areaJson.Dock = DockStyle.Fill;
        areaJson.Margin = Padding.Empty;
        areaJson.Name = "areaJson";
        areaJson.Controls.Add(seletorJson, 0, 0);
        areaJson.Controls.Add(campoJson, 0, 1);
        //
        // seletorJson
        //
        seletorJson.AccessibleName = "Objeto ou lote exibido em JSON";
        seletorJson.DisplayMember = "CodigoInformado";
        seletorJson.Dock = DockStyle.Fill;
        seletorJson.DropDownStyle = ComboBoxStyle.DropDownList;
        seletorJson.Enabled = false;
        seletorJson.Margin = new Padding(0, 0, 0, 6);
        seletorJson.Name = "seletorJson";
        seletorJson.TabIndex = 0;
        //
        // campoJson
        // 
        campoJson.BackColor = Color.White;
        campoJson.BorderStyle = BorderStyle.None;
        campoJson.DetectUrls = false;
        campoJson.Dock = DockStyle.Fill;
        campoJson.Font = new Font("Consolas", 10F);
        campoJson.Location = new Point(8, 8);
        campoJson.Name = "campoJson";
        campoJson.ReadOnly = true;
        campoJson.Size = new Size(560, 359);
        campoJson.TabIndex = 1;
        campoJson.Text = "O JSON da consulta será apresentado aqui.";
        campoJson.WordWrap = false;
        // 
        // abaObjetos
        // 
        abaObjetos.BackColor = Color.White;
        abaObjetos.Controls.Add(listaObjetos);
        abaObjetos.Location = new Point(4, 29);
        abaObjetos.Name = "abaObjetos";
        abaObjetos.Padding = new Padding(4);
        abaObjetos.Size = new Size(576, 375);
        abaObjetos.TabIndex = 2;
        abaObjetos.Text = "Objetos";
        // 
        // listaObjetos
        // 
        listaObjetos.BorderStyle = BorderStyle.None;
        listaObjetos.Columns.AddRange(new ColumnHeader[] { colunaObjeto, colunaEstado, colunaDetalhe });
        listaObjetos.Dock = DockStyle.Fill;
        listaObjetos.FullRowSelect = true;
        listaObjetos.Location = new Point(4, 4);
        listaObjetos.MultiSelect = false;
        listaObjetos.Name = "listaObjetos";
        listaObjetos.ShowItemToolTips = true;
        listaObjetos.Size = new Size(568, 367);
        listaObjetos.TabIndex = 0;
        listaObjetos.UseCompatibleStateImageBehavior = false;
        listaObjetos.View = View.Details;
        // 
        // acoesResultado
        // 
        acoesResultado.AutoSize = true;
        acoesResultado.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        acoesResultado.Controls.Add(botaoExportar);
        acoesResultado.Controls.Add(botaoCopiar);
        acoesResultado.Dock = DockStyle.Fill;
        acoesResultado.FlowDirection = FlowDirection.RightToLeft;
        acoesResultado.Location = new Point(8, 452);
        acoesResultado.Margin = new Padding(0);
        acoesResultado.Name = "acoesResultado";
        acoesResultado.Padding = new Padding(0, 6, 0, 0);
        acoesResultado.Size = new Size(590, 44);
        acoesResultado.TabIndex = 2;
        acoesResultado.WrapContents = false;
        // 
        // botaoExportar
        // 
        botaoExportar.BackColor = Color.FromArgb(0, 123, 174);
        botaoExportar.Enabled = false;
        botaoExportar.FlatAppearance.BorderSize = 0;
        botaoExportar.FlatStyle = FlatStyle.Flat;
        botaoExportar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        botaoExportar.ForeColor = Color.White;
        botaoExportar.Location = new Point(447, 9);
        botaoExportar.Name = "botaoExportar";
        botaoExportar.Size = new Size(140, 32);
        botaoExportar.TabIndex = 0;
        botaoExportar.Text = "Exportar JSON";
        botaoExportar.UseVisualStyleBackColor = false;
        // 
        // botaoCopiar
        // 
        botaoCopiar.BackColor = Color.FromArgb(238, 244, 249);
        botaoCopiar.Enabled = false;
        botaoCopiar.FlatAppearance.BorderSize = 0;
        botaoCopiar.FlatStyle = FlatStyle.Flat;
        botaoCopiar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        botaoCopiar.ForeColor = Color.FromArgb(36, 69, 96);
        botaoCopiar.Location = new Point(311, 9);
        botaoCopiar.Name = "botaoCopiar";
        botaoCopiar.Size = new Size(130, 32);
        botaoCopiar.TabIndex = 1;
        botaoCopiar.Text = "Copiar JSON";
        botaoCopiar.UseVisualStyleBackColor = false;
        // 
        // areaLogs
        // 
        areaLogs.BackColor = Color.White;
        areaLogs.ColumnCount = 1;
        areaLogs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        areaLogs.Controls.Add(rotuloAtividade, 0, 0);
        areaLogs.Controls.Add(campoLogs, 0, 1);
        areaLogs.Dock = DockStyle.Fill;
        areaLogs.Location = new Point(16, 688);
        areaLogs.Margin = new Padding(0, 10, 0, 0);
        areaLogs.Name = "areaLogs";
        areaLogs.Padding = new Padding(10, 8, 10, 8);
        areaLogs.RowCount = 2;
        areaLogs.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        areaLogs.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        areaLogs.Size = new Size(1328, 156);
        areaLogs.TabIndex = 3;
        // 
        // rotuloAtividade
        // 
        rotuloAtividade.Dock = DockStyle.Fill;
        rotuloAtividade.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        rotuloAtividade.ForeColor = Color.SlateGray;
        rotuloAtividade.Location = new Point(13, 8);
        rotuloAtividade.Name = "rotuloAtividade";
        rotuloAtividade.Size = new Size(1302, 24);
        rotuloAtividade.TabIndex = 0;
        rotuloAtividade.Text = "Atividade da execução";
        // 
        // campoLogs
        // 
        campoLogs.BackColor = Color.White;
        campoLogs.BorderStyle = BorderStyle.None;
        campoLogs.DetectUrls = false;
        campoLogs.Dock = DockStyle.Fill;
        campoLogs.Font = new Font("Consolas", 9F);
        campoLogs.Location = new Point(13, 35);
        campoLogs.Name = "campoLogs";
        campoLogs.ReadOnly = true;
        campoLogs.Size = new Size(1302, 110);
        campoLogs.TabIndex = 1;
        campoLogs.Text = "";
        // 
        // FormularioPrincipal
        // 
        AutoScaleDimensions = new SizeF(120F, 120F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(241, 244, 247);
        ClientSize = new Size(1360, 860);
        Controls.Add(estrutura);
        Font = new Font("Segoe UI", 10F);
        ForeColor = Color.FromArgb(25, 45, 65);
        MinimumSize = new Size(1000, 680);
        Name = "FormularioPrincipal";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "NetSpeed · Rastreamento Correios";
        estrutura.ResumeLayout(false);
        cabecalho.ResumeLayout(false);
        painelConsulta.ResumeLayout(false);
        painelConsulta.PerformLayout();
        areaTrabalho.Panel1.ResumeLayout(false);
        areaTrabalho.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)areaTrabalho).EndInit();
        areaTrabalho.ResumeLayout(false);
        painelNavegador.ResumeLayout(false);
        areaResultado.ResumeLayout(false);
        areaResultado.PerformLayout();
        abas.ResumeLayout(false);
        painelEventos.ResumeLayout(false);
        areaEventos.ResumeLayout(false);
        abaEventos.ResumeLayout(false);
        areaJson.ResumeLayout(false);
        abaJson.ResumeLayout(false);
        abaObjetos.ResumeLayout(false);
        acoesResultado.ResumeLayout(false);
        areaLogs.ResumeLayout(false);
        ResumeLayout(false);
    }
}
