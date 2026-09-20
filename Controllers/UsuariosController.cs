using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

/// <summary>
/// Gestão de contas de acesso — só para administrador. Antes este controller
/// era um CRUD aberto que qualquer um na internet podia chamar.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(PapelUsuario.Admin))]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _contexto;
    private readonly IPasswordHasher<Usuario> _hasher;

    public UsuariosController(AppDbContext contexto, IPasswordHasher<Usuario> hasher)
    {
        _contexto = contexto;
        _hasher = hasher;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponse>>> Listar(CancellationToken ct)
    {
        var usuarios = await _contexto.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioResponse(u.Id, u.Nome, u.Email, u.Papel, u.Ativo, u.DataCriacao))
            .ToListAsync(ct);

        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioResponse>> BuscarPorId(int id, CancellationToken ct)
    {
        var usuario = await _contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UsuarioResponse(u.Id, u.Nome, u.Email, u.Papel, u.Ativo, u.DataCriacao))
            .FirstOrDefaultAsync(ct);

        return usuario is null ? NotFound() : Ok(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioResponse>> Criar(CriarUsuarioRequest requisicao, CancellationToken ct)
    {
        if (await _contexto.Usuarios.AnyAsync(u => u.Email == requisicao.Email, ct))
        {
            ModelState.AddModelError(nameof(requisicao.Email), "Já existe conta com esse e-mail");
            return ValidationProblem(ModelState);
        }

        // o vínculo precisa combinar com o papel, senão um "professor" sem
        // ProfessorId passaria em [Authorize(Roles=...)] e falharia depois,
        // na hora de checar de quem é a turma
        var erroDeVinculo = await ValidarVinculoAsync(requisicao, ct);
        if (erroDeVinculo is not null)
        {
            ModelState.AddModelError(erroDeVinculo.Value.Campo, erroDeVinculo.Value.Mensagem);
            return ValidationProblem(ModelState);
        }

        var usuario = new Usuario
        {
            Nome = requisicao.Nome,
            Email = requisicao.Email,
            SenhaHash = string.Empty,
            Papel = requisicao.Papel,
            ProfessorId = requisicao.ProfessorId,
            AlunoId = requisicao.AlunoId,
            DataCriacao = DateTime.UtcNow,
        };
        usuario.SenhaHash = _hasher.HashPassword(usuario, requisicao.Senha);

        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync(ct);

        var resposta = new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel, usuario.Ativo, usuario.DataCriacao);
        return CreatedAtAction(nameof(BuscarPorId), new { id = usuario.Id }, resposta);
    }

    /// <summary>
    /// Desativa em vez de apagar: conta apagada levaria junto o histórico de
    /// quem lançou cada nota.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        var usuario = await _contexto.Usuarios.FindAsync([id], ct);
        if (usuario is null)
        {
            return NotFound();
        }

        // ExecuteUpdate grava na hora; a mudança na entidade só sai no
        // SaveChanges. Sem transação, falhar no meio deixaria a conta ativa com
        // as sessões revogadas — estado inconsistente.
        await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);

        usuario.Ativo = false;
        await _contexto.RefreshTokens
            .Where(t => t.UsuarioId == id && t.RevogadoEm == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevogadoEm, DateTime.UtcNow), ct);

        await _contexto.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// O vínculo precisa combinar com o papel, nos dois sentidos. Antes só o
    /// vínculo esperado era checado, então um admin podia ser criado com
    /// professorId e alunoId preenchidos — e o token dele saía com os dois
    /// claims, valendo como professor e como aluno na checagem de posse.
    /// </summary>
    private async Task<(string Campo, string Mensagem)?> ValidarVinculoAsync(CriarUsuarioRequest requisicao, CancellationToken ct)
    {
        switch (requisicao.Papel)
        {
            case PapelUsuario.Admin when requisicao.ProfessorId is not null || requisicao.AlunoId is not null:
                return (nameof(requisicao.Papel), "Conta de admin não se vincula a professor nem a aluno");

            case PapelUsuario.Professor when requisicao.AlunoId is not null:
                return (nameof(requisicao.AlunoId), "Conta de professor não se vincula a aluno");

            case PapelUsuario.Aluno when requisicao.ProfessorId is not null:
                return (nameof(requisicao.ProfessorId), "Conta de aluno não se vincula a professor");

            case PapelUsuario.Professor when requisicao.ProfessorId is null:
                return (nameof(requisicao.ProfessorId), "Conta de professor precisa estar vinculada a um professor");

            case PapelUsuario.Professor when !await _contexto.Professores.AnyAsync(p => p.Id == requisicao.ProfessorId, ct):
                return (nameof(requisicao.ProfessorId), "Professor não encontrado");

            case PapelUsuario.Professor when await _contexto.Usuarios.AnyAsync(u => u.ProfessorId == requisicao.ProfessorId, ct):
                return (nameof(requisicao.ProfessorId), "Esse professor já tem conta de acesso");

            case PapelUsuario.Aluno when requisicao.AlunoId is null:
                return (nameof(requisicao.AlunoId), "Conta de aluno precisa estar vinculada a um aluno");

            case PapelUsuario.Aluno when !await _contexto.Alunos.AnyAsync(a => a.Id == requisicao.AlunoId, ct):
                return (nameof(requisicao.AlunoId), "Aluno não encontrado");

            case PapelUsuario.Aluno when await _contexto.Usuarios.AnyAsync(u => u.AlunoId == requisicao.AlunoId, ct):
                return (nameof(requisicao.AlunoId), "Esse aluno já tem conta de acesso");

            default:
                return null;
        }
    }
}
