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

### Alunos e matrículas

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/alunos?pagina=1&tamanho=20&busca=ana` | Lista paginada (admin e professor) |
| POST | `/api/alunos` | Cadastra aluno — apenas admin |
| POST | `/api/matriculas` | Matricula respeitando a capacidade da turma |
| GET | `/api/matriculas/{id}/boletim` | Notas, média e frequência |
| PUT | `/api/matriculas/{id}/notas` | Lança ou corrige a nota de um bimestre |

O boletim é visível para o próprio aluno, o professor daquela turma e o admin.
Professor só lança nota na turma dele.

## Testes

```bash
cd tests/SchoolApi.Tests
dotnet test
```

31 testes de integração que sobem a API inteira contra um PostgreSQL em
container (Testcontainers). Banco em memória não serviria: o provedor InMemory
do EF não tem índice único, transação nem `FOR UPDATE` — justamente o que
sustenta as regras desta API.

Três deles disparam requisições simultâneas de propósito, porque é onde os bugs
moram: renovações com o mesmo refresh token, dez matrículas para a última vaga e
dez cadastros com o mesmo e-mail.

Esses testes são verificados por mutação: removendo a trava do banco, o teste de
matrícula falha. A primeira versão dele usava só duas requisições e passava
mesmo com a trava removida — a janela entre contar e gravar é de milissegundos,
e duas requisições não competem de verdade.

## Configuração

Nada de segredo no repositório. Em desenvolvimento, use user-secrets:

```bash
dotnet user-secrets set "Jwt:Chave" "<48+ caracteres aleatórios>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=schooldb;Username=school;Password=<senha>"
```

Em produção, as mesmas chaves vêm de variável de ambiente. A aplicação não sobe
sem a chave do JWT — é validação na inicialização, não erro no primeiro login.

## Como executar

Tudo em container, um comando:

```bash
docker compose up -d
```

A API sobe em `http://localhost:5105` (Swagger em `/swagger`), aplica as
migrations sozinha e, em desenvolvimento, popula a base. O banco só recebe
tráfego depois de passar no healthcheck.

Para rodar a API fora do container, durante o desenvolvimento:

```bash
docker compose up -d postgres
dotnet user-secrets set "Jwt:Chave" "<48+ caracteres aleatórios>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=schooldb;Username=school;Password=school123"
dotnet run
```

O sistema completo, com o front junto, sobe pelo compose do
[school-app](https://github.com/Jx-dev-c/school-app).

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

- [ ] Validação de dados com Data Annotations
- [ ] Testes unitários com xUnit