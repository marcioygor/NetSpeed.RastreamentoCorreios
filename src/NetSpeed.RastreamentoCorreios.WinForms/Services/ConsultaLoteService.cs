using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Excecoes;
using NetSpeed.RastreamentoCorreios.Modelos;
using NetSpeed.RastreamentoCorreios.Validacoes;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class ConsultaLoteService(ILogger<ConsultaLoteService> logger)
{
    public static IReadOnlyList<string> SepararCodigos(string entrada) => entrada.Split(
        [';', ',', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async IAsyncEnumerable<ResultadoConsultaObjeto> ConsultarAsync(IReadOnlyList<string> codigos,
        Func<string, CancellationToken, Task<ResultadoRastreamento>> consultar,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resultadosConcluidos = new Dictionary<string, ResultadoRastreamento>(StringComparer.OrdinalIgnoreCase);
        var limiteRequisicoes = false;
        for (var indice = 0; indice < codigos.Count; indice++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var codigoInformado = codigos[indice];
            if (limiteRequisicoes)
            {
                yield return new ResultadoConsultaObjeto
                {
                    CodigoInformado = codigoInformado,
                    TipoErro = "nao_consultado",
                    Erro = "Objeto não consultado porque o portal limitou temporariamente as consultas. Tente novamente mais tarde."
                };
                continue;
            }
            ResultadoConsultaObjeto item;
            var codigoValidado = false;
            try
            {
                var codigo = ValidadorCodigoRastreamento.ValidarCodigo(codigoInformado);
                codigoValidado = true;
                if (!resultadosConcluidos.TryGetValue(codigo, out var resultado))
                {
                    logger.LogInformation("Lote: objeto {Posicao}/{Quantidade}, código {Codigo}.", indice + 1, codigos.Count, codigo);
                    resultado = await consultar(codigo, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    resultadosConcluidos.Add(codigo, resultado);
                }
                else
                    logger.LogInformation("Lote: resultado de {Codigo} reutilizado nesta execução, sem nova consulta ao portal.", codigo);
                cancellationToken.ThrowIfCancellationRequested();
                item = new ResultadoConsultaObjeto { CodigoInformado = codigoInformado, Resultado = resultado };
            }
            catch (Exception excecao) when (excecao is ArgumentException or IOException or TimeoutException or InvalidOperationException or JsonException)
            {
                limiteRequisicoes = excecao is ConsultaRastreamentoException { TipoErro: "limite_requisicoes" };
                if (limiteRequisicoes)
                    logger.LogWarning(excecao, "Lote interrompido por limitação do portal no código {Codigo}; {Quantidade} objetos não serão consultados.",
                        codigoInformado, codigos.Count - indice - 1);
                else
                    logger.LogWarning(excecao, "Lote: falha no código {Codigo}. O próximo objeto será tratado de forma independente.", codigoInformado);
                item = CriarResultadoFalha(codigoInformado, excecao, codigoValidado);
            }
            yield return item;
        }
    }

    private static ResultadoConsultaObjeto CriarResultadoFalha(string codigoInformado, Exception excecao, bool codigoValidado)
    {
        var (tipo, mensagem) = excecao switch
        {
            ArgumentException when !codigoValidado => ("validacao", ValidadorCodigoRastreamento.MensagemFormatoInvalido),
            ConsultaRastreamentoException { TipoErro: "validacao" } => ("validacao", ValidadorCodigoRastreamento.MensagemFormatoInvalido),
            ConsultaRastreamentoException erroPortal => (erroPortal.TipoErro, erroPortal.MensagemUsuario),
            TimeoutException => ("timeout", "A consulta excedeu o tempo configurado."),
            _ => ("automacao", "Não foi possível concluir a consulta. Consulte os logs para os detalhes.")
        };
        return new ResultadoConsultaObjeto
        {
            CodigoInformado = codigoInformado,
            TipoErro = tipo,
            Erro = mensagem
        };
    }
}
