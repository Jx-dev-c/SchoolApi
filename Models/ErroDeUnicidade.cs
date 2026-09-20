using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace SchoolApi.Models;

/// <summary>
/// Reconhece violação de índice único vinda do banco.
///
/// Checar antes de inserir ("já existe esse e-mail?") resolve o caso comum,
/// mas não o simultâneo: duas requisições checam ao mesmo tempo, as duas
/// passam, e a segunda estoura no INSERT. Sem este tratamento, o cliente
/// recebia 500 com traceId em vez de um erro que explica o que houve.
/// </summary>
public static class ErroDeUnicidade
{
    private const string ViolacaoDeUnicidade = "23505";

    public static bool EhConflito(this DbUpdateException excecao) =>
        excecao.InnerException is PostgresException { SqlState: ViolacaoDeUnicidade };
}
