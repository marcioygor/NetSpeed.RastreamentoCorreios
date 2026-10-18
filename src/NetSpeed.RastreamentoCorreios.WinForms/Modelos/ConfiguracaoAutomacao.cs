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
        ValidarUrlPortal();
        ValidarTimeouts();
        ValidarIntervaloVerificacao();
        ValidarConfiancaCaptcha();
        ValidarFormatoCaptcha();
        ValidarTentativas();
    }

    private void ValidarUrlPortal()
    {
        if (UrlPortal is null || !UrlPortal.IsAbsoluteUri)
            throw new InvalidDataException("A URL do portal deve ser um endereço absoluto.");
        if (UrlPortal.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("A URL do portal deve utilizar HTTPS.");
    }

    private void ValidarTimeouts()
    {
        ValidarIntervalo(TimeoutNavegacaoSegundos, 1, 300, "O timeout de navegação deve estar entre 1 e 300 segundos.");
        ValidarIntervalo(TimeoutElementoSegundos, 1, 300, "O timeout de espera dos elementos deve estar entre 1 e 300 segundos.");
        ValidarIntervalo(TimeoutOcrSegundos, 1, 300, "O timeout do OCR deve estar entre 1 e 300 segundos.");
    }

    private void ValidarIntervaloVerificacao() => ValidarIntervalo(IntervaloVerificacaoMilissegundos, 50, 5000,
        "O intervalo de verificação deve estar entre 50 e 5000 ms.");

    private void ValidarConfiancaCaptcha()
    {
        if (!float.IsFinite(ConfiancaMinimaCaptcha))
            throw new InvalidDataException("A confiança mínima do OCR deve ser um número finito.");
        if (ConfiancaMinimaCaptcha is < 0 or > 1)
            throw new InvalidDataException("A confiança mínima do OCR deve estar entre zero e um.");
    }

    private void ValidarFormatoCaptcha()
    {
        if (MinimoCaracteresCaptcha < 1)
            throw new InvalidDataException("O número mínimo de caracteres do CAPTCHA deve ser pelo menos 1.");
        if (MaximoCaracteresCaptcha > 12)
            throw new InvalidDataException("O número máximo de caracteres do CAPTCHA não pode exceder 12.");
        if (MinimoCaracteresCaptcha > MaximoCaracteresCaptcha)
            throw new InvalidDataException("O número mínimo de caracteres do CAPTCHA não pode exceder o máximo.");
        if (string.IsNullOrWhiteSpace(CaracteresCaptcha))
            throw new InvalidDataException("Informe os caracteres permitidos no CAPTCHA.");
        if (!CaracteresCaptcha.All(char.IsAsciiLetterOrDigit))
            throw new InvalidDataException("Os caracteres permitidos no CAPTCHA devem conter apenas letras ASCII e números.");
    }

    private void ValidarTentativas()
    {
        ValidarIntervalo(MaximoTentativas, 1, 10, "O número máximo de tentativas deve estar entre 1 e 10.");
        ValidarIntervalo(IntervaloTentativasMilissegundos, 100, 30000, "O intervalo entre tentativas deve estar entre 100 e 30000 ms.");
    }

    private static void ValidarIntervalo(int valor, int minimo, int maximo, string mensagem)
    {
        if (valor < minimo || valor > maximo)
            throw new InvalidDataException(mensagem);
    }
}
