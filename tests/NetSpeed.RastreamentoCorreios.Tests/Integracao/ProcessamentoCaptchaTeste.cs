using NetSpeed.RastreamentoCorreios.Interfaces;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Tests.Integracao;

internal sealed class ProcessamentoCaptchaTeste(Func<byte[], CancellationToken, Task<ResultadoOcrCaptcha>> reconhecer) : IProcessamentoCaptchaService
{
    public Task<ResultadoOcrCaptcha> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken) => reconhecer(imagem, cancellationToken);
}
