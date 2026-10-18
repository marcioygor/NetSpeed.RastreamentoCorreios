using System.Text.RegularExpressions;

namespace NetSpeed.RastreamentoCorreios.Validacoes;

public static partial class ValidadorCodigoRastreamento
{
    public const string MensagemFormatoInvalido = "Informe duas letras, nove números e duas letras. Exemplo: AA123456789BR.";

    public static string ValidarCodigo(string? codigoRastreamento)
    {
        var codigoNormalizado = codigoRastreamento?.Trim().ToUpperInvariant();
        if (codigoNormalizado is null || !FormatoCodigo().IsMatch(codigoNormalizado))
            throw new ArgumentException(MensagemFormatoInvalido, nameof(codigoRastreamento));
        return codigoNormalizado;
    }

    [GeneratedRegex(@"\A[A-Z]{2}[0-9]{9}[A-Z]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoCodigo();
}