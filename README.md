# SchoolApi

API REST para gerenciamento de usuários de uma instituição de ensino, desenvolvida em ASP.NET Core.

Frontend do projeto: [school-app](https://github.com/Jx-dev-c) *(Angular)*

## Stack

- .NET 10 / ASP.NET Core
- Entity Framework Core (banco em memória)
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

```bash
dotnet restore
dotnet run
```

A API sobe em `http://localhost:5105`.
Documentação interativa em `http://localhost:5105/swagger`.

## Decisões técnicas

- **Banco em memória:** escolhido para facilitar a execução sem dependências externas. Migração para PostgreSQL planejada.
- **CORS liberado:** configurado para desenvolvimento local com o frontend Angular.
- **HTTPS condicional:** `UseHttpsRedirection` ativo apenas fora de desenvolvimento, evitando conflito com certificado auto-assinado em chamadas server-side.
- **Métodos assíncronos:** operações de banco usam as versões `Async` do EF Core para não bloquear threads.

## Roadmap

- [ ] Migração para PostgreSQL via LocalStack
- [ ] Validação de dados com Data Annotations
- [ ] Testes unitários com xUnit