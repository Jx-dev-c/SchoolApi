using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Auth;
using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _contexto;
    private readonly ServicoDeToken _tokens;
    private readonly IPasswordHasher<Usuario> _hasher;

    public AuthController(AppDbContext contexto, ServicoDeToken tokens, IPasswordHasher<Usuario> hasher)
    {
        _contexto = contexto;
        _tokens = tokens;
        _hasher = hasher;
    }

    /// <summary>Troca e-mail e senha por um par de tokens.</summary>
    /// <remarks>
    /// A resposta é a mesma para e-mail inexistente e senha errada: dizer qual
    /// dos dois falhou entrega ao atacante quais e-mails existem na base.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<SessaoResponse>> Login(LoginRequest requisicao, CancellationToken ct)
    {
        var usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Email == requisicao.Email, ct);

        if (usuario is null || !usuario.Ativo)
        {
            // gasta o mesmo tempo de um hash real mesmo sem usuário, para o
            // tempo de resposta não revelar se o e-mail existe
            _hasher.HashPassword(new Usuario { Nome = "-", Email = "-", SenhaHash = "-" }, requisicao.Senha);
            return Unauthorized(new ProblemDetails { Title = "E-mail ou senha inválidos", Status = 401 });
        }

        var resultado = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, requisicao.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ProblemDetails { Title = "E-mail ou senha inválidos", Status = 401 });
        }

        // o hash é reescrito quando o algoritmo evolui, sem pedir troca de senha
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, requisicao.Senha);
        }

        return Ok(await AbrirSessaoAsync(usuario, ct));
    }

    /// <summary>Troca um refresh token válido por um par novo (rotação).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<SessaoResponse>> Refresh(RefreshRequest requisicao, CancellationToken ct)
    {
        var hash = ServicoDeToken.CalcularHash(requisicao.RefreshToken);

        var token = await _contexto.RefreshTokens
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.Usuario is null || !token.Usuario.Ativo)
        {
            return Unauthorized(new ProblemDetails { Title = "Refresh token inválido", Status = 401 });
        }

        // Token já usado (ou revogado) sendo apresentado de novo é sinal de
        // roubo: derruba a família inteira em vez de só recusar este uso.
        if (!token.Ativo)
        {
            await RevogarTodosAsync(token.UsuarioId, ct);
            return Unauthorized(new ProblemDetails { Title = "Sessão encerrada por segurança", Status = 401 });
        }

        var sessao = await AbrirSessaoAsync(token.Usuario, ct, tokenAnterior: token);
        return Ok(sessao);
    }

    /// <summary>Encerra a sessão revogando os refresh tokens do usuário.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var usuarioId = User.ObterUsuarioId();
        if (usuarioId is null)
        {
            return Unauthorized();
        }

        await RevogarTodosAsync(usuarioId.Value, ct);
        return NoContent();
    }

    /// <summary>Quem sou eu — usado pelo front ao recarregar a página.</summary>
    [HttpGet("eu")]
    [Authorize]
    public async Task<ActionResult<UsuarioLogadoResponse>> Eu(CancellationToken ct)
    {
        var usuarioId = User.ObterUsuarioId();
        var usuario = usuarioId is null ? null : await _contexto.Usuarios.FindAsync([usuarioId.Value], ct);

        return usuario is null
            ? Unauthorized()
            : Ok(new UsuarioLogadoResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel, usuario.ProfessorId, usuario.AlunoId));
    }

    private async Task<SessaoResponse> AbrirSessaoAsync(Usuario usuario, CancellationToken ct, RefreshToken? tokenAnterior = null)
    {
        var (accessToken, expiraEm) = _tokens.GerarAccessToken(usuario);
        var (refreshToken, hash, expiraRefresh) = _tokens.GerarRefreshToken();

        if (tokenAnterior is not null)
        {
            tokenAnterior.RevogadoEm = DateTime.UtcNow;
            tokenAnterior.SubstituidoPor = hash;
        }

        _contexto.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = hash,
            CriadoEm = DateTime.UtcNow,
            ExpiraEm = expiraRefresh,
        });

        await _contexto.SaveChangesAsync(ct);

        return new SessaoResponse(
            accessToken,
            expiraEm,
            refreshToken,
            new UsuarioLogadoResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel, usuario.ProfessorId, usuario.AlunoId));
    }

    private async Task RevogarTodosAsync(int usuarioId, CancellationToken ct)
    {
        await _contexto.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevogadoEm, DateTime.UtcNow), ct);
    }
}
