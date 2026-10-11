# 🚗 API Localiza&Co (Clone)

API REST de locação de veículos construída com **ASP.NET Core Minimal APIs (.NET)**. Permite listar a frota, alugar, reservar, devolver, cancelar reservas e consultar um relatório de ocupação.

> ⚠️ Projeto de estudo. Os dados ficam **em memória** (uma `List<Carro>`) e são reiniciados sempre que a aplicação é reiniciada.

---

## 📋 Sumário

- [Tecnologias](#-tecnologias)
- [Como executar](#-como-executar)
- [Modelo de dados](#-modelo-de-dados)
- [Frota inicial](#-frota-inicial)
- [Estados do veículo](#-estados-do-veículo)
- [Endpoints](#-endpoints)
- [Regras de negócio](#-regras-de-negócio)
- [Exemplos com cURL](#-exemplos-com-curl)
- [Limitações conhecidas](#-limitações-conhecidas)
- [Melhorias futuras](#-melhorias-futuras)

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
| `valorDiaria` | `decimal` | Valor da diária em R$ |
| `disponivel` | `bool` | `true` quando pode ser alugado/reservado. Padrão: `true` |
| `dataAluguel` | `DateTime?` | Data de início do aluguel ativo |
| `diasContratados` | `int` | Quantidade de dias contratados |
| `clienteAtual` | `string?` | Cliente que alugou/reservou |
| `dataReserva` | `DateTime?` | Data da reserva |
| `reservado` | `bool` | Calculado: `true` se `dataReserva != null` |

## 🚙 Frota inicial

| Placa | Modelo | Diária (R$) |
|---|---|---|
| BRA2E16 | Onix LT | 120 |
| XYZ1234 | Renegade | 250 |
| ABC1D23 | HB20 | 110 |
| ACD2H18 | Creta | 200 |
| BCA3E19 | T-Cross | 220 |
| DEF4G20 | HR-V | 230 |
| GHI5J21 | Kicks | 180 |
| JKL6K22 | Compass | 270 |
| MNO7L23 | Corolla | 300 |

---

## 🔄 Estados do veículo

O estado é inferido pela combinação dos campos:

| Estado | `disponivel` | `dataReserva` | `dataAluguel` |
|---|---|---|---|
| **Disponível** | `true` | `null` | `null` |
| **Reservado** (data futura) | `false` | preenchida | `null` |
| **Alugado** | `false` | qualquer | preenchida |

```
Disponível ──/alugar──────────────▶ Alugado ──/devolver──▶ Disponível
Disponível ──/reservar (futuro)───▶ Reservado ──/cancelar-reserva──▶ Disponível
Disponível ──/reservar (hoje/passado)▶ Alugado
```

---

## 🌐 Endpoints

| Método | Rota | Descrição |
|---|---|---|
| GET | `/` | Mensagem de status da API |
| GET | `/carros` | Lista toda a frota |
| GET | `/carros/disponiveis` | Lista apenas carros disponíveis |
| POST | `/alugar` | Aluga um carro imediatamente |
| POST | `/reservar` | Reserva (ou aluga, se a data for hoje/passada) |
| POST | `/devolver` | Registra a devolução e calcula o total |
| GET | `/reserva` | Lista carros que possuem data de reserva |
| DELETE | `/cancelar-reserva/{placa}` | Cancela uma reserva futura |
| GET | `/relatorio` | Resumo de ocupação da frota |

---

### `GET /`

Verifica se a API está rodando.

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
    "disponivel": true,
    "dataAluguel": null,
    "diasContratados": 0,
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

### `POST /alugar`

Aluga um carro a partir de agora.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `cliente` | string | ✅ | Nome do cliente |
| `dias` | int | ✅ | Dias de aluguel |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` | `{ "mensagem": "Alugado para João", "total": 360 }` |
| `404 Not Found` | `"Carro não encontrado"` |
| `400 Bad Request` | `"Carro já está alugado ou reservado"` |

`total = valorDiaria × dias`

---

### `POST /reservar`

Reserva um carro para uma data. Se a data for **hoje ou anterior**, o carro é alugado imediatamente.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `cliente` | string | ✅ | Nome do cliente |
| `dataReserva` | DateTime | ✅ | Ex.: `2026-11-20` |
| `dias` | int | ✅ | Dias contratados |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` (data futura) | `{ "mensagem": "RESERVADO para João no dia 20/11/2026", "total": 360 }` |
| `200 OK` (hoje/passado) | `{ "mensagem": "Alugado para João", "total": 360 }` |
| `404 Not Found` | `"Carro não encontrado"` |
| `400 Bad Request` | `"Carro já está alugado/reservado"` |

---

### `POST /devolver`

Registra a devolução, libera o carro e calcula o valor final.

**Query params**

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `placa` | string | ✅ | Placa do carro |
| `diasUsados` | int | ✅ | Dias efetivamente usados |

**Respostas**

| Status | Corpo |
|---|---|
| `200 OK` (no prazo) | `{ "mensagem": "Devolução realizada dentro do prazo", "total": 240 }` |
| `200 OK` (atrasado) | `{ "mensagem": "Atraso de 2 dia(s)! Multa de 50% aplicada.", "multa": 120, "total": 600 }` |
| `404 Not Found` | `"Carro não encontrado"` |

**Cálculo**

- `atraso = diasUsados - diasContratados`
- **Sem atraso (`atraso <= 0`):** `total = valorDiaria × diasUsados`
- **Com atraso:**
  - `multa = atraso × valorDiaria × 0,5`
  - `total = (diasContratados × valorDiaria) + multa + (atraso × valorDiaria)`

*Exemplo:* Onix (R$ 120), 3 dias contratados, 5 usados → atraso 2, multa R$ 120, total = 360 + 120 + 240 = **R$ 720**.

---

### `GET /reserva`

Lista os carros que possuem `dataReserva`.

**Resposta `200 OK`**
```json
[
  {
    "placa": "BRA2E16",
    "modelo": "Onix LT",
    "dataReserva": "2026-11-20T00:00:00",
    "clienteAtual": "João"
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
| `200 OK` | `{ "mensagem": "Reserva da placa BRA2E16 cancelada com sucesso" }` |
| `404 Not Found` | `{ "erro": "Placa não encontrada" }` |
| `400 Bad Request` | `{ "erro": "Carro já está disponivel" }` |
| `400 Bad Request` | `{ "erro": "Carro está alugado, use /devolver" }` |

---

### `GET /relatorio`

Resumo de ocupação da frota.

**Resposta `200 OK`**
```json
{
  "totalCarros": 9,
  "carrosDisponiveis": 6,
  "carrosAlugados": 2,
  "carrosReservados": 1,
  "taxaOcupacao": "33,3%"
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
4. Reservas com data de hoje ou anterior viram **aluguel imediato**.
5. Só é possível cancelar o que está **reservado**; carro alugado deve ser **devolvido**.
6. Atraso na devolução gera **multa de 50% da diária por dia de atraso**, além das diárias excedentes.
7. Após a devolução, o estado do carro é totalmente resetado (`ResetarEstado()`).

---

## 💻 Exemplos com cURL

```bash
# Listar frota e disponíveis
curl http://localhost:5000/carros
curl http://localhost:5000/carros/disponiveis

# Alugar
curl -X POST "http://localhost:5000/alugar?placa=BRA2E16&cliente=Joao&dias=3"

# Reservar para data futura
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
- **Sem validação de entrada:** `dias` ou `diasUsados` zero/negativos são aceitos.
- **`/devolver` não verifica se o carro está alugado:** é possível "devolver" um carro disponível.
- **`/devolver` não trata reservas:** o carro é liberado independentemente do estado.
- **Sem concorrência segura:** a lista não é thread-safe.
- **`/reserva` também lista aluguéis imediatos** feitos via `/reservar` (pois `dataReserva` é preenchida).
- **Sem autenticação** e sem Swagger configurado.

## 🚀 Melhorias futuras

- Persistência com banco de dados (PostgreSQL + EF Core)
- Validações (FluentValidation) e uso de corpo JSON nos `POST`
- Swagger/OpenAPI (`AddSwaggerGen` / `UseSwagger`)
- Autenticação JWT e perfis de acesso
- Testes automatizados (xUnit)
- Separação em camadas (endpoints, serviços, repositório) e Docker

---

## 👤 Autor

**Diego Sousa** — [GitHub](https://github.com/Diegodevops26) · [LinkedIn](https://linkedin.com/in/diegosousasantosdev)
