# SchoolApi

API REST para gerenciamento de usuários de uma instituição de ensino, desenvolvida em ASP.NET Core.

Frontend do projeto: [school-app](https://github.com/Jx-dev-c/school-app) *(Angular)*

## Domínio

`Aluno`, `Professor`, `Turma`, `Matrícula`, `Nota` e `Presença`.

A matrícula liga aluno e turma e é entidade própria, porque carrega dados
(data, situação); nota e presença penduram nela, já que só fazem sentido dentro
de uma turma. O banco garante as regras que não podem depender do código:
e-mail único, um aluno por turma uma vez só, uma nota por bimestre e uma chamada
por dia.

Em desenvolvimento a base sobe populada (4 professores, 4 turmas, 48 alunos,
138 notas e 1.840 presenças), com semente fixa — os mesmos números a cada vez.

## Stack

- .NET 10 / ASP.NET Core
- Entity Framework Core + Npgsql
- PostgreSQL 16 (container Docker)
- LocalStack (simulação de serviços AWS)
- Swagger / OpenAPI

## Endpoints

### Autenticação

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/login` | Troca e-mail e senha por access token + refresh token |
| POST | `/api/auth/refresh` | Renova a sessão (rotaciona o refresh token) |
| POST | `/api/auth/logout` | Revoga os refresh tokens do usuário |
| GET | `/api/auth/eu` | Dados do usuário autenticado |

Access token dura 15 minutos; a sessão continua pelo refresh token, que dura 7
dias, é guardado como hash e é trocado a cada uso. Apresentar um refresh token
já usado derruba todos os tokens daquele usuário — é o sinal de que alguém
copiou o token.

Contas de demonstração (apenas em desenvolvimento), senha `escola@2026`:
`admin@escola.test`, `professor@escola.test`, `aluno@escola.test`.

### Turmas

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/turmas?anoLetivo=2026` | Lista turmas com professor e ocupação (autenticado) |
| GET | `/api/turmas/{id}` | Busca uma turma |
| POST | `/api/turmas` | Cria turma — apenas admin |

### Usuários (apenas admin)

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/usuarios` | Lista contas |
| GET | `/api/usuarios/{id}` | Busca uma conta |
| POST | `/api/usuarios` | Cria conta (valida vínculo com professor ou aluno) |
| DELETE | `/api/usuarios/{id}` | Desativa a conta e revoga as sessões |

## Configuração

Nada de segredo no repositório. Em desenvolvimento, use user-secrets:

```bash
dotnet user-secrets set "Jwt:Chave" "<48+ caracteres aleatórios>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=schooldb;Username=school;Password=<senha>"
```

Em produção, as mesmas chaves vêm de variável de ambiente. A aplicação não sobe
sem a chave do JWT — é validação na inicialização, não erro no primeiro login.

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