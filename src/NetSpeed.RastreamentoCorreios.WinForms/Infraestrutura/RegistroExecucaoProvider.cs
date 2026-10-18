using Microsoft.Extensions.Logging;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Infraestrutura;

public sealed class RegistroExecucaoProvider(string diretorio) : ILoggerProvider
{
    private readonly object sincronizacao = new();
    private readonly Queue<MensagemLog> historico = new();
    private bool descartado;

    public event Action<MensagemLog>? MensagemRegistrada;

    public IReadOnlyList<MensagemLog> ObterHistorico()
    {
        lock (sincronizacao)
            return historico.ToArray();
    }

    public ILogger CreateLogger(string categoryName) => new RegistroExecucao(this, categoryName);

    internal void Registrar(LogLevel nivel, string categoria, string texto, Exception? excecao)
    {
        var mensagem = new MensagemLog(DateTimeOffset.Now, nivel, texto);
        lock (sincronizacao)
        {
            if (descartado)
                return;
            try
            {
                Directory.CreateDirectory(diretorio);
                var caminho = Path.Combine(diretorio, $"execucao-{mensagem.DataHora:yyyy-MM-dd}-{Environment.ProcessId}.log");
                File.AppendAllText(caminho, $"{mensagem.DataHora:O} [{nivel}] {categoria}: {texto}{Environment.NewLine}{(excecao is null ? "" : excecao + Environment.NewLine)}");
            }
            catch (Exception falha) when (falha is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Falha ao gravar log: {falha.Message}");
                mensagem = mensagem with { Texto = texto + " [Não foi possível gravar o log em disco.]" };
            }
            historico.Enqueue(mensagem);
            while (historico.Count > 500)
                historico.Dequeue();
        }
        MensagemRegistrada?.Invoke(mensagem);
    }

    public void Dispose()
    {
        lock (sincronizacao)
        {
            descartado = true;
            MensagemRegistrada = null;
        }
    }
}
