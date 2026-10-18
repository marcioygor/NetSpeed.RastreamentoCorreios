using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Interfaces;

public interface IProcessamentoCaptchaService
{
    Task<ResultadoOcrCaptcha> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken);
}