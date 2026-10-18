using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

// Enums aparecem como texto no JSON ("Suv" em vez de 2)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Banco de Memória
var carros = new List<Carro>
{
    new ("BRA2E16", "Onix LT",   120, Categoria.Economico),
    new ("ABC1D23", "HB20",      110, Categoria.Economico),
    new ("XYZ1234", "Renegade",  250, Categoria.Suv),
    new ("ACD2H18", "Creta",     200, Categoria.Suv),
    new ("BCA3E19", "T-Cross",   220, Categoria.Suv),
    new ("DEF4G20", "HR-V",      230, Categoria.Suv),
    new ("GHI5J21", "Kicks",     180, Categoria.Suv),
    new ("JKL6K22", "Compass",   270, Categoria.Suv),
    new ("MNO7L23", "Corolla",   300, Categoria.Executivo)
};

// ───────────────────────── Funções auxiliares ─────────────────────────

Carro? Buscar(string placa) =>
    carros.FirstOrDefault(c => c.Placa.Equals(placa, StringComparison.OrdinalIgnoreCase));

double Ocupacao(Categoria cat)
{
    var frota = carros.Where(c => c.Categoria == cat).ToList();
    return frota.Count == 0 ? 0 : (double)frota.Count(c => !c.Disponivel) / frota.Count;
}

decimal TarifaCategoria(Categoria cat)
{
    var frota = carros.Where(c => c.Categoria == cat).ToList();
    return frota.Count == 0 ? 0 : Math.Round(frota.Average(c => c.ValorDiaria), 2);
}

(Carro? Alocado, bool Upgrade) Alocar(Categoria pedida)
{
    for (var cat = pedida; cat <= Categoria.Executivo; cat++)
    {
        var carro = carros
            .Where(c => c.Categoria == cat && c.Disponivel)
            .OrderBy(c => c.ValorDiaria)
            .FirstOrDefault();

        if (carro is not null)
            return (carro, cat != pedida);
    }
    return (null, false);
}

bool TentarCategoria(string? texto, out Categoria categoria)
{
    categoria = default;
    if (string.IsNullOrWhiteSpace(texto)) return false;

    var semAcento = new string(texto.Trim().Normalize(NormalizationForm.FormD)
        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
        .ToArray());

    return Enum.TryParse(semAcento, true, out categoria) && Enum.IsDefined(categoria);
}

bool Contratar(Carro carro, string cliente, DateTime inicio, int dias, Cotacao cotacao)
{
    var imediato = inicio.Date <= DateTime.Now.Date;

    carro.Disponivel = false;
    carro.ClienteAtual = cliente;
    carro.DiasContratados = dias;
    carro.DiariaContratada = cotacao.DiariaMedia;
    carro.DataAluguel = imediato ? inicio : null;
    carro.DataReserva = imediato ? null : inicio;

    return imediato;
}

// ───────────────────────── Endpoints ─────────────────────────

app.MapGet("/", () => "API Localiza Clone rodando! Acesse /carros");
app.MapGet("/carros", () => carros);
app.MapGet("/carros/disponiveis", () => carros.Where(c => c.Disponivel));

app.MapGet("/carros/{placa}/preco", (string placa, DateTime inicio, DateTime fim) =>
{
    var carro = Buscar(placa);
    if (carro == null) return Results.NotFound(new { Erro = "Carro não encontrado" });

    var dias = (fim.Date - inicio.Date).Days;
    if (dias <= 0) return Results.BadRequest(new { Erro = "A data final deve ser posterior à data inicial" });

    var cotacao = Precificacao.Calcular(carro.ValorDiaria, inicio, dias, Ocupacao(carro.Categoria), DateTime.Now);
    return Results.Ok(new { carro.Placa, carro.Modelo, carro.Categoria, Cotacao = cotacao });
});

app.MapGet("/categorias", () =>
    Enum.GetValues<Categoria>().Select(cat => new
    {
        Categoria = cat,
        TarifaBase = TarifaCategoria(cat),
        TotalCarros = carros.Count(c => c.Categoria == cat),
        Disponiveis = carros.Count(c => c.Categoria == cat && c.Disponivel),
        Ocupacao = $"{Ocupacao(cat) * 100:F1}%"
    }));

app.MapGet("/cotacao", (string categoria, DateTime inicio, DateTime fim) =>
{
    if (!TentarCategoria(categoria, out var cat))
        return Results.BadRequest(new { Erro = "Categoria inválida. Use: Economico, Suv ou Executivo" });

    var dias = (fim.Date - inicio.Date).Days;
    if (dias <= 0) return Results.BadRequest(new { Erro = "A data final deve ser posterior à data inicial" });

    var cotacao = Precificacao.Calcular(TarifaCategoria(cat), inicio, dias, Ocupacao(cat), DateTime.Now);
    var (alocado, upgrade) = Alocar(cat);

    return Results.Ok(new
    {
        CategoriaSolicitada = cat,
        Disponivel = alocado is not null,
        Upgrade = upgrade,
        CategoriaDisponivel = alocado?.Categoria,
        Cotacao = cotacao
    });
});

app.MapPost("/reservar-categoria", (string categoria, string cliente, DateTime inicio, int dias) =>
{
    if (!TentarCategoria(categoria, out var cat))
        return Results.BadRequest(new { Erro = "Categoria inválida. Use: Economico, Suv ou Executivo" });
    if (string.IsNullOrWhiteSpace(cliente))
        return Results.BadRequest(new { Erro = "Informe o nome do cliente" });
    if (dias <= 0)
        return Results.BadRequest(new { Erro = "A quantidade de dias deve ser maior que zero" });

    var (alocado, upgrade) = Alocar(cat);
    if (alocado is null)
        return Results.Conflict(new { Erro = "Nenhum carro disponível nesta categoria ou em categoria superior" });

    var cotacao = Precificacao.Calcular(TarifaCategoria(cat), inicio, dias, Ocupacao(cat), DateTime.Now);
    var imediato = Contratar(alocado, cliente, inicio, dias, cotacao);

    var mensagem = imediato ? $"Alugado para {cliente}" : $"RESERVADO para {cliente} no dia {inicio:dd/MM/yyyy}";
    if (upgrade) mensagem += $" com UPGRADE gratuito de {cat} para {alocado.Categoria}";

    return Results.Ok(new
    {
        Mensagem = mensagem,
        alocado.Placa,
        alocado.Modelo,
        CategoriaReservada = cat,
        CategoriaAlocada = alocado.Categoria,
        Upgrade = upgrade,
        Total = cotacao.Total,
        Cotacao = cotacao
    });
});

app.MapPost("/devolver", (string placa, int diasUsados) =>
{
    if (diasUsados <= 0)
        return Results.BadRequest(new { Erro = "A quantidade de dias usados deve ser maior que zero" });

    var carro = Buscar(placa);
    if (carro == null)
        return Results.NotFound(new { Erro = "Carro não encontrado" });
    if (carro.DataAluguel == null)
        return Results.BadRequest(new { Erro = "Carro não está alugado" });

    var diaria = carro.DiariaContratada;
    var atraso = diasUsados - carro.DiasContratados;

    if (atraso <= 0)
    {
        var total = Math.Round(diaria * diasUsados, 2);
        carro.ResetarEstado();
        return Results.Ok(new { Mensagem = "Devolução realizada dentro do prazo", Total = total });
    }

    var multa = Math.Round(atraso * diaria * 0.5m, 2);
    var totalFinal = Math.Round(carro.DiasContratados * diaria + multa + atraso * diaria, 2);
    carro.ResetarEstado();
    return Results.Ok(new { Mensagem = $"Atraso de {atraso} dia(s)! Multa de 50% aplicada.", Multa = multa, Total = totalFinal });
});

app.MapDelete("/cancelar-reserva/{placa}", (string placa) =>
{
    var carro = Buscar(placa);
    if (carro == null)
        return Results.NotFound(new { Erro = "Placa não encontrada" });

    bool isReservado = !carro.Disponivel && carro.DataReserva != null && carro.DataAluguel == null;

    if (!isReservado)
    {
        if (carro.Disponivel)
            return Results.BadRequest(new { Erro = "Carro já está disponível" });

        return Results.BadRequest(new { Erro = "Carro está alugado, use /devolver" });
    }

    carro.ResetarEstado();
    return Results.Ok(new { Mensagem = $"Reserva da placa {carro.Placa} cancelada com sucesso" });
});

app.Run();

// ───────────────────────── Domínio e Modelos ─────────────────────────

enum Categoria { Economico = 1, Suv = 2, Executivo = 3 }

record Carro(string Placa, string Modelo, decimal ValorDiaria, Categoria Categoria)
{
    public bool Disponivel { get; set; } = true;
    public DateTime? DataAluguel { get; set; }
    public int DiasContratados { get; set; }
    public decimal DiariaContratada { get; set; }
    public string? ClienteAtual { get; set; }
    public DateTime? DataReserva { get; set; }
    public bool Reservado => DataReserva != null;

    public void ResetarEstado()
    {
        Disponivel = true;
        ClienteAtual = null;
        DataAluguel = null;
        DataReserva = null;
        DiasContratados = 0;
        DiariaContratada = 0;
    }
}

record FatoresPreco(
    decimal Sazonalidade,
    decimal Antecedencia,
    decimal Ocupacao,
    decimal Duracao,
    int DiasFeriado,
    int DiasAltaTemporada,
    int DiasFimDeSemana);

record Cotacao(decimal DiariaBase, int Dias, FatoresPreco Fatores, decimal DiariaMedia, decimal Total);

static class Precificacao
{
    public static Cotacao Calcular(decimal diariaBase, DateTime inicio, int dias, double ocupacao, DateTime hoje)
    {
        decimal soma = 0;
        int feriados = 0, altaTemporada = 0, fimDeSemana = 0;

        for (var i = 0; i < dias; i++)
        {
            var dia = inicio.Date.AddDays(i);
            var fator = 1.00m;

            if (CalendarioFeriados.EhFeriado(dia)) { fator = 1.25m; feriados++; }
            else if (CalendarioFeriados.EhAltaTemporada(dia)) { fator = 1.15m; altaTemporada++; }
            else if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { fator = 1.10m; fimDeSemana++; }

            soma += diariaBase * fator;
        }

        var antecedenciaDias = (inicio.Date - hoje.Date).Days;
        var fAntecedencia = antecedenciaDias switch
        {
            <= 1 => 1.15m,
            <= 6 => 1.05m,
            <= 14 => 1.00m,
            <= 29 => 0.95m,
            _ => 0.90m
        };

        var fOcupacao = ocupacao switch
        {
            >= 0.8 => 1.20m,
            >= 0.5 => 1.10m,
            < 0.2 => 0.95m,
            _ => 1.00m
        };

        var fDuracao = dias switch
        {
            >= 30 => 0.85m,
            >= 7 => 0.95m,
            _ => 1.00m
        };

        var totalBase = diariaBase * dias;
        var total = soma * fAntecedencia * fOcupacao * fDuracao;

        total = Math.Round(Math.Clamp(total, totalBase * 0.70m, totalBase * 2.00m), 2);

        var fatores = new FatoresPreco(
            totalBase == 0 ? 1 : Math.Round(soma / totalBase, 3),
            fAntecedencia, fOcupacao, fDuracao,
            feriados, altaTemporada, fimDeSemana);

        return new Cotacao(diariaBase, dias, fatores, Math.Round(total / dias, 2), total);
    }
}

static class CalendarioFeriados
{
    static readonly (int Mes, int Dia)[] Fixos =
    {
        (1, 1), (4, 21), (5, 1), (9, 7), (10, 12), (11, 2), (11, 15), (11, 20), (12, 25)
    };

    static readonly int[] DeslocamentosPascoa = { -48, -47, -2, 60 };

    public static bool EhFeriado(DateTime data)
    {
        if (Fixos.Contains((data.Month, data.Day))) return true;
        var pascoa = Pascoa(data.Year);
        return DeslocamentosPascoa.Any(d => pascoa.AddDays(d) == data.Date);
    }

    public static bool EhAltaTemporada(DateTime data) =>
        data.Month == 1 || data.Month == 7 || (data.Month == 12 && data.Day >= 20);

    static DateTime Pascoa(int ano)
    {
        int a = ano % 19, b = ano / 100, c = ano % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int mes = (h + l - 7 * m + 114) / 31;
        int dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(ano, mes, dia);
    }
}
