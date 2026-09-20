using System.Security.Claims;

using SchoolApi.Models;

namespace SchoolApi.Auth;

/// <summary>
/// Leitura dos claims em um lugar só. Espalhar `User.FindFirst("...")` pelos
/// controllers é como erro de autorização nasce: cada lugar lê de um jeito.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static int? ObterUsuarioId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? usuario.FindFirstValue("sub");
        return int.TryParse(valor, out var id) ? id : null;
    }

    public static int? ObterProfessorId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue(ClaimsDoUsuario.ProfessorId);
        return int.TryParse(valor, out var id) ? id : null;
    }

    public static int? ObterAlunoId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue(ClaimsDoUsuario.AlunoId);
        return int.TryParse(valor, out var id) ? id : null;
    }

    public static bool EhAdmin(this ClaimsPrincipal usuario) =>
        usuario.IsInRole(nameof(PapelUsuario.Admin));
}
