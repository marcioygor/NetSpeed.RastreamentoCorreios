using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Formularios;

public partial class FormularioPrincipal
{
    private List<ResultadoConsultaObjeto>? resultadoLoteAtual;
    private bool PossuiResultado => resultadoAtual is not null || resultadoLoteAtual is { Count: > 0 };

    private async Task ConsultarLoteAsync(IReadOnlyList<string> codigos)
    {
        using var cancelamento = new CancellationTokenSource();
        operacaoAtual = cancelamento;
        resultadoAtual = null;
        resultadoLoteAtual = [];
        listaObjetos.Items.Clear();
        listaEventos.Items.Clear();
        campoJson.Clear();
        contadorEventos.Text = "RESULTADO  ·  Consultando lote";
        abas.SelectedTab = abaObjetos;
        DefinirProcessando(true);
        var progressoConsulta = new Progress<string>(texto =>
        {
            if (ReferenceEquals(operacaoAtual, cancelamento) && !cancelamento.IsCancellationRequested)
                DefinirStatus(texto, Color.FromArgb(0, 123, 174));
        });
        try
        {
            await foreach (var item in consultaLote.ConsultarAsync(codigos,
                (codigo, token) => consulta.ConsultarRastreamentoAsync(navegador, codigo, progressoConsulta, token), cancelamento.Token))
            {
                resultadoLoteAtual.Add(item);
                AdicionarObjeto(item);
                if (resultadoAtual is null && item.Resultado is not null)
                    ApresentarEventos(item.Resultado);
            }
            var falhas = resultadoLoteAtual.Count(item => item.Resultado is null);
            DefinirStatus(falhas == 0 ? "Lote concluído" : $"Lote: {falhas} falha(s)", falhas == 0 ? Color.SeaGreen : Color.DarkOrange);
            logger.LogInformation("Lote finalizado: {Concluidos} objetos tratados, {Falhas} falhas.", resultadoLoteAtual.Count, falhas);
        }
        catch (OperationCanceledException)
        {
            DefinirStatus("Lote cancelado", Color.SlateGray);
            logger.LogInformation("Lote cancelado; {Quantidade} resultados anteriores preservados.", resultadoLoteAtual.Count);
        }
        finally
        {
            operacaoAtual = null;
            if (resultadoLoteAtual.Count > 0)
            {
                campoJson.Text = exportacao.SerializarLote(resultadoLoteAtual);
                logger.LogInformation("JSON de lote gerado com {Quantidade} resultados independentes.", resultadoLoteAtual.Count);
            }
            contadorEventos.Text = $"RESULTADO  ·  {resultadoLoteAtual.Count}/{codigos.Count} objetos tratados";
            DefinirProcessando(false);
            if (fechando)
                Close();
        }
    }

    private void AdicionarObjeto(ResultadoConsultaObjeto item)
    {
        listaObjetos.Items.Add(new ListViewItem([item.CodigoInformado,
            item.Resultado is null ? "Falha" : "Concluído",
            item.Resultado is null ? item.Erro ?? "Falha" : $"{item.Resultado.Eventos.Count} eventos"])
        {
            Tag = item,
            ToolTipText = item.Erro ?? item.Resultado!.CodigoRastreamento
        });
    }

    private void ApresentarEventos(ResultadoRastreamento resultado)
    {
        resultadoAtual = resultado;
        listaEventos.Items.Clear();
        foreach (var evento in resultado.Eventos)
            listaEventos.Items.Add(new ListViewItem([evento.DataHora?.ToString("dd/MM/yyyy HH:mm") ?? "Não informada",
                evento.Situacao, evento.Local ?? evento.Origem ?? evento.Destino ?? ""]));
        contadorEventos.Text = $"RESULTADO  ·  {resultado.CodigoRastreamento} · {resultado.Eventos.Count} eventos";
        AtualizarMensagemEventos();
    }

    private void AoSelecionarObjeto(object? sender, EventArgs e)
    {
        if (listaObjetos.SelectedItems.Count == 0 || listaObjetos.SelectedItems[0].Tag is not ResultadoConsultaObjeto item)
            return;
        if (item.Resultado is not null)
            ApresentarEventos(item.Resultado);
        else
        {
            resultadoAtual = null;
            listaEventos.Items.Clear();
            AtualizarMensagemEventos();
            contadorEventos.Text = $"RESULTADO  ·  {item.CodigoInformado} · {item.Erro}";
        }
    }

    private string ObterJsonAtual() => resultadoLoteAtual is not null
        ? exportacao.SerializarLote(resultadoLoteAtual) : exportacao.Serializar(resultadoAtual!);

    private void AjustarColunasObjetos()
    {
        if (listaObjetos.IsDisposed || listaObjetos.Columns.Count != 3 || listaObjetos.ClientSize.Width <= 0)
            return;
        var largura = Math.Max(0, listaObjetos.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        listaObjetos.Columns[0].Width = Math.Max(110, largura * 3 / 10);
        listaObjetos.Columns[1].Width = Math.Max(75, largura / 5);
        listaObjetos.Columns[2].Width = Math.Max(100, largura - listaObjetos.Columns[0].Width - listaObjetos.Columns[1].Width);
    }
}
