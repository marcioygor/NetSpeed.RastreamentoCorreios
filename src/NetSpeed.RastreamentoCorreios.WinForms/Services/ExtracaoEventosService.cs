using CefSharp.WinForms;
using NetSpeed.RastreamentoCorreios.Infraestrutura;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class ExtracaoEventosService(JavascriptCorreiosService javascript)
{
    private static readonly string ScriptExtracao = RecursosAplicacao.LerTexto("Scripts.ExtrairEventos.js");

    public async Task<IReadOnlyList<EventoRastreamento>> ExtrairEventosAsync(ChromiumWebBrowser navegador, CancellationToken cancellationToken)
    {
        var eventos = await javascript.ExecutarAsync<List<EventoRastreamento>>(navegador,
            ScriptExtracao, cancellationToken);
        if (eventos is null || eventos.Count == 0)
            throw new InvalidDataException("Nenhum evento foi extraído do histórico.");
        return eventos;
    }
}