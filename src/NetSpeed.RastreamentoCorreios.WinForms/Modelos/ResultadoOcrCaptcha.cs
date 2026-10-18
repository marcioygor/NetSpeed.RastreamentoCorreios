namespace NetSpeed.RastreamentoCorreios.Modelos;

public sealed record ResultadoOcrCaptcha(string Texto, float Confianca, string Variante, int Concordancias);