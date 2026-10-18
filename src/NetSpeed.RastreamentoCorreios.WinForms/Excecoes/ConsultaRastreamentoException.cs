namespace NetSpeed.RastreamentoCorreios.Excecoes;

public sealed class ConsultaRastreamentoException(string mensagem, string tipoErro = "portal",
    string? detalheTecnico = null, Exception? causa = null) : InvalidOperationException(detalheTecnico ?? mensagem, causa)
{
    public string MensagemUsuario { get; } = mensagem;
    public string TipoErro { get; } = tipoErro;

    public static ConsultaRastreamentoException PorLimiteRequisicoes(string detalheTecnico, Exception? causa = null) => new(
        "O portal dos Correios limitou temporariamente as consultas. Tente novamente mais tarde.",
        "limite_requisicoes", detalheTecnico, causa);
}
