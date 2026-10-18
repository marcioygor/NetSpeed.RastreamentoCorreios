namespace NetSpeed.RastreamentoCorreios.Infraestrutura;

public static class RecursosAplicacao
{
    public static string LerTexto(string nome)
    {
        using var fluxo = typeof(RecursosAplicacao).Assembly.GetManifestResourceStream($"NetSpeed.RastreamentoCorreios.{nome}")
            ?? throw new InvalidOperationException($"Recurso da aplicação não encontrado: {nome}.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}