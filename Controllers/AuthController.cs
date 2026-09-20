using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly ILogger<AuthController> _log;

    public AuthController(
        AppDbContext contexto,
        ServicoDeToken tokens,
        IPasswordHasher<Usuario> hasher,
        ILogger<AuthController> log)
    {
        _contexto = contexto;
        _tokens = tokens;
        _hasher = hasher;
        _log = log;
    }

    /// <summary>Troca e-mail e senha por um par de tokens.</summary>
    /// <remarks>
    /// A resposta é a mesma para e-mail inexistente e senha errada: dizer qual
    /// dos dois falhou entrega ao atacante quais e-mails existem na base.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SessaoResponse>> Login(LoginRequest requisicao, CancellationToken ct)
    {
        var usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Email == requisicao.Email, ct);

        if (usuario is null || !usuario.Ativo)
        {
            // gasta o mesmo tempo de um hash real mesmo sem usuário, para o
            // tempo de resposta não revelar se o e-mail existe
            _hasher.HashPassword(new Usuario { Nome = "-", Email = "-", SenhaHash = "-" }, requisicao.Senha);
            _log.LogInformation("Tentativa de login sem conta correspondente");
            return Unauthorized(new ProblemDetails { Title = "E-mail ou senha inválidos", Status = 401 });
        }

        var resultado = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, requisicao.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            _log.LogWarning("Senha inválida para o usuario {UsuarioId}", usuario.Id);
            return Unauthorized(new ProblemDetails { Title = "E-mail ou senha inválidos", Status = 401 });
        }

        // o hash é reescrito quando o algoritmo evolui, sem pedir troca de senha
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, requisicao.Senha);
        }

        _log.LogInformation("Login do usuario {UsuarioId} ({Papel})", usuario.Id, usuario.Papel);
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

        // Expirado é diferente de revogado. Tratar os dois como roubo derrubava
        // as sessões vivas de quem só passou uma semana sem abrir o sistema.
        if (token.RevogadoEm is null && DateTime.UtcNow >= token.ExpiraEm)
        {
            return Unauthorized(new ProblemDetails { Title = "Refresh token expirado", Status = 401 });
        }

        // Token já revogado sendo apresentado de novo é sinal de cópia: derruba
        // a família inteira, em vez de só recusar este uso.
        if (token.RevogadoEm is not null)
        {
            _log.LogWarning(
                "Refresh token revogado reapresentado (usuario {UsuarioId}); todas as sessões foram encerradas",
                token.UsuarioId);
            await RevogarTodosAsync(token.UsuarioId, ct);
            return Unauthorized(new ProblemDetails { Title = "Sessão encerrada por segurança", Status = 401 });
        }

        var sessao = await AbrirSessaoAsync(token.Usuario, ct, tokenAnterior: token);
        if (sessao is null)
        {
            // outra requisição rotacionou este mesmo token entre a leitura e a
            // escrita: quem perdeu a corrida não emite par novo
            _log.LogWarning("Corrida no refresh do usuario {UsuarioId}: rotação já feita por outra requisição", token.UsuarioId);
            return Unauthorized(new ProblemDetails { Title = "Refresh token inválido", Status = 401 });
        }

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

        // `Ativo` aqui é o que faz a desativação valer de imediato para o
        // front: sem isso, a tela seguia achando que a sessão era boa.
        return usuario is null || !usuario.Ativo
            ? Unauthorized()
            : Ok(new UsuarioLogadoResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel, usuario.ProfessorId, usuario.AlunoId));
    }

    /// <summary>
    /// Emite um par novo. Devolve `null` quando a rotação perdeu a corrida para
    /// outra requisição — nesse caso nada é emitido.
    /// </summary>
    private async Task<SessaoResponse?> AbrirSessaoAsync(Usuario usuario, CancellationToken ct, RefreshToken? tokenAnterior = null)
    {
        var (accessToken, expiraEm) = _tokens.GerarAccessToken(usuario);
        var (refreshToken, hash, expiraRefresh) = _tokens.GerarRefreshToken();

        if (tokenAnterior is not null)
        {
            // `WHERE RevogadoEm IS NULL` no próprio UPDATE: o banco decide quem
            // rotaciona. Ler o estado e escrever depois deixava duas requisições
            // simultâneas passarem, cada uma com um par válido — e o alarme de
            // reuso nunca disparava.
            var linhasAfetadas = await _contexto.RefreshTokens
                .Where(t => t.Id == tokenAnterior.Id && t.RevogadoEm == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.RevogadoEm, DateTime.UtcNow)
                          .SetProperty(x => x.SubstituidoPor, hash),
                    ct);

            if (linhasAfetadas == 0)
            {
                return null;
            }
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
