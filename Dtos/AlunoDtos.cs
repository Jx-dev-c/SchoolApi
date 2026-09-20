using System.ComponentModel.DataAnnotations;

namespace SchoolApi.Dtos;

/// <summary>
/// O que o cliente pode mandar ao criar um aluno — e só isso. Enquanto a
/// entidade era o contrato da API, um PUT podia sobrescrever `Id` e
/// `DataCriacao`, que são do servidor.
/// </summary>
public record CriarAlunoRequest(
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 120 caracteres")]
    string Nome,
    [Required(ErrorMessage = "E-mail é obrigatório")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    [StringLength(160)]
    string Email,
    [Required(ErrorMessage = "Data de nascimento é obrigatória")]
    DateOnly DataNascimento);

public record AtualizarAlunoRequest(
    [Required]
    [StringLength(120, MinimumLength = 2)]
    string Nome,
    [Required]
    [EmailAddress]
    [StringLength(160)]
    string Email,
    [Required]
    DateOnly DataNascimento);

public record AlunoResponse(
    int Id,
    string Nome,
    string Email,
    DateOnly DataNascimento,
    DateTime DataCriacao);
