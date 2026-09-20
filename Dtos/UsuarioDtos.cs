using System.ComponentModel.DataAnnotations;

using SchoolApi.Models;

namespace SchoolApi.Dtos;

public record CriarUsuarioRequest(
    [Required][StringLength(120, MinimumLength = 2)] string Nome,
    [Required][EmailAddress][StringLength(160)] string Email,
    [Required][StringLength(100, MinimumLength = 8, ErrorMessage = "A senha tem no mínimo 8 caracteres")] string Senha,
    [Required] PapelUsuario Papel,
    int? ProfessorId,
    int? AlunoId);

/// <summary>
/// Sem hash de senha, sem token: o que a API devolve sobre um usuário é só o
/// que uma tela precisa mostrar.
/// </summary>
public record UsuarioResponse(
    int Id,
    string Nome,
    string Email,
    PapelUsuario Papel,
    bool Ativo,
    DateTime DataCriacao);
