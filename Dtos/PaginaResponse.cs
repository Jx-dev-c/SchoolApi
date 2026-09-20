using System.ComponentModel.DataAnnotations;

namespace SchoolApi.Dtos;

/// <summary>
/// Parâmetros de paginação. Listagem sem limite é bomba-relógio: funciona com
/// 48 alunos e derruba a tela com 5 mil.
/// </summary>
/// <remarks>
/// Propriedades com `set`, e não `init`: o model binder do ASP.NET não preenche
/// propriedades init-only vindas de query string — os valores padrão venciam
/// sempre, e `?tamanho=100000` passava como se fosse 20.
/// </remarks>
public class ParametrosDePagina
{
    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    /// <summary>Teto de 100 para o cliente não pedir a tabela inteira.</summary>
    [Range(1, 100)]
    public int Tamanho { get; set; } = 20;

    public int Pular => (Pagina - 1) * Tamanho;
}

public record PaginaResponse<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total)
{
    public int TotalDePaginas => (int)Math.Ceiling(Total / (double)Tamanho);
    public bool TemProxima => Pagina < TotalDePaginas;
}
