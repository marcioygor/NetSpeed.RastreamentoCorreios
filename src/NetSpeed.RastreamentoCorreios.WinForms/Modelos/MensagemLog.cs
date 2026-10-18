using Microsoft.Extensions.Logging;

namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record MensagemLog(DateTimeOffset DataHora, LogLevel Nivel, string Texto);