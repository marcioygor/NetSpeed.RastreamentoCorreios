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
        LimparEventosConsulta();
        LimparJsonConsulta();
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
        catch (Exception excecao)
        {
            logger.LogError(excecao, "Falha ao consultar o lote; {Quantidade} resultados anteriores preservados.", resultadoLoteAtual.Count);
            DefinirStatus("Falha na consulta do lote", Color.Firebrick);
            if (!fechando)
                MessageBox.Show(this, "Não foi possível concluir o lote. Os resultados já obtidos foram preservados. Tente novamente mais tarde.",
                    "Lote não concluído", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            operacaoAtual = null;
            if (resultadoLoteAtual.Count > 0)
            {
                logger.LogInformation("JSON de lote gerado com {Quantidade} resultados independentes.", resultadoLoteAtual.Count);
            }
            contadorEventos.Text = $"RESULTADO  ·  {resultadoLoteAtual.Count}/{codigos.Count} objetos tratados";
            DefinirProcessando(false);
            AtualizarSeletorEventos();
            AtualizarSeletorJson();
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
        var indice = seletorEventos.Items.IndexOf(item);
        if (indice >= 0 && seletorEventos.SelectedIndex != indice)
        {
            seletorEventos.SelectedIndex = indice;
            return;
        }
        ApresentarObjetoSelecionado(item);
    }

    private void ApresentarObjetoSelecionado(ResultadoConsultaObjeto item)
    {
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

    private void LimparEventosConsulta()
    {
        seletorEventos.Items.Clear();
        seletorEventos.Enabled = false;
        DefinirVisibilidadeSeletor(seletorEventos, areaEventos, false);
        listaEventos.Items.Clear();
        contadorEventos.Text = "RESULTADO  ·  Sem resultado";
        AtualizarMensagemEventos();
    }

    private void AtualizarSeletorEventos()
    {
        seletorEventos.BeginUpdate();
        try
        {
            seletorEventos.Items.Clear();
            if (resultadoLoteAtual is not null)
                seletorEventos.Items.AddRange(resultadoLoteAtual.ToArray());
            else if (resultadoAtual is not null)
                seletorEventos.Items.Add(new ResultadoConsultaObjeto
                {
                    CodigoInformado = resultadoAtual.CodigoRastreamento,
                    Resultado = resultadoAtual
                });
            seletorEventos.Enabled = operacaoAtual is null && seletorEventos.Items.Count > 1;
            DefinirVisibilidadeSeletor(seletorEventos, areaEventos, seletorEventos.Items.Count > 1);
            if (seletorEventos.Items.Count > 0)
                seletorEventos.SelectedIndex = 0;
        }
        finally
        {
            seletorEventos.EndUpdate();
        }
    }

    private void AoSelecionarCodigoEventos(object? sender, EventArgs e)
    {
        if (seletorEventos.SelectedItem is ResultadoConsultaObjeto item)
            ApresentarObjetoSelecionado(item);
    }

    private void LimparJsonConsulta()
    {
        seletorJson.Items.Clear();
        seletorJson.Enabled = false;
        DefinirVisibilidadeSeletor(seletorJson, areaJson, false);
        campoJson.Clear();
    }

    private void AtualizarSeletorJson()
    {
        seletorJson.BeginUpdate();
        try
        {
            LimparJsonConsulta();
            if (resultadoLoteAtual is { Count: > 0 })
            {
                seletorJson.Items.Add("Todos os objetos (lote completo)");
                seletorJson.Items.AddRange(resultadoLoteAtual.ToArray());
            }
            else if (resultadoLoteAtual is null && resultadoAtual is not null)
            {
                seletorJson.Items.Add("Todos os objetos (lote completo)");
                seletorJson.Items.Add(new ResultadoConsultaObjeto
                {
                    CodigoInformado = resultadoAtual.CodigoRastreamento,
                    Resultado = resultadoAtual
                });
            }
            DefinirVisibilidadeSeletor(seletorJson, areaJson, seletorJson.Items.Count > 2);
            if (seletorJson.Items.Count > 0)
                seletorJson.SelectedIndex = 0;
            AtualizarBotoes();
        }
        finally
        {
            seletorJson.EndUpdate();
        }
    }

    private void AoSelecionarCodigoJson(object? sender, EventArgs e)
    {
        if (seletorJson.SelectedItem is ResultadoConsultaObjeto item)
        {
            campoJson.Text = item.Resultado is not null
                ? exportacao.Serializar(item.Resultado)
                : exportacao.SerializarLote([item]);
        }
        else if (seletorJson.SelectedIndex == 0 && resultadoLoteAtual is { Count: > 0 })
            campoJson.Text = exportacao.SerializarLote(resultadoLoteAtual);
        else if (seletorJson.SelectedIndex == 0 && resultadoAtual is not null)
            campoJson.Text = exportacao.Serializar(resultadoAtual);
        else
            campoJson.Clear();
    }

    private static void DefinirVisibilidadeSeletor(ComboBox seletor, TableLayoutPanel painel, bool visivel)
    {
        seletor.Visible = visivel;
        painel.RowStyles[0].SizeType = visivel ? SizeType.AutoSize : SizeType.Absolute;
        painel.RowStyles[0].Height = 0;
    }

    private string ObterJsonAtual() => campoJson.Text;

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
