namespace NetSpeed.RastreamentoCorreios.Excecoes;

public sealed class ConsultaRastreamentoException(string mensagem) : InvalidOperationException(mensagem)
{
    public string MensagemUsuario { get; } = mensagem;
}