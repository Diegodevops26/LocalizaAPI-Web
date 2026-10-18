# 🚗 API Localiza&Co (Clone)

API REST de locação de veículos construída com **ASP.NET Core Minimal APIs (.NET)**. Permite listar a frota, alugar, reservar, devolver, cancelar reservas e consultar um relatório de ocupação, agora com **precificação dinâmica** e **reserva por categoria com upgrade automático**.

> ⚠️ Projeto de estudo. Os dados ficam **em memória** (uma `List<Carro>`) e são reiniciados sempre que a aplicação é reiniciada.

---

## 📋 Sumário

- [Novidades da versão 2](#-novidades-da-versão-2)
- [Tecnologias](#-tecnologias)
- [Como executar](#-como-executar)
- [Modelo de dados](#-modelo-de-dados)
- [Frota inicial](#-frota-inicial)
- [Estados do veículo](#-estados-do-veículo)
- [Precificação dinâmica](#-precificação-dinâmica)
- [Categorias e upgrade](#-categorias-e-upgrade)
- [Endpoints](#-endpoints)
- [Regras de negócio](#-regras-de-negócio)
- [Exemplos com cURL](#-exemplos-com-curl)
- [Limitações conhecidas](#-limitações-conhecidas)
- [Melhorias futuras](#-melhorias-futuras)

---

## ✨ Novidades da versão 2

- **Precificação dinâmica:** o valor da locação varia por sazonalidade (feriados, alta temporada, fins de semana), antecedência da reserva, ocupação da frota e duração.
- **Categorias de veículo** (`Economico`, `Suv`, `Executivo`) e **reserva por categoria**, com **upgrade gratuito** quando a categoria pedida está esgotada.
- Novos endpoints: `/carros/{placa}/preco`, `/categorias`, `/cotacao` e `/reservar-categoria`.
- `/relatorio` agora detalha a ocupação **por categoria**.
- **Correções:** validação de `dias`/`diasUsados` (> 0), `/devolver` só aceita carros alugados, aluguéis imediatos não aparecem mais em `/reserva` e ajuste nas mensagens de erro.
- A devolução passa a usar a **diária efetivamente contratada** (já com a precificação aplicada).

---

## 🛠 Tecnologias

- C# / .NET (Minimal APIs)
- Armazenamento em memória (sem banco de dados)

## ▶️ Como executar

```bash
# clonar o repositório
git clone <url-do-repositorio>
cd <pasta-do-projeto>

# executar
dotnet run
```

A porta aparece no console ao iniciar (ex.: `http://localhost:5000`). Acesse a raiz `/` para validar que a API está no ar.

> **Importante:** nos endpoints `POST`, os parâmetros são enviados pela **query string** (não pelo corpo da requisição).

---

## 🧩 Modelo de dados

### `Carro`

| Campo | Tipo | Descrição |
|---|---|---|
| `placa` | `string` | Placa do veículo (identificador). Busca **não** diferencia maiúsculas/minúsculas |
| `modelo` | `string` | Modelo do veículo |
| `valorDiaria` | `decimal` | Diária **base** em R$ (antes da precificação dinâmica) |
| `categoria` | `enum` | `Economico`, `Suv` ou `Executivo` |
| `disponivel` | `bool` | `true` quando pode ser alugado/reservado. Padrão: `true` |
| `dataAluguel` | `DateTime?` | Data de início do aluguel ativo |
| `diasContratados` | `int` | Quantidade de dias contratados |
| `diariaContratada` | `decimal` | Diária média efetivamente cobrada (total cotado ÷ dias) |
| `clienteAtual` | `string?` | Cliente que alugou/reservou |
| `dataReserva` | `DateTime?` | Data da reserva futura |
| `reservado` | `bool` | Calculado: `true` se `dataReserva != null` |

### `Cotacao`

| Campo | Descrição |
|---|---|
| `diariaBase` | Diária base usada no cálculo |
| `dias` | Quantidade de dias da locação |
| `fatores` | Detalhamento dos fatores aplicados (ver [Precificação dinâmica](#-precificação-dinâmica)) |
| `diariaMedia` | Total ÷ dias |
| `total` | Valor final da locação |

## 🚙 Frota inicial

| Placa | Modelo | Categoria | Diária base (R$) |
|---|---|---|---|
| BRA2E16 | Onix LT | Economico | 120 |
| ABC1D23 | HB20 | Economico | 110 |
| XYZ1234 | Renegade | Suv | 250 |
| ACD2H18 | Creta | Suv | 200 |
| BCA3E19 | T-Cross | Suv | 220 |
| DEF4G20 | HR-V | Suv | 230 |
| GHI5J21 | Kicks | Suv | 180 |
| JKL6K22 | Compass | Suv | 270 |
| MNO7L23 | Corolla | Executivo | 300 |

---

## 🔄 Estados do veículo

O estado é inferido pela combinação dos campos:

| Estado | `disponivel` | `dataReserva` | `dataAluguel` |
|---|---|---|---|
| **Disponível** | `true` | `null` | `null` |
| **Reservado** (data futura) | `false` | preenchida | `null` |
| **Alugado** | `false` | `null` | preenchida |

```
Disponível ──/alugar────────────────────▶ Alugado ──/devolver──▶ Disponível
Disponível ──/reservar (data futura)────▶ Reservado ──/cancelar-reserva──▶ Disponível
Disponível ──/reservar (hoje/passado)───▶ Alugado
Disponível ──/reservar-categoria────────▶ Reservado ou Alugado (mesma regra de datas)
```

---

## 💰 Precificação dinâmica

O total da locação é calculado por `Precificacao.Calcular(...)`:

```
total = Σ (diária base × fator do dia) × fator de antecedência × fator de ocupação × fator de duração
```

O resultado é limitado a **70%–200% do valor base** (`diária base × dias`) e arredondado para 2 casas decimais.

### 1. Sazonalidade (por dia)

Cada dia da locação recebe **um único fator**. Eles **não se acumulam**; vale o primeiro que se aplicar, nesta ordem:

| Tipo do dia | Fator |
|---|---|
| Feriado nacional | × 1,25 |
| Alta temporada (20/12 a 31/01 e todo o mês de julho) | × 1,15 |
| Sábado ou domingo | × 1,10 |
| Dia comum | × 1,00 |

**Feriados considerados:** 01/01, 21/04, 01/05, 07/09, 12/10, 02/11, 15/11, 20/11, 25/12 e os móveis calculados a partir da Páscoa (segunda e terça de Carnaval, Sexta-feira Santa e Corpus Christi).

### 2. Antecedência (dias entre hoje e a retirada)

| Antecedência | Fator |
|---|---|
| 0 a 1 dia (ou data passada) | × 1,15 |
| 2 a 6 dias | × 1,05 |
| 7 a 14 dias | × 1,00 |
| 15 a 29 dias | × 0,95 |
| 30 dias ou mais | × 0,90 |

### 3. Ocupação da categoria (% de carros bloqueados)

| Ocupação | Fator |
|---|---|
| 80% ou mais | × 1,20 |
| 50% a 79% | × 1,10 |
| 20% a 49% | × 1,00 |
| Menos de 20% | × 0,95 |

### 4. Duração

| Duração | Fator |
|---|---|
| 30 dias ou mais | × 0,85 |
| 7 a 29 dias | × 0,95 |
| Menos de 7 dias | × 1,00 |

### Exemplo

Onix LT (diária base R$ 120), retirada em **20/12/2026**, 7 dias, cotação feita em 18/10/2026 com a categoria sem carros ocupados:

- Dias 20 a 24 e 26/12 → alta temporada (6 dias × 1,15); dia 25/12 → feriado (× 1,25)
- Soma por dia: 120 × (6 × 1,15 + 1,25) = **R$ 978,00**
- Antecedência (63 dias): × 0,90 · Ocupação (0%): × 0,95 · Duração (7 dias): × 0,95
- **Total = 978 × 0,90 × 0,95 × 0,95 = R$ 794,38** (diária média R$ 113,48)

---

## 🏷 Categorias e upgrade

- Cada carro pertence a uma categoria, na ordem crescente: **Economico → Suv → Executivo**.
- A **tarifa da categoria** é a média das diárias base dos carros dela (Economico: R$ 115 · Suv: R$ 225 · Executivo: R$ 300).
- Em `/reservar-categoria`, o sistema aloca o **carro disponível mais barato** da categoria pedida.
- Se a categoria estiver esgotada, o cliente recebe **upgrade gratuito** para a próxima categoria com carro livre. Ele **paga a tarifa da categoria que reservou**.
- Não existe *downgrade*: a busca só sobe de categoria.
- Se não houver carro livre na categoria pedida nem nas superiores, a API responde `409 Conflict`.
- O parâmetro `categoria` aceita variações como `suv`, `SUV`, `economico` e `econômico`.

---

## 🌐 Endpoints

| Método | Rota | Descrição |
|---|---|---|
| GET | `/` | Mensagem de status da API |
| GET | `/carros` | Lista toda a frota |
| GET | `/carros/disponiveis` | Lista apenas carros disponíveis |
| GET | `/carros/{placa}/preco` | Cotação dinâmica para um carro específico |
| GET | `/categorias` | Resumo por categoria (tarifa, disponibilidade, ocupação) |
| GET | `/cotacao` | Cotação por categoria, indicando se haverá upgrade |
| POST | `/reservar-categoria` | Reserva por categoria, com upgrade automático |
| POST | `/alugar` | Aluga um carro imediatamente |
| POST | `/reservar` | Reserva um carro (ou aluga, se a data for hoje/passada) |
| POST | `/devolver` | Registra a devolução e calcula o total |
| GET | `/reserva` | Lista carros com reserva futura |
| DELETE | `/cancelar-reserva/{placa}` | Cancela uma reserva futura |
| GET | `/relatorio` | Resumo de ocupação da frota |

> Os valores monetários dos exemplos abaixo são ilustrativos e consideram **hoje = 18/10/2026**, pois a precificação depende da data da consulta.

---

### `GET /`

**Resposta `200 OK`** (texto)
```
API Localiza Clone rodando! Acesse /carros
```

---

### `GET /carros`

Retorna todos os carros com todos os campos.

**Resposta `200 OK`**
```json
[
  {
    "placa": "BRA2E16",
    "modelo": "Onix LT",
    "valorDiaria": 120,
    "categoria": "Economico",
    "disponivel": true,
    "dataAluguel": null,
    "diasContratados": 0,
    "diariaContratada": 0,
    "clienteAtual": null,
    "dataReserva": null,
    "reservado": false
  }
]
```

---

### `GET /carros/disponiveis`

Retorna somente os carros com `disponivel = true`. Mesmo formato de `/carros`.

---

### `GET /carros/{placa}/preco`

Calcula a cotação de um carro específico para um período. Não altera o estado do carro.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `inicio` | DateTime | ✅ | Data de retirada. Ex.: `2026-12-20` |
| `fim` | DateTime | ✅ | Data de devolução (**não** é contada como diária) |

`dias = fim − inicio`

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | Carro + cotação (exemplo abaixo) |
| `404 Not Found` | `{ "erro": "Carro não encontrado" }` |
| `400 Bad Request` | `{ "erro": "A data final deve ser posterior à data inicial" }` |

**Exemplo:** `GET /carros/BRA2E16/preco?inicio=2026-12-20&fim=2026-12-27`
```json
{
  "placa": "BRA2E16",
  "modelo": "Onix LT",
  "categoria": "Economico",
  "cotacao": {
    "diariaBase": 120,
    "dias": 7,
    "fatores": {
      "sazonalidade": 1.164,
      "antecedencia": 0.90,
      "ocupacao": 0.95,
      "duracao": 0.95,
      "diasFeriado": 1,
      "diasAltaTemporada": 6,
      "diasFimDeSemana": 0
    },
    "diariaMedia": 113.48,
    "total": 794.38
  }
}
```

---

### `GET /categorias`

Resumo de cada categoria.

**Resposta `200 OK`**
```json
[
  { "categoria": "Economico", "tarifaBase": 115, "totalCarros": 2, "disponiveis": 2, "ocupacao": "0,0%" },
  { "categoria": "Suv",       "tarifaBase": 225, "totalCarros": 6, "disponiveis": 6, "ocupacao": "0,0%" },
  { "categoria": "Executivo", "tarifaBase": 300, "totalCarros": 1, "disponiveis": 1, "ocupacao": "0,0%" }
]
```

---

### `GET /cotacao`

Cotação por categoria, informando se haverá upgrade. Não altera o estado da frota.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `categoria` | string | ✅ | `Economico`, `Suv` ou `Executivo` |
| `inicio` | DateTime | ✅ | Data de retirada |
| `fim` | DateTime | ✅ | Data de devolução |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | Cotação e disponibilidade (exemplo abaixo) |
| `400 Bad Request` | `{ "erro": "Categoria inválida. Use: Economico, Suv ou Executivo" }` |
| `400 Bad Request` | `{ "erro": "A data final deve ser posterior à data inicial" }` |

**Exemplo:** `GET /cotacao?categoria=suv&inicio=2026-12-20&fim=2026-12-27`
```json
{
  "categoriaSolicitada": "Suv",
  "disponivel": true,
  "upgrade": false,
  "categoriaDisponivel": "Suv",
  "cotacao": {
    "diariaBase": 225,
    "dias": 7,
    "fatores": { "sazonalidade": 1.164, "antecedencia": 0.90, "ocupacao": 0.95, "duracao": 0.95, "diasFeriado": 1, "diasAltaTemporada": 6, "diasFimDeSemana": 0 },
    "diariaMedia": 212.78,
    "total": 1489.46
  }
}
```

Quando não há carro livre em nenhuma categoria elegível, `disponivel` vem como `false` (a cotação continua sendo informada).

---

### `POST /reservar-categoria`

Reserva por categoria. Aloca o carro disponível mais barato; se a categoria estiver esgotada, aplica **upgrade gratuito**.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `categoria` | string | ✅ | Categoria desejada |
| `cliente` | string | ✅ | Nome do cliente |
| `inicio` | DateTime | ✅ | Data de retirada. Hoje/passada = aluguel imediato; futura = reserva |
| `dias` | int | ✅ | Dias de locação (> 0) |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | Carro alocado + cotação (exemplo abaixo) |
| `400 Bad Request` | `{ "erro": "Categoria inválida. Use: Economico, Suv ou Executivo" }` |
| `400 Bad Request` | `{ "erro": "Informe o nome do cliente" }` |
| `400 Bad Request` | `{ "erro": "A quantidade de dias deve ser maior que zero" }` |
| `409 Conflict` | `{ "erro": "Nenhum carro disponível nesta categoria ou em categoria superior" }` |

**Exemplo com upgrade** (todos os SUVs ocupados):
```json
{
  "mensagem": "RESERVADO para Diego no dia 20/12/2026 com UPGRADE gratuito de Suv para Executivo",
  "placa": "MNO7L23",
  "modelo": "Corolla",
  "categoriaReservada": "Suv",
  "categoriaAlocada": "Executivo",
  "upgrade": true,
  "total": 1489.46,
  "cotacao": { "...": "..." }
}
```

---

### `POST /alugar`

Aluga um carro a partir de agora. O valor é calculado pela precificação dinâmica.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `cliente` | string | ✅ | Nome do cliente |
| `dias` | int | ✅ | Dias de aluguel (> 0) |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | `{ "mensagem": "Alugado para João", "total": 406.41, "cotacao": { ... } }` |
| `404 Not Found` | `"Carro não encontrado"` |
| `400 Bad Request` | `"Carro já está alugado ou reservado"` |
| `400 Bad Request` | `"A quantidade de dias deve ser maior que zero"` |

*Exemplo:* Onix LT por 3 dias a partir de domingo, 18/10/2026: 120 × (1,10 + 1 + 1) = 372 → × 1,15 (última hora) × 0,95 (ocupação baixa) = **R$ 406,41**.

---

### `POST /reservar`

Reserva um carro específico para uma data. Se a data for **hoje ou anterior**, o carro é alugado imediatamente.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `cliente` | string | ✅ | Nome do cliente |
| `dataReserva` | DateTime | ✅ | Ex.: `2026-11-20` |
| `dias` | int | ✅ | Dias contratados (> 0) |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` (data futura) | `{ "mensagem": "RESERVADO para Maria no dia 20/11/2026", "total": 512.57, "cotacao": { ... } }` |
| `200 OK` (hoje/passado) | `{ "mensagem": "Alugado para Maria", "total": ..., "cotacao": { ... } }` |
| `404 Not Found` | `"Carro não encontrado"` |
| `400 Bad Request` | `"Carro já está alugado/reservado"` |
| `400 Bad Request` | `"A quantidade de dias deve ser maior que zero"` |

---

### `POST /devolver`

Registra a devolução, libera o carro e calcula o valor final, usando a **diária contratada** na hora do aluguel/reserva.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `diasUsados` | int | ✅ | Dias efetivamente usados (> 0) |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` (no prazo) | `{ "mensagem": "Devolução realizada dentro do prazo", "total": 270.94 }` |
| `200 OK` (atrasado) | `{ "mensagem": "Atraso de 2 dia(s)! Multa de 50% aplicada.", "multa": 135.47, "total": 812.82 }` |
| `404 Not Found` | `"Carro não encontrado"` |
| `400 Bad Request` | `"Carro não está alugado"` |
| `400 Bad Request` | `"A quantidade de dias usados deve ser maior que zero"` |

**Cálculo** (`diaria` = `diariaContratada`)

- `atraso = diasUsados − diasContratados`
- **Sem atraso (`atraso <= 0`):** `total = diaria × diasUsados`
- **Com atraso:**
  - `multa = atraso × diaria × 0,5`
  - `total = (diasContratados × diaria) + multa + (atraso × diaria)`

*Exemplo:* Onix alugado por 3 dias com diária média de R$ 135,47 (total contratado R$ 406,41).

- Devolvido com 2 dias usados → 135,47 × 2 = **R$ 270,94**
- Devolvido com 5 dias usados → atraso 2, multa = 135,47, total = 406,41 + 135,47 + 270,94 = **R$ 812,82**

> Só é possível devolver carros **alugados**. Carros apenas reservados devem ser cancelados em `/cancelar-reserva/{placa}`.

---

### `GET /reserva`

Lista os carros com **reserva futura** (`dataReserva` preenchida). Aluguéis imediatos não aparecem aqui.

**Resposta `200 OK`**
```json
[
  {
    "placa": "ABC1D23",
    "modelo": "HB20",
    "categoria": "Economico",
    "dataReserva": "2026-11-20T00:00:00",
    "clienteAtual": "Maria"
  }
]
```

---

### `DELETE /cancelar-reserva/{placa}`

Cancela uma reserva. Só funciona para carros **reservados** (não alugados).

**Path params**

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `placa` | string | Placa do carro |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | `{ "mensagem": "Reserva da placa ABC1D23 cancelada com sucesso" }` |
| `404 Not Found` | `{ "erro": "Placa não encontrada" }` |
| `400 Bad Request` | `{ "erro": "Carro já está disponível" }` |
| `400 Bad Request` | `{ "erro": "Carro está alugado, use /devolver" }` |

---

### `GET /relatorio`

Resumo de ocupação da frota, geral e por categoria.

**Resposta `200 OK`**
```json
{
  "totalCarros": 9,
  "carrosDisponiveis": 6,
  "carrosAlugados": 2,
  "carrosReservados": 1,
  "taxaOcupacao": "33,3%",
  "porCategoria": [
    { "categoria": "Economico", "total": 2, "disponiveis": 1, "ocupacao": "50,0%" },
    { "categoria": "Suv",       "total": 6, "disponiveis": 4, "ocupacao": "33,3%" },
    { "categoria": "Executivo", "total": 1, "disponiveis": 1, "ocupacao": "0,0%" }
  ]
}
```

**Cálculo**

- `carrosDisponiveis`: `disponivel == true`
- `carrosReservados`: bloqueados, com `dataReserva` e **sem** `dataAluguel`
- `carrosAlugados`: bloqueados e com `dataAluguel`
- `taxaOcupacao = (alugados + reservados) / total × 100` (1 casa decimal)

---

## 📐 Regras de negócio

1. A busca por placa **ignora maiúsculas/minúsculas**.
2. Um carro só pode ser alugado/reservado se estiver **disponível**.
3. Reservas com data futura **bloqueiam** o carro (`disponivel = false`) para evitar duplicidade.
4. Reservas com data de hoje ou anterior viram **aluguel imediato** (não ficam na lista de reservas).
5. Só é possível cancelar o que está **reservado**; carro alugado deve ser **devolvido**; carro reservado não pode ser devolvido.
6. Quantidades de dias (`dias`, `diasUsados`) devem ser **maiores que zero**.
7. O preço é definido na contratação pela [precificação dinâmica](#-precificação-dinâmica) e fica registrado em `diariaContratada`.
8. Atraso na devolução gera **multa de 50% da diária contratada por dia de atraso**, além das diárias excedentes.
9. Na reserva por categoria, o cliente paga a **tarifa da categoria reservada**, mesmo quando recebe upgrade.
10. Após a devolução ou o cancelamento, o estado do carro é totalmente resetado (`ResetarEstado()`).

---

## 💻 Exemplos com cURL

```bash
# Listar frota e disponíveis
curl http://localhost:5000/carros
curl http://localhost:5000/carros/disponiveis

# Cotação de um carro específico
curl "http://localhost:5000/carros/BRA2E16/preco?inicio=2026-12-20&fim=2026-12-27"

# Resumo por categoria
curl http://localhost:5000/categorias

# Cotação por categoria (informa se haverá upgrade)
curl "http://localhost:5000/cotacao?categoria=suv&inicio=2026-12-20&fim=2026-12-27"

# Reservar por categoria (com upgrade automático, se necessário)
curl -X POST "http://localhost:5000/reservar-categoria?categoria=suv&cliente=Diego&inicio=2026-12-20&dias=7"

# Alugar um carro específico
curl -X POST "http://localhost:5000/alugar?placa=BRA2E16&cliente=Joao&dias=3"

# Reservar um carro específico para data futura
curl -X POST "http://localhost:5000/reservar?placa=ABC1D23&cliente=Maria&dataReserva=2026-11-20&dias=5"

# Devolver (com atraso)
curl -X POST "http://localhost:5000/devolver?placa=BRA2E16&diasUsados=5"

# Listar reservas
curl http://localhost:5000/reserva

# Cancelar reserva
curl -X DELETE http://localhost:5000/cancelar-reserva/ABC1D23

# Relatório
curl http://localhost:5000/relatorio
```

---

## ⚠️ Limitações conhecidas

- **Dados voláteis:** tudo é perdido ao reiniciar.
- **Disponibilidade simples:** a reserva futura bloqueia o carro imediatamente. Não há verificação de períodos sobrepostos, então o carro fica indisponível desde a reserva até a devolução ou o cancelamento.
- **Calendário nacional apenas:** não há feriados estaduais ou municipais, nem eventos locais.
- **Regras de preço fixas no código:** fatores, faixas e limites não são configuráveis.
- **Upgrade só para cima:** não há *downgrade* entre categorias.
- **Sem concorrência segura:** a lista não é thread-safe.
- **Parâmetros na query string:** os `POST` não usam corpo JSON.
- **Formatação por cultura:** percentuais usam a cultura do servidor (ex.: `33,3%` em pt-BR).
- **Sem autenticação** e sem Swagger configurado.

## 🚀 Melhorias futuras

- Persistência com banco de dados (PostgreSQL + EF Core), com entidades separadas (`Cliente`, `Locacao`, `Reserva`) e histórico de locações
- Máquina de estados explícita (`enum StatusCarro`) com manutenção preventiva e desativação para seminovos
- Verificação de conflito de datas em reservas futuras
- Regras de preço configuráveis (tabela no banco) e dashboard de receita e ocupação
- Validações (FluentValidation) e uso de corpo JSON nos `POST`
- Swagger/OpenAPI (`AddSwaggerGen` / `UseSwagger`)
- Autenticação JWT e perfis de acesso
- Testes automatizados (xUnit) para precificação, upgrade e multa
- Separação em camadas (endpoints, serviços, repositório), Docker e CI com GitHub Actions
- Front-end em Next.js consumindo a API

---

## 👤 Autor

**Diego Sousa** — [GitHub](https://github.com/Diegodevops26) · [LinkedIn](https://linkedin.com/in/diegosousasantosdev)
