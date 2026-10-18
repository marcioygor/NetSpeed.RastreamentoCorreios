using System.Reflection;
using NetSpeed.RastreamentoCorreios.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Formularios;
using NetSpeed.RastreamentoCorreios.Modelos;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class FormularioPrincipalTests
{
    [Fact]
    public async Task ListasApresentamResultadosEBotoesCabemNoPainel()
    {
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var formulario = new FormularioPrincipal();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                T Campo<T>(string nome) => (T)typeof(FormularioPrincipal).GetField(nome, flags)!.GetValue(formulario)!;
                void Executar(string nome, object valor) => typeof(FormularioPrincipal).GetMethod(nome, flags)!.Invoke(formulario, [valor]);
                var eventos = Campo<ListView>("listaEventos");
                var objetos = Campo<ListView>("listaObjetos");
                Assert.Equal(3, eventos.Columns.Count);
                Assert.Equal(3, objetos.Columns.Count);
                var resultado = new ResultadoRastreamento
                {
                    CodigoRastreamento = "AA123456789BR",
                    Eventos = [new EventoRastreamento { Situacao = "Evento de teste", Local = "Unidade de teste" }]
                };
                Executar("ApresentarEventos", resultado);
                Executar("AdicionarObjeto", new ResultadoConsultaObjeto { CodigoInformado = resultado.CodigoRastreamento, Resultado = resultado });
                Assert.Equal("Evento de teste", Assert.Single(eventos.Items.Cast<ListViewItem>()).SubItems[1].Text);
                Assert.Equal("AA123456789BR", Assert.Single(objetos.Items.Cast<ListViewItem>()).Text);
                formulario.Show();
                Executar("AoRegistrarMensagem", new MensagemLog(DateTimeOffset.Now, LogLevel.Warning,
                    "O portal respondeu: System.Exception: detalhe reservado\n at CodigoInterno()"));
                var atividade = Campo<RichTextBox>("campoLogs").Text;
                Assert.Contains("O portal não retornou um histórico", atividade);
                Assert.DoesNotContain("System.Exception", atividade);
                Assert.DoesNotContain("CodigoInterno", atividade);
                using var cancelamento = new CancellationTokenSource();
                typeof(FormularioPrincipal).GetField("operacaoAtual", flags)!.SetValue(formulario, cancelamento);
                var definirProcessando = typeof(FormularioPrincipal).GetMethod("DefinirProcessando", flags)!;
                definirProcessando.Invoke(formulario, [true, true]);
                Assert.True(Campo<Button>("botaoCancelar").Enabled);
                Assert.False(Campo<Button>("botaoAbrirPortal").Enabled);
                definirProcessando.Invoke(formulario, [true, false]);
                Assert.False(Campo<Button>("botaoCancelar").Enabled);
                typeof(FormularioPrincipal).GetField("operacaoAtual", flags)!.SetValue(formulario, null);
                definirProcessando.Invoke(formulario, [false, true]);
                foreach (var tamanho in new[] { new Size(1000, 680), new Size(1360, 860) })
                {
                    formulario.Size = tamanho;
                    formulario.PerformLayout();
                    Application.DoEvents();
                    var painel = Campo<FlowLayoutPanel>("acoesResultado");
                    foreach (var nome in new[] { "botaoCopiar", "botaoExportar" })
                    {
                        var botao = Campo<Button>(nome);
                        Assert.True(botao.Bottom <= painel.ClientSize.Height, $"{nome}: {botao.Bottom} > {painel.ClientSize.Height}");
                    }
                }
                Executar("AdicionarObjeto", new ResultadoConsultaObjeto
                {
                    CodigoInformado = "BB123456789BR", TipoErro = "nao_consultado",
                    Erro = "Objeto não consultado porque o portal limitou temporariamente as consultas. Tente novamente mais tarde."
                });
                Assert.Equal("Não consultado", objetos.Items[1].SubItems[1].Text);
                Assert.Contains("portal limitou", objetos.Items[1].SubItems[2].Text);
                objetos.Columns.Clear();
                typeof(FormularioPrincipal).GetMethod("AjustarColunasObjetos", flags)!.Invoke(formulario, null);
                formulario.Close();
                conclusao.SetResult();
            }
            catch (Exception excecao) { conclusao.SetException(excecao); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await conclusao.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task SeletorEventosIsolaHistoricosELimpaResultadosEntreConsultas()
    {
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var formulario = new FormularioPrincipal();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                T Campo<T>(string nome) => (T)typeof(FormularioPrincipal).GetField(nome, flags)!.GetValue(formulario)!;
                void Definir(string nome, object? valor) => typeof(FormularioPrincipal).GetField(nome, flags)!.SetValue(formulario, valor);
                void Executar(string nome) => typeof(FormularioPrincipal).GetMethod(nome, flags)!.Invoke(formulario, null);
                var seletor = Campo<ComboBox>("seletorEventos");
                seletor.SelectedIndexChanged += typeof(FormularioPrincipal).GetMethod("AoSelecionarCodigoEventos", flags)!
                    .CreateDelegate<EventHandler>(formulario);
                var eventos = Campo<ListView>("listaEventos");
                var objetos = Campo<ListView>("listaObjetos");
                var cabecalho = Campo<Label>("contadorEventos");
                var mensagem = Campo<Label>("rotuloSemEventos");
                var json = Campo<RichTextBox>("campoJson");
                ResultadoConsultaObjeto Objeto(string codigo, int quantidade) => new()
                {
                    CodigoInformado = codigo,
                    Resultado = new ResultadoRastreamento
                    {
                        CodigoRastreamento = codigo,
                        Eventos = Enumerable.Range(1, quantidade).Select(indice => new EventoRastreamento
                        {
                            Situacao = $"{codigo}: evento {indice}"
                        }).ToArray()
                    }
                };
                var primeiro = Objeto("AA123456789BR", 9);
                var segundo = Objeto("BB123456789BR", 7);
                var vazio = Objeto("CC123456789BR", 0);
                var falha = new ResultadoConsultaObjeto { CodigoInformado = "DD123456789BR", Erro = "Objeto não encontrado", TipoErro = "portal" };
                var lote = new List<ResultadoConsultaObjeto> { primeiro, segundo, vazio, falha };
                Definir("resultadoLoteAtual", lote);
                foreach (var item in lote)
                    typeof(FormularioPrincipal).GetMethod("AdicionarObjeto", flags)!.Invoke(formulario, [item]);
                json.Text = "JSON do lote preservado";
                formulario.Show();
                Executar("AtualizarSeletorEventos");
                Assert.Equal(4, seletor.Items.Count);
                Assert.Equal(0, seletor.SelectedIndex);
                Assert.True(seletor.Enabled);
                Assert.Equal(ComboBoxStyle.DropDownList, seletor.DropDownStyle);
                Assert.Equal(9, eventos.Items.Count);
                Assert.Contains("AA123456789BR · 9 eventos", cabecalho.Text);
                seletor.SelectedIndex = 1;
                Assert.Equal(7, eventos.Items.Count);
                Assert.All(eventos.Items.Cast<ListViewItem>(), item => Assert.StartsWith("BB123456789BR:", item.SubItems[1].Text));
                Assert.Contains("BB123456789BR · 7 eventos", cabecalho.Text);
                seletor.SelectedIndex = 2;
                Assert.Empty(eventos.Items.Cast<ListViewItem>());
                Assert.True(mensagem.Visible);
                Assert.Contains("CC123456789BR", mensagem.Text);
                Assert.Contains("0 eventos", cabecalho.Text);
                seletor.SelectedIndex = 3;
                Assert.Empty(eventos.Items.Cast<ListViewItem>());
                Assert.True(mensagem.Visible);
                Assert.Equal("Objeto não encontrado", mensagem.Text);
                Assert.Contains("DD123456789BR", cabecalho.Text);
                Assert.Equal("JSON do lote preservado", json.Text);
                Assert.Equal(4, objetos.Items.Count);

                // A seleção existente na aba Objetos continua apresentando o mesmo histórico.
                seletor.SelectedIndex = 0;
                Campo<TabControl>("abas").SelectedTab = Campo<TabPage>("abaObjetos");
                Application.DoEvents();
                objetos.Items[1].Selected = true;
                typeof(FormularioPrincipal).GetMethod("AoSelecionarObjeto", flags)!.Invoke(formulario, [null, EventArgs.Empty]);
                Assert.Equal(1, seletor.SelectedIndex);
                Assert.Equal(7, eventos.Items.Count);

                // Uma nova consulta remove a seleção e as linhas da execução anterior.
                Definir("resultadoAtual", null);
                Definir("resultadoLoteAtual", null);
                Executar("LimparEventosConsulta");
                Assert.Empty(seletor.Items.Cast<object>());
                Assert.Empty(eventos.Items.Cast<ListViewItem>());
                Assert.False(seletor.Enabled);
                var unico = Objeto("EE123456789BR", 2);
                Definir("resultadoAtual", unico.Resultado);
                Executar("AtualizarSeletorEventos");
                Assert.Single(seletor.Items.Cast<object>());
                Assert.Equal(0, seletor.SelectedIndex);
                Assert.False(seletor.Enabled);
                Assert.Equal(2, eventos.Items.Count);
                Assert.Contains("EE123456789BR · 2 eventos", cabecalho.Text);

                Definir("resultadoAtual", null);
                Definir("resultadoLoteAtual", new List<ResultadoConsultaObjeto> { segundo, primeiro });
                Executar("LimparEventosConsulta");
                Executar("AtualizarSeletorEventos");
                Assert.Equal(2, seletor.Items.Count);
                Assert.Same(segundo, seletor.SelectedItem);
                Assert.Equal(7, eventos.Items.Count);
                seletor.SelectedIndex = 1;
                Assert.Equal(9, eventos.Items.Count);
                Assert.All(eventos.Items.Cast<ListViewItem>(), item => Assert.StartsWith("AA123456789BR:", item.SubItems[1].Text));
                Assert.Equal("JSON do lote preservado", json.Text);
                Assert.Equal(4, objetos.Items.Count);
                formulario.Close();
                conclusao.SetResult();
            }
            catch (Exception excecao) { conclusao.SetException(excecao); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await conclusao.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }


    [Fact]
    public async Task SeletorJsonPreservaSelecoesECopiaEExportaConteudoExibido()
    {
        var textosExibidos = new List<string>();
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var formulario = new FormularioPrincipal();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                T Campo<T>(string nome) => (T)typeof(FormularioPrincipal).GetField(nome, flags)!.GetValue(formulario)!;
                void Definir(string nome, object? valor) => typeof(FormularioPrincipal).GetField(nome, flags)!.SetValue(formulario, valor);
                void Executar(string nome) => typeof(FormularioPrincipal).GetMethod(nome, flags)!.Invoke(formulario, null);
                var exportacao = new ExportacaoJsonService();
                Definir("exportacao", exportacao);
                Definir("logger", NullLogger<FormularioPrincipal>.Instance);
                var seletor = Campo<ComboBox>("seletorJson");
                var seletorEventos = Campo<ComboBox>("seletorEventos");
                seletor.SelectedIndexChanged += typeof(FormularioPrincipal).GetMethod("AoSelecionarCodigoJson", flags)!
                    .CreateDelegate<EventHandler>(formulario);
                seletorEventos.SelectedIndexChanged += typeof(FormularioPrincipal).GetMethod("AoSelecionarCodigoEventos", flags)!
                    .CreateDelegate<EventHandler>(formulario);
                var json = Campo<RichTextBox>("campoJson");
                var abas = Campo<TabControl>("abas");
                ResultadoConsultaObjeto Objeto(string codigo, int quantidade) => new()
                {
                    CodigoInformado = codigo,
                    Resultado = new ResultadoRastreamento
                    {
                        CodigoRastreamento = codigo,
                        Eventos = Enumerable.Range(1, quantidade).Select(indice => new EventoRastreamento { Situacao = $"{codigo}: {indice}" }).ToArray()
                    }
                };
                var primeiro = Objeto("AA123456789BR", 2);
                var segundo = Objeto("BB123456789BR", 3);
                var vazio = Objeto("CC123456789BR", 0);
                var falha = new ResultadoConsultaObjeto { CodigoInformado = "DD123456789BR", TipoErro = "portal", Erro = "Objeto não encontrado" };
                var lote = new List<ResultadoConsultaObjeto> { primeiro, segundo, vazio, falha };
                Definir("resultadoLoteAtual", lote);
                foreach (var item in lote)
                    typeof(FormularioPrincipal).GetMethod("AdicionarObjeto", flags)!.Invoke(formulario, [item]);
                formulario.Show();
                Executar("AtualizarSeletorEventos");
                Executar("AtualizarSeletorJson");
                Assert.Equal(5, seletor.Items.Count);
                Assert.Equal(0, seletor.SelectedIndex);
                Assert.Equal("Todos os objetos (lote completo)", seletor.Text);
                Assert.Equal(exportacao.SerializarLote(lote).ReplaceLineEndings("\n"), json.Text.ReplaceLineEndings("\n"));
                var clipboardAnterior = Clipboard.GetDataObject();
                try
                {
                    void VerificarCopia(string esperado)
                    {
                        Assert.Equal(esperado.ReplaceLineEndings("\n"), json.Text.ReplaceLineEndings("\n"));
                        var conteudo = (string)typeof(FormularioPrincipal).GetMethod("ObterJsonAtual", flags)!.Invoke(formulario, null)!;
                        Assert.Equal(json.Text, conteudo);
                        typeof(FormularioPrincipal).GetMethod("AoCopiarJson", flags)!.Invoke(formulario, [null, EventArgs.Empty]);
                        Assert.Equal(json.Text, Clipboard.GetText());
                        textosExibidos.Add(conteudo);
                    }
                    VerificarCopia(exportacao.SerializarLote(lote));
                    seletor.SelectedIndex = 1;
                    VerificarCopia(exportacao.Serializar(primeiro.Resultado!));
                    seletor.SelectedIndex = 2;
                    VerificarCopia(exportacao.Serializar(segundo.Resultado!));
                    Assert.Equal("BB123456789BR", seletor.Text);
                    Assert.Equal(0, seletorEventos.SelectedIndex);
                    seletorEventos.SelectedIndex = 1;
                    foreach (var aba in new[] { "abaEventos", "abaJson", "abaObjetos", "abaJson" })
                        abas.SelectedTab = Campo<TabPage>(aba);
                    Assert.Equal(2, seletor.SelectedIndex);
                    Assert.Equal(1, seletorEventos.SelectedIndex);
                    Assert.Equal(exportacao.Serializar(segundo.Resultado!).ReplaceLineEndings("\n"), json.Text.ReplaceLineEndings("\n"));
                    Assert.Equal(4, Campo<ListView>("listaObjetos").Items.Count);
                    seletor.SelectedIndex = 3;
                    VerificarCopia(exportacao.Serializar(vazio.Resultado!));
                    seletor.SelectedIndex = 4;
                    VerificarCopia(exportacao.SerializarLote([falha]));
                    Assert.DoesNotContain("AA123456789BR", json.Text);
                    Assert.Contains("Objeto não encontrado", json.Text);
                    seletor.SelectedIndex = 0;
                    Assert.Equal(exportacao.SerializarLote(lote).ReplaceLineEndings("\n"), json.Text.ReplaceLineEndings("\n"));
                    Assert.Equal(1, seletorEventos.SelectedIndex);

                    using var cancelamento = new CancellationTokenSource();
                    Definir("operacaoAtual", cancelamento);
                    typeof(FormularioPrincipal).GetMethod("DefinirProcessando", flags)!.Invoke(formulario, [true, false]);
                    Assert.False(seletor.Enabled);
                    Definir("operacaoAtual", null);
                    typeof(FormularioPrincipal).GetMethod("DefinirProcessando", flags)!.Invoke(formulario, [false, true]);
                    Assert.True(seletor.Enabled);

                    // Nova consulta única: não restam opções nem JSON do lote anterior.
                    Definir("resultadoLoteAtual", null);
                    Definir("resultadoAtual", null);
                    Executar("LimparJsonConsulta");
                    Executar("LimparEventosConsulta");
                    Assert.Empty(seletor.Items.Cast<object>());
                    Assert.Empty(json.Text);
                    Assert.False(seletor.Enabled);
                    var unico = Objeto("EE123456789BR", 1);
                    Definir("resultadoAtual", unico.Resultado);
                    Executar("AtualizarSeletorEventos");
                    Executar("AtualizarSeletorJson");
                    Assert.Equal(2, seletor.Items.Count);
                    Assert.Equal(0, seletor.SelectedIndex);
                    VerificarCopia(exportacao.Serializar(unico.Resultado!));
                    seletor.SelectedIndex = 1;
                    VerificarCopia(exportacao.Serializar(unico.Resultado!));
                    Assert.Equal("EE123456789BR", seletor.Text);
                    Assert.Equal(0, seletorEventos.SelectedIndex);
                    abas.SelectedTab = Campo<TabPage>("abaJson");
                    formulario.PerformLayout();
                    Application.DoEvents();
                    Assert.False(seletor.Visible);
                    Assert.Equal(0, Campo<TableLayoutPanel>("areaJson").GetRowHeights()[0]);
                }
                finally
                {
                    if (clipboardAnterior is not null)
                        Clipboard.SetDataObject(clipboardAnterior, true);
                    else
                        Clipboard.Clear();
                }
                formulario.Close();
                conclusao.SetResult();
            }
            catch (Exception excecao) { conclusao.SetException(excecao); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await conclusao.Task.WaitAsync(TimeSpan.FromSeconds(20));

        // O mesmo gravador usado pelo botão deve preservar exatamente cada texto selecionado.
        var diretorio = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(diretorio);
        try
        {
            var exportacao = new ExportacaoJsonService();
            var caminho = Path.Combine(diretorio, "resultado.json");
            foreach (var textoExibido in textosExibidos)
            {
                await exportacao.ExportarTextoJsonAsync(textoExibido, caminho, CancellationToken.None);
                Assert.Equal(textoExibido, await File.ReadAllTextAsync(caminho));
            }
        }
        finally
        {
            Directory.Delete(diretorio, true);
        }
    }


    [Fact]
    public async Task SeletoresAparecemSomenteComMultiplosObjetosSemReservarEspacoVazio()
    {
        var conclusao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var formulario = new FormularioPrincipal();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                T Campo<T>(string nome) => (T)typeof(FormularioPrincipal).GetField(nome, flags)!.GetValue(formulario)!;
                void Definir(string nome, object? valor) => typeof(FormularioPrincipal).GetField(nome, flags)!.SetValue(formulario, valor);
                void Executar(string nome) => typeof(FormularioPrincipal).GetMethod(nome, flags)!.Invoke(formulario, null);
                Definir("exportacao", new ExportacaoJsonService());
                var abas = Campo<TabControl>("abas");
                var eventos = Campo<ComboBox>("seletorEventos");
                var json = Campo<ComboBox>("seletorJson");
                eventos.SelectedIndexChanged += typeof(FormularioPrincipal).GetMethod("AoSelecionarCodigoEventos", flags)!
                    .CreateDelegate<EventHandler>(formulario);
                json.SelectedIndexChanged += typeof(FormularioPrincipal).GetMethod("AoSelecionarCodigoJson", flags)!
                    .CreateDelegate<EventHandler>(formulario);
                void VerificarVisibilidade(bool visivel)
                {
                    foreach (var nome in new[] { "Eventos", "Json" })
                    {
                        abas.SelectedTab = Campo<TabPage>("aba" + nome);
                        formulario.PerformLayout();
                        Application.DoEvents();
                        var seletor = Campo<ComboBox>("seletor" + nome);
                        var painel = Campo<TableLayoutPanel>("area" + nome);
                        Assert.Equal(visivel, seletor.Visible);
                        if (visivel)
                            Assert.True(painel.GetRowHeights()[0] > 0);
                        else
                            Assert.Equal(0, painel.GetRowHeights()[0]);
                    }
                }
                void Atualizar()
                {
                    Executar("AtualizarSeletorEventos");
                    Executar("AtualizarSeletorJson");
                }
                formulario.Show();
                VerificarVisibilidade(false);
                Atualizar();
                VerificarVisibilidade(false);
                Assert.Contains("Sem resultado", Campo<Label>("contadorEventos").Text);

                var primeiro = new ResultadoConsultaObjeto
                {
                    CodigoInformado = "AA123456789BR",
                    Resultado = new ResultadoRastreamento
                    {
                        CodigoRastreamento = "AA123456789BR",
                        Eventos = [new EventoRastreamento { Situacao = "Primeiro evento" }]
                    }
                };
                var segundo = new ResultadoConsultaObjeto
                {
                    CodigoInformado = "BB123456789BR",
                    Resultado = new ResultadoRastreamento
                    {
                        CodigoRastreamento = "BB123456789BR",
                        Eventos = [new EventoRastreamento { Situacao = "Segundo evento" }]
                    }
                };
                Definir("resultadoAtual", primeiro.Resultado);
                Atualizar();
                VerificarVisibilidade(false);
                Assert.Single(Campo<ListView>("listaEventos").Items.Cast<ListViewItem>());
                Assert.Contains("AA123456789BR", Campo<RichTextBox>("campoJson").Text);
                Assert.True(Campo<Button>("botaoCopiar").Enabled);
                Assert.True(Campo<Button>("botaoExportar").Enabled);

                Definir("resultadoLoteAtual", new List<ResultadoConsultaObjeto> { primeiro, segundo });
                Atualizar();
                VerificarVisibilidade(true);
                Assert.Equal(0, json.SelectedIndex);
                Assert.Equal("Todos os objetos (lote completo)", json.Text);
                eventos.SelectedIndex = 1;
                Assert.Equal(0, json.SelectedIndex);
                json.SelectedIndex = 1;
                Assert.Equal(1, eventos.SelectedIndex);

                // Um lote parcialmente concluído com apenas um resultado também dispensa os seletores.
                Definir("resultadoLoteAtual", new List<ResultadoConsultaObjeto> { segundo });
                Atualizar();
                VerificarVisibilidade(false);
                Assert.DoesNotContain("AA123456789BR", Campo<RichTextBox>("campoJson").Text);
                Assert.Contains("BB123456789BR", Campo<RichTextBox>("campoJson").Text);

                Definir("resultadoAtual", null);
                Definir("resultadoLoteAtual", new List<ResultadoConsultaObjeto>());
                Executar("LimparEventosConsulta");
                Executar("LimparJsonConsulta");
                Atualizar();
                VerificarVisibilidade(false);
                Assert.Empty(eventos.Items.Cast<object>());
                Assert.Empty(json.Items.Cast<object>());
                Assert.Empty(Campo<ListView>("listaEventos").Items.Cast<ListViewItem>());
                Assert.Empty(Campo<RichTextBox>("campoJson").Text);
                Assert.Contains("Sem resultado", Campo<Label>("contadorEventos").Text);
                Assert.False(Campo<Button>("botaoCopiar").Enabled);
                Assert.False(Campo<Button>("botaoExportar").Enabled);

                Definir("resultadoLoteAtual", new List<ResultadoConsultaObjeto> { segundo, primeiro });
                Atualizar();
                VerificarVisibilidade(true);
                Assert.Same(segundo, eventos.SelectedItem);
                Assert.Equal(0, json.SelectedIndex);
                formulario.Close();
                conclusao.SetResult();
            }
            catch (Exception excecao) { conclusao.SetException(excecao); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await conclusao.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

}
