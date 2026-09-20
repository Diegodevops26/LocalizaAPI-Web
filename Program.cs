var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Banco de Memória
var carros = new List<Carro>
{
    new ("BRA2E16", "Onix LT", 120),
    new ("XYZ1234", "Renegade", 250),
    new ("ABC1D23", "HB20", 110),
    new ("ACD2H18", "Creta", 200),
    new ("BCA3E19", "T-Cross", 220),
    new ("DEF4G20", "HR-V", 230),
    new ("GHI5J21", "Kicks", 180),
    new ("JKL6K22", "Compass", 270),
    new ("MNO7L23", "Corolla", 300)
};

app.MapGet("/", () => "API Localiza Clone rodando! Acesse /carros");

app.MapGet("/carros", () => carros);

app.MapGet("/carros/disponiveis", () => carros.Where(c => c.Disponivel));

app.MapPost("/alugar", (string placa, string cliente, int dias) => 
{
    var carro = carros.FirstOrDefault(c => c.Placa.Equals(placa, StringComparison.OrdinalIgnoreCase));
    if (carro == null) 
        return Results.NotFound("Carro não encontrado");
    if (!carro.Disponivel) 
        return Results.BadRequest("Carro já está alugado ou reservado");

    carro.Disponivel = false;
    carro.DataAluguel = DateTime.Now;
    carro.DiasContratados = dias;
    carro.ClienteAtual = cliente;

    var total = carro.ValorDiaria * dias;
    return Results.Ok(new { Mensagem = $"Alugado para {cliente}", Total = total });
});

app.MapPost("/devolver", (string placa, int diasUsados) =>
{
    var carro = carros.FirstOrDefault(c => c.Placa.Equals(placa, StringComparison.OrdinalIgnoreCase));
    if (carro == null) 
        return Results.NotFound("Carro não encontrado");

    var atraso = diasUsados - carro.DiasContratados;
    decimal totalFinal;

    if (atraso <= 0)
    {
        totalFinal = carro.ValorDiaria * diasUsados;
        carro.ResetarEstado(); // Libera o carro após a devolução
        return Results.Ok(new { Mensagem = "Devolução realizada dentro do prazo", Total = totalFinal });
    }
    else
    {
        var multa = atraso * carro.ValorDiaria * 0.5m; // 50% de multa por dia de atraso
        totalFinal = (carro.DiasContratados * carro.ValorDiaria) + multa + (atraso * carro.ValorDiaria);
        carro.ResetarEstado(); // Libera o carro após a devolução
        return Results.Ok(new { Mensagem = $"Atraso de {atraso} dia(s)! Multa de 50% aplicada.", Multa = multa, Total = totalFinal });
    }
});

app.MapGet("/reserva", () => 
    carros.Where(c => c.DataReserva != null)
          .Select(c => new { c.Placa, c.Modelo, c.DataReserva, c.ClienteAtual }));

app.MapPost("/reservar", (string placa, string cliente, DateTime dataReserva, int dias) => 
{
    var carro = carros.FirstOrDefault(c => c.Placa.Equals(placa, StringComparison.OrdinalIgnoreCase));

    if (carro == null)
        return Results.NotFound("Carro não encontrado");

    if (!carro.Disponivel)
        return Results.BadRequest("Carro já está alugado/reservado");

    if (dataReserva.Date <= DateTime.Now.Date)
    {
        carro.Disponivel = false;
        carro.ClienteAtual = cliente;
        carro.DataReserva = dataReserva;
        carro.DataAluguel = dataReserva;
        carro.DiasContratados = dias;
        return Results.Ok(new { Mensagem = $"Alugado para {cliente}", Total = carro.ValorDiaria * dias });
    }
    else
    {
        carro.Disponivel = false; // Bloqueia para não alugar para outro
        carro.ClienteAtual = cliente;
        carro.DataReserva = dataReserva;
        carro.DataAluguel = null;
        carro.DiasContratados = dias;
        return Results.Ok(new { Mensagem = $"RESERVADO para {cliente} no dia {dataReserva:dd/MM/yyyy}", Total = carro.ValorDiaria * dias });
    }
});

app.MapGet("/relatorio", () => 
{
    var total = carros.Count;
    
    // Disponíveis: Disponivel true
    var disponiveis = carros.Count(c => c.Disponivel);
    
    // Reservados: bloqueados, com reserva marcada e ainda sem data de aluguel ativa
    var reservados = carros.Count(c => !c.Disponivel && c.DataReserva != null && c.DataAluguel == null);
    
    // Alugados: bloqueados e com aluguel ativo
    var alugados = carros.Count(c => !c.Disponivel && c.DataAluguel != null);

    var taxaOcupacao = total > 0 ? (double)(alugados + reservados) / total * 100 : 0;

    return Results.Ok(new 
    { 
        TotalCarros = total, 
        CarrosDisponiveis = disponiveis, 
        CarrosAlugados = alugados,
        CarrosReservados = reservados,
        TaxaOcupacao = $"{taxaOcupacao:F1}%" 
    });
});

app.Run();

record Carro(string Placa, string Modelo, decimal ValorDiaria)
{
    public bool Disponivel { get; set; } = true;
    public DateTime? DataAluguel { get; set; }
    public int DiasContratados { get; set; }
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
    }
}