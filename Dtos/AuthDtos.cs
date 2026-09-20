using System.ComponentModel.DataAnnotations;

using SchoolApi.Models;

namespace SchoolApi.Dtos;

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required][StringLength(100, MinimumLength = 8, ErrorMessage = "A senha tem no mínimo 8 caracteres")] string Senha);

public record RefreshRequest([Required] string RefreshToken);

public record SessaoResponse(
    string AccessToken,
    DateTime ExpiraEm,
    string RefreshToken,
    UsuarioLogadoResponse Usuario);

public record UsuarioLogadoResponse(
    int Id,
    string Nome,
    string Email,
    PapelUsuario Papel,
    int? ProfessorId,
    int? AlunoId);
