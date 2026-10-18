using System.Text.Json;

namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record ConfiguracaoAutomacao
{
    public Uri UrlPortal { get; init; } = new("https://rastreamento.correios.com.br/app/index.php");
    public int TimeoutNavegacaoSegundos { get; init; } = 45;
    public int TimeoutElementoSegundos { get; init; } = 20;
    public int TimeoutOcrSegundos { get; init; } = 20;
    public int IntervaloVerificacaoMilissegundos { get; init; } = 200;
    public float ConfiancaMinimaCaptcha { get; init; } = 0.45f;
    public int MinimoCaracteresCaptcha { get; init; } = 4;
    public int MaximoCaracteresCaptcha { get; init; } = 8;
    public string CaracteresCaptcha { get; init; } = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public int MaximoTentativas { get; init; } = 10;
    public int IntervaloTentativasMilissegundos { get; init; } = 1000;

    public static ConfiguracaoAutomacao Carregar(string caminho)
    {
        var configuracao = JsonSerializer.Deserialize<ConfiguracaoAutomacao>(
            File.ReadAllText(caminho), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("O arquivo de configuração está vazio.");
        configuracao.Validar();
        return configuracao;
    }

    public void Validar()
    {
        if (UrlPortal is null || !UrlPortal.IsAbsoluteUri || UrlPortal.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("A URL do portal deve ser um endereço HTTPS absoluto.");
        if (TimeoutNavegacaoSegundos is < 1 or > 300 || TimeoutElementoSegundos is < 1 or > 300 || TimeoutOcrSegundos is < 1 or > 300)
            throw new InvalidDataException("Os timeouts devem estar entre 1 e 300 segundos.");
        if (IntervaloVerificacaoMilissegundos is < 50 or > 5000)
            throw new InvalidDataException("O intervalo de verificação deve estar entre 50 e 5000 ms.");
        if (!float.IsFinite(ConfiancaMinimaCaptcha) || ConfiancaMinimaCaptcha is < 0 or > 1)
            throw new InvalidDataException("A confiança mínima do OCR deve estar entre zero e um.");
        if (MinimoCaracteresCaptcha < 1 || MaximoCaracteresCaptcha > 12 || MinimoCaracteresCaptcha > MaximoCaracteresCaptcha || string.IsNullOrWhiteSpace(CaracteresCaptcha) || CaracteresCaptcha.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new InvalidDataException("O formato configurado para o CAPTCHA é inválido.");
        if (MaximoTentativas is < 1 or > 10 || IntervaloTentativasMilissegundos is < 100 or > 30000)
            throw new InvalidDataException("A configuração de tentativas está fora dos limites permitidos.");
    }
}
