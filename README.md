# SchoolApi

API REST para gerenciamento de usuários de uma instituição de ensino, desenvolvida em ASP.NET Core.

Frontend do projeto: [school-app](https://github.com/Jx-dev-c/school-app) *(Angular)*

## Stack

- .NET 10 / ASP.NET Core
- Entity Framework Core + Npgsql
- PostgreSQL 16 (container Docker)
- LocalStack (simulação de serviços AWS)
- Swagger / OpenAPI

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/usuarios` | Lista todos os usuários |
| GET | `/api/usuarios/{id}` | Busca um usuário por ID |
| POST | `/api/usuarios` | Cria um novo usuário |
| PUT | `/api/usuarios/{id}` | Atualiza um usuário |
| DELETE | `/api/usuarios/{id}` | Remove um usuário |

## Como executar

**1. Suba a infraestrutura**

```bash
docker compose up -d
```

Isso inicia o PostgreSQL na porta `5433` e o LocalStack na `4566`.

**2. Aplique as migrations**

```bash
dotnet ef database update
```

**3. Execute a API**

```bash
dotnet run
```

Disponível em `http://localhost:5105` · Swagger em `http://localhost:5105/swagger`.

## Decisões técnicas

- **PostgreSQL em container:** substitui o banco em memória usado inicialmente. Os dados agora persistem entre reinicializações da aplicação, através de um volume Docker.
- **Porta 5433 para o container:** a 5432 costuma estar ocupada por instalações nativas do PostgreSQL. Mapear para 5433 evita o conflito.
- **`127.0.0.1` em vez de `localhost`:** no Windows, `localhost` pode ser resolvido como IPv6, o que causa timeout na conexão com containers Docker.
- **Datas em UTC:** o PostgreSQL mapeia `DateTime` para `timestamptz` e aceita apenas UTC. O armazenamento em UTC também evita inconsistências com fuso horário e horário de verão.
- **`DbContext` com tempo de vida Scoped:** o padrão do EF Core. Não é thread-safe, portanto não deve ser registrado como Singleton com banco relacional.
- **HTTPS condicional:** `UseHttpsRedirection` ativo apenas fora de desenvolvimento, evitando conflito com certificado auto-assinado em chamadas server-side.
- **Métodos assíncronos:** operações de banco usam as versões `Async` do EF Core para não bloquear threads.

> As credenciais neste repositório são de desenvolvimento local. Em produção, devem vir de variáveis de ambiente ou de um cofre de segredos.

## Roadmap

- [ ] Uso do S3 via LocalStack
- [ ] Validação de dados com Data Annotations
- [ ] Testes unitários com xUnit