using System.Reflection;
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
}
