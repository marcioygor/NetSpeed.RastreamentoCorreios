using System.Drawing.Drawing2D;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NetSpeed.RastreamentoCorreios.Excecoes;
using NetSpeed.RastreamentoCorreios.Interfaces;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class ProcessamentoCaptchaService : IProcessamentoCaptchaService, IDisposable
{
    private readonly ConfiguracaoAutomacao configuracao;
    private readonly ILogger<ProcessamentoCaptchaService> logger;
    private readonly Lazy<InferenceSession> motor;
    private readonly Lazy<string[]> caracteres;
    private readonly SemaphoreSlim exclusao = new(1, 1);

    public ProcessamentoCaptchaService(ConfiguracaoAutomacao configuracao, ILogger<ProcessamentoCaptchaService> logger)
    {
        this.configuracao = configuracao;
        this.logger = logger;
        motor = new(() =>
        {
            using var opcoes = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1 };
            return new InferenceSession(Path.Combine(AppContext.BaseDirectory, "ModelosOcr", "captcha.onnx"), opcoes);
        });
        caracteres = new(() => JsonSerializer.Deserialize<string[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "ModelosOcr", "charset_beta.json")))
            ?? throw new InvalidDataException("O dicionário de caracteres OCR está vazio."));
    }

    public async Task<ResultadoOcrCaptcha> ReconhecerAsync(byte[] imagem, CancellationToken cancellationToken)
    {
        await exclusao.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => Reconhecer(imagem, cancellationToken), cancellationToken);
        }
        catch (OnnxRuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OnnxRuntimeException excecao)
        {
            throw new InvalidOperationException("Falha no motor de reconhecimento OCR local.", excecao);
        }
        finally
        {
            exclusao.Release();
        }
    }

    private ResultadoOcrCaptcha Reconhecer(byte[] imagem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preparacao = new PreProcessamentoImagemService();
        var variantes = new List<VarianteImagemCaptcha> { new("original", imagem) };
        variantes.AddRange(preparacao.PrepararVariantes(imagem).Where(variante => variante.Nome is "cor-predominante" or "cor-tracos"));
        var resultados = new List<ResultadoOcrCaptcha>();
        foreach (var variante in variantes)
        foreach (var centralizar in new[] { false, true })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entrada = PrepararTensor(variante.Conteudo, centralizar);
            using var opcoesExecucao = new RunOptions();
            using var registroCancelamento = cancellationToken.Register(() => opcoesExecucao.Terminate = true);
            var entradas = new[] { NamedOnnxValue.CreateFromTensor(motor.Value.InputMetadata.Keys.Single(), entrada) };
            using var saidas = motor.Value.Run(entradas, motor.Value.OutputMetadata.Keys.ToArray(), opcoesExecucao);
            var resultado = Decodificar(saidas.First().AsTensor<float>(), $"onnx/{variante.Nome}/{(centralizar ? "centrado" : "unitario")}");
            logger.LogDebug("OCR {Variante}: '{Texto}', confiança interna {Confianca:F3}.", resultado.Variante, resultado.Texto, resultado.Confianca);
            if (resultado.Texto.Length >= configuracao.MinimoCaracteresCaptcha && resultado.Texto.Length <= configuracao.MaximoCaracteresCaptcha)
                resultados.Add(resultado);
        }
        var melhor = resultados.MaxBy(resultado => resultado.Confianca);
        if (melhor is null || melhor.Confianca < configuracao.ConfiancaMinimaCaptcha)
            throw new FalhaReconhecimentoCaptchaException("O OCR não reconheceu uma sequência com confiança suficiente nesta imagem.");
        return melhor with { Concordancias = resultados.Count(resultado => resultado.Texto == melhor.Texto) };
    }

    private ResultadoOcrCaptcha Decodificar(Tensor<float> saida, string variante)
    {
        var dimensoes = saida.Dimensions.ToArray();
        if (dimensoes.Length != 3 || dimensoes[^1] != caracteres.Value.Length || (dimensoes[0] != 1 && dimensoes[1] != 1))
            throw new InvalidDataException("As dimensões de saída do modelo OCR não correspondem ao contrato esperado.");
        var classes = dimensoes[^1];
        var valores = saida.ToArray();
        var permitidos = Enumerable.Range(0, classes).Where(indice => indice == 0 ||
            caracteres.Value[indice].Length == 1 && configuracao.CaracteresCaptcha.Contains(caracteres.Value[indice][0])).ToArray();
        var texto = new StringBuilder();
        var confiancas = new List<float>();
        var anterior = 0;
        for (var inicio = 0; inicio < valores.Length; inicio += classes)
        {
            var melhor = permitidos.MaxBy(indice => valores[inicio + indice]);
            if (melhor != 0 && melhor != anterior)
            {
                var maximo = valores.AsSpan(inicio, classes).ToArray().Max();
                double denominador = 0;
                for (var indice = 0; indice < classes; indice++)
                    denominador += Math.Exp(valores[inicio + indice] - maximo);
                var confianca = (float)(Math.Exp(valores[inicio + melhor] - maximo) / denominador);
                texto.Append(caracteres.Value[melhor]);
                confiancas.Add(confianca);
            }
            anterior = melhor;
        }
        return new(texto.ToString(), confiancas.Count > 0 ? confiancas.Average() : 0, variante, 1);
    }

    private static DenseTensor<float> PrepararTensor(byte[] conteudo, bool centralizar)
    {
        using var fluxo = new MemoryStream(conteudo);
        using var original = new Bitmap(fluxo);
        const int altura = 64;
        var largura = Math.Clamp((int)(original.Width * (double)altura / original.Height), 16, 2048);
        using var redimensionada = new Bitmap(largura, altura);
        using (var desenho = Graphics.FromImage(redimensionada))
        {
            desenho.Clear(Color.White);
            desenho.InterpolationMode = InterpolationMode.HighQualityBicubic;
            desenho.DrawImage(original, new Rectangle(0, 0, largura, altura));
        }
        var tensor = new DenseTensor<float>([1, 1, altura, largura]);
        for (var y = 0; y < altura; y++)
        for (var x = 0; x < largura; x++)
        {
            var pixel = redimensionada.GetPixel(x, y);
            var cinza = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
            tensor[0, 0, y, x] = centralizar ? 2 * cinza - 1 : cinza;
        }
        return tensor;
    }

    public void Dispose()
    {
        if (motor.IsValueCreated)
            motor.Value.Dispose();
        exclusao.Dispose();
    }
}
