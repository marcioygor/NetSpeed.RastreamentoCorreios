namespace NetSpeed.RastreamentoCorreios.Excecoes;

public sealed class ConsultaRastreamentoException(string mensagem) : InvalidOperationException(mensagem);