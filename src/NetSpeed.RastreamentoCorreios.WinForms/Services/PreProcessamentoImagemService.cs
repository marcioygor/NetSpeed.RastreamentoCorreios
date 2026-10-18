using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NetSpeed.RastreamentoCorreios.Modelos;

namespace NetSpeed.RastreamentoCorreios.Services;

public sealed class PreProcessamentoImagemService
{
    public IReadOnlyList<VarianteImagemCaptcha> PrepararVariantes(byte[] imagem)
    {
        if (imagem.Length is 0 or > 2_000_000)
            throw new InvalidDataException("A imagem do CAPTCHA está vazia ou excede o tamanho permitido.");
        using var fluxo = new MemoryStream(imagem);
        using var original = new Bitmap(fluxo);
        if (original.Width is < 10 or > 1200 || original.Height is < 10 or > 500)
            throw new InvalidDataException("As dimensões da imagem do CAPTCHA não são suportadas.");

        var variantes = new List<VarianteImagemCaptcha>
        {
            new("original-ampliada", Ampliar(original))
        };
        using var corPredominante = IsolarCorPredominante(original);
        RemoverPontosIsolados(corPredominante);
        variantes.Add(new("cor-predominante", Ampliar(corPredominante)));
        foreach (var vertical in new[] { true, false })
        {
            using var reconstruida = ReconstruirTracos(corPredominante, vertical);
            variantes.Add(new(vertical ? "cor-tracos-verticais" : "cor-tracos", Ampliar(reconstruida)));
        }
        foreach (var limiar in new[] { 160, 200 })
        {
            using var binaria = Binarizar(original, limiar);
            RemoverPontosIsolados(binaria);
            variantes.Add(new($"binaria-{limiar}", Ampliar(binaria)));
            using var limpa = AplicarAbertura(binaria);
            RemoverPontosIsolados(limpa);
            variantes.Add(new($"abertura-3x3-{limiar}", Ampliar(limpa)));
        }
        return variantes;
    }

    private static Bitmap ReconstruirTracos(Bitmap imagem, bool somenteVertical)
    {
        var dilatada = new bool[imagem.Width, imagem.Height];
        var alcanceHorizontal = somenteVertical ? 0 : 1;
        for (var y = 1; y < imagem.Height - 1; y++)
        for (var x = 1; x < imagem.Width - 1; x++)
        {
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -alcanceHorizontal; dx <= alcanceHorizontal; dx++)
                if (imagem.GetPixel(x + dx, y + dy).R == 0)
                    dilatada[x, y] = true;
        }
        var reconstruida = new Bitmap(imagem.Width, imagem.Height, PixelFormat.Format24bppRgb);
        using (var desenho = Graphics.FromImage(reconstruida))
            desenho.Clear(Color.White);
        for (var y = 1; y < imagem.Height - 1; y++)
        for (var x = 1; x < imagem.Width - 1; x++)
        {
            var preenchido = true;
            for (var dy = -1; dy <= 1 && preenchido; dy++)
            for (var dx = -alcanceHorizontal; dx <= alcanceHorizontal; dx++)
                if (!dilatada[x + dx, y + dy])
                {
                    preenchido = false;
                    break;
                }
            if (preenchido)
                reconstruida.SetPixel(x, y, Color.Black);
        }
        return reconstruida;
    }
    private static Bitmap IsolarCorPredominante(Bitmap original)
    {
        var histograma = new int[256];
        for (var y = 0; y < original.Height; y++)
        for (var x = 0; x < original.Width; x++)
        {
            var pixel = original.GetPixel(x, y);
            if (pixel.A >= 128)
                histograma[(pixel.R + pixel.G + pixel.B) / 3]++;
        }
        var tom = Enumerable.Range(0, 226).MaxBy(indice => histograma[indice]);
        var resultado = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < original.Height; y++)
        for (var x = 0; x < original.Width; x++)
        {
            var pixel = original.GetPixel(x, y);
            var luminancia = (pixel.R + pixel.G + pixel.B) / 3;
            resultado.SetPixel(x, y, pixel.A >= 128 && Math.Abs(luminancia - tom) <= 8 ? Color.Black : Color.White);
        }
        return resultado;
    }
    private static Bitmap Binarizar(Bitmap original, int limiar)
    {
        var imagem = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < original.Height; y++)
        for (var x = 0; x < original.Width; x++)
        {
            var pixel = original.GetPixel(x, y);
            var luminancia = (299 * pixel.R + 587 * pixel.G + 114 * pixel.B) / 1000;
            imagem.SetPixel(x, y, pixel.A < 128 || luminancia >= limiar ? Color.White : Color.Black);
        }
        return imagem;
    }

    private static void RemoverPontosIsolados(Bitmap imagem)
    {
        var visitados = new bool[imagem.Width, imagem.Height];
        var fila = new Queue<Point>();
        var componente = new List<Point>();
        for (var y = 0; y < imagem.Height; y++)
        for (var x = 0; x < imagem.Width; x++)
        {
            if (visitados[x, y] || imagem.GetPixel(x, y).R != 0)
                continue;
            fila.Enqueue(new Point(x, y));
            visitados[x, y] = true;
            componente.Clear();
            while (fila.TryDequeue(out var ponto))
            {
                componente.Add(ponto);
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var vizinhoX = ponto.X + dx;
                    var vizinhoY = ponto.Y + dy;
                    if (vizinhoX < 0 || vizinhoY < 0 || vizinhoX >= imagem.Width || vizinhoY >= imagem.Height || visitados[vizinhoX, vizinhoY])
                        continue;
                    visitados[vizinhoX, vizinhoY] = true;
                    if (imagem.GetPixel(vizinhoX, vizinhoY).R == 0)
                        fila.Enqueue(new Point(vizinhoX, vizinhoY));
                }
            }
            if (componente.Count <= 8)
                foreach (var ponto in componente)
                    imagem.SetPixel(ponto.X, ponto.Y, Color.White);
        }
    }

    private static Bitmap AplicarAbertura(Bitmap imagem)
    {
        var erodida = new bool[imagem.Width, imagem.Height];
        for (var y = 1; y < imagem.Height - 1; y++)
        for (var x = 1; x < imagem.Width - 1; x++)
        {
            var preenchido = true;
            for (var dy = -1; dy <= 1 && preenchido; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (imagem.GetPixel(x + dx, y + dy).R != 0)
                {
                    preenchido = false;
                    break;
                }
            erodida[x, y] = preenchido;
        }
        var resultado = new Bitmap(imagem.Width, imagem.Height, PixelFormat.Format24bppRgb);
        using (var desenho = Graphics.FromImage(resultado))
            desenho.Clear(Color.White);
        for (var y = 1; y < imagem.Height - 1; y++)
        for (var x = 1; x < imagem.Width - 1; x++)
            if (erodida[x, y])
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    resultado.SetPixel(x + dx, y + dy, Color.Black);
        return resultado;
    }
    private static byte[] Ampliar(Bitmap original)
    {
        const int escala = 3;
        const int margem = 24;
        var esquerda = original.Width;
        var direita = -1;
        var topo = original.Height;
        var baseImagem = -1;
        for (var y = 0; y < original.Height; y++)
        for (var x = 0; x < original.Width; x++)
        {
            var pixel = original.GetPixel(x, y);
            if (pixel.A < 128 || (pixel.R + pixel.G + pixel.B) / 3 >= 230)
                continue;
            esquerda = Math.Min(esquerda, x); direita = Math.Max(direita, x);
            topo = Math.Min(topo, y); baseImagem = Math.Max(baseImagem, y);
        }
        var recorte = direita >= esquerda && baseImagem >= topo
            ? Rectangle.FromLTRB(esquerda, topo, direita + 1, baseImagem + 1)
            : new Rectangle(0, 0, original.Width, original.Height);
        using var ampliada = new Bitmap(recorte.Width * escala + margem * 2, recorte.Height * escala + margem * 2);
        ampliada.SetResolution(300, 300);
        using (var desenho = Graphics.FromImage(ampliada))
        {
            desenho.Clear(Color.White);
            desenho.InterpolationMode = InterpolationMode.HighQualityBicubic;
            desenho.DrawImage(original, new Rectangle(margem, margem, recorte.Width * escala, recorte.Height * escala), recorte, GraphicsUnit.Pixel);
        }
        using var fluxo = new MemoryStream();
        ampliada.Save(fluxo, ImageFormat.Png);
        return fluxo.ToArray();
    }
}