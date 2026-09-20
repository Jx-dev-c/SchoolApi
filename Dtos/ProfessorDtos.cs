using System.ComponentModel.DataAnnotations;

namespace SchoolApi.Dtos;

public record CriarProfessorRequest(
    [Required]
    [StringLength(120, MinimumLength = 2)]
    string Nome,
    [Required]
    [EmailAddress]
    [StringLength(160)]
    string Email,
    [Required]
    [StringLength(80)]
    string Disciplina);

public record ProfessorResponse(
    int Id,
    string Nome,
    string Email,
    string Disciplina,
    int QuantidadeTurmas);
