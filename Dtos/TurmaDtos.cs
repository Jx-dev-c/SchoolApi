using System.ComponentModel.DataAnnotations;

namespace SchoolApi.Dtos;

public record CriarTurmaRequest(
    [Required]
    [StringLength(80, MinimumLength = 2)]
    string Nome,
    [Range(2000, 2100, ErrorMessage = "Ano letivo fora do intervalo aceito")]
    int AnoLetivo,
    [Range(1, 200, ErrorMessage = "Capacidade deve ficar entre 1 e 200")]
    int Capacidade,
    [Range(1, int.MaxValue)]
    int ProfessorId);

/// <summary>
/// Resposta de listagem: já traz o nome do professor e a ocupação, para a tela
/// não precisar de uma chamada por turma (é o N+1 aparecendo na camada HTTP).
/// </summary>
public record TurmaResponse(
    int Id,
    string Nome,
    int AnoLetivo,
    int Capacidade,
    int Matriculados,
    string ProfessorNome,
    string Disciplina)
{
    /// <summary>
    /// Calculado aqui, não na consulta: repetir a contagem no SELECT gerava
    /// duas subqueries idênticas por turma, o dobro do trabalho no banco.
    /// </summary>
    public int VagasDisponiveis => Capacidade - Matriculados;
}
