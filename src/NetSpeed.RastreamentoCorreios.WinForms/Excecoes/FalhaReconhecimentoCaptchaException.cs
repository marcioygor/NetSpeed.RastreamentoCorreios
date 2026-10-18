namespace NetSpeed.RastreamentoCorreios.Excecoes;

public sealed class FalhaReconhecimentoCaptchaException(string mensagem) : InvalidOperationException(mensagem);