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
        for (var indice = 0; indice < codigos.Count; indice++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var codigoInformado = codigos[indice];
            ResultadoConsultaObjeto item;
            var codigoValidado = false;
            try
            {
                var codigo = ValidadorCodigoRastreamento.ValidarCodigo(codigoInformado);
                codigoValidado = true;
                logger.LogInformation("Lote: objeto {Posicao}/{Quantidade}, código {Codigo}.", indice + 1, codigos.Count, codigo);
                var resultado = await consultar(codigo, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                item = new ResultadoConsultaObjeto { CodigoInformado = codigoInformado, Resultado = resultado };
            }
            catch (Exception excecao) when (excecao is ArgumentException or IOException or TimeoutException or InvalidOperationException or JsonException)
            {
                logger.LogWarning(excecao, "Lote: falha no código {Codigo}. O próximo objeto será tratado de forma independente.", codigoInformado);
                var tipo = excecao switch
                {
                    ArgumentException when !codigoValidado => "validacao",
                    ConsultaRastreamentoException => "portal",
                    TimeoutException => "timeout",
                    _ => "automacao"
                };
                item = new ResultadoConsultaObjeto
                {
                    CodigoInformado = codigoInformado,
                    TipoErro = tipo,
                    Erro = tipo == "validacao" ? ValidadorCodigoRastreamento.MensagemFormatoInvalido
                        : excecao is ConsultaRastreamentoException consultaExcecao ? consultaExcecao.MensagemUsuario
                        : tipo == "timeout" ? "A consulta excedeu o tempo configurado."
                        : "Não foi possível concluir a consulta. Consulte os logs para os detalhes."
                };
            }
            yield return item;
        }
    }
}
