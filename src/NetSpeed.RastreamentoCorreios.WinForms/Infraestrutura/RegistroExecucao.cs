using Microsoft.Extensions.Logging;

namespace NetSpeed.RastreamentoCorreios.Infraestrutura;

internal sealed class RegistroExecucao(RegistroExecucaoProvider provider, string categoria) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
            provider.Registrar(logLevel, categoria, formatter(state, exception), exception);
    }
}