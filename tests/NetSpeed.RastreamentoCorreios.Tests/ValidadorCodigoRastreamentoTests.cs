using NetSpeed.RastreamentoCorreios.Validacoes;
using Xunit;

namespace NetSpeed.RastreamentoCorreios.Tests;

public sealed class ValidadorCodigoRastreamentoTests
{
    [Theory]
    [InlineData("NN437873753BR", "NN437873753BR")]
    [InlineData("  nn437873753br  ", "NN437873753BR")]
    [InlineData("LX123456789US", "LX123456789US")]
    public void NormalizaCodigosNoFormatoEsperado(string entrada, string esperado)
    {
        Assert.Equal(esperado, ValidadorCodigoRastreamento.ValidarCodigo(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NN43787375BR")]
    [InlineData("NN4378737533BR")]
    [InlineData("NN437873753BR\nINDEVIDO")]
    [InlineData("ÑN437873753BR")]
    [InlineData("NN٤٣٧٨٧٣٧٥٣BR")]
    [InlineData("NN 437873753BR")]
    public void RejeitaFormatoInvalido(string? entrada)
    {
        Assert.Throws<ArgumentException>(() => ValidadorCodigoRastreamento.ValidarCodigo(entrada));
    }
}