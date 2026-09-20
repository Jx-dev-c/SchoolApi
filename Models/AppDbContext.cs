using Microsoft.EntityFrameworkCore;

namespace SchoolApi.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Professor> Professores => Set<Professor>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<Presenca> Presencas => Set<Presenca>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// A modelagem fica aqui, e não em atributos espalhados pelas entidades:
    /// num lugar só dá para ler o desenho do banco inteiro — índices, tamanhos
    /// e o que acontece ao apagar cada coisa.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aluno>(aluno =>
        {
            aluno.Property(a => a.Nome).HasMaxLength(120).IsRequired();
            aluno.Property(a => a.Email).HasMaxLength(160).IsRequired();
            // e-mail duplicado é erro de cadastro que só aparece meses depois,
            // quando dois registros disputam a mesma pessoa
            aluno.HasIndex(a => a.Email).IsUnique();
        });

        modelBuilder.Entity<Professor>(professor =>
        {
            professor.Property(p => p.Nome).HasMaxLength(120).IsRequired();
            professor.Property(p => p.Email).HasMaxLength(160).IsRequired();
            professor.Property(p => p.Disciplina).HasMaxLength(80).IsRequired();
            professor.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<Turma>(turma =>
        {
            turma.Property(t => t.Nome).HasMaxLength(80).IsRequired();
            turma.HasIndex(t => new { t.AnoLetivo, t.Nome }).IsUnique();

            // Restrict e não Cascade: apagar um professor não pode levar junto
            // as turmas dele (e o histórico de notas atrás delas)
            turma.HasOne(t => t.Professor)
                 .WithMany(p => p.Turmas)
                 .HasForeignKey(t => t.ProfessorId)
                 .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Matricula>(matricula =>
        {
            // o mesmo aluno não se matricula duas vezes na mesma turma —
            // é a regra que o banco garante, mesmo se o código falhar
            matricula.HasIndex(m => new { m.AlunoId, m.TurmaId }).IsUnique();

            matricula.HasOne(m => m.Aluno)
                     .WithMany(a => a.Matriculas)
                     .HasForeignKey(m => m.AlunoId)
                     .OnDelete(DeleteBehavior.Restrict);

            matricula.HasOne(m => m.Turma)
                     .WithMany(t => t.Matriculas)
                     .HasForeignKey(m => m.TurmaId)
                     .OnDelete(DeleteBehavior.Restrict);

            // guarda o nome do enum no banco, não o número: consulta direta no
            // psql fica legível e inserir um valor no meio do enum não remapeia
            // o que já está gravado
            matricula.Property(m => m.Situacao).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Nota>(nota =>
        {
            // um bimestre por matrícula; relançar nota é atualizar, não inserir
            nota.HasIndex(n => new { n.MatriculaId, n.Bimestre }).IsUnique();
            nota.Property(n => n.Valor).HasPrecision(4, 2);

            // aqui Cascade faz sentido: nota não existe fora da matrícula
            nota.HasOne(n => n.Matricula)
                .WithMany(m => m.Notas)
                .HasForeignKey(n => n.MatriculaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Presenca>(presenca =>
        {
            presenca.HasIndex(p => new { p.MatriculaId, p.Data }).IsUnique();

            presenca.HasOne(p => p.Matricula)
                    .WithMany(m => m.Presencas)
                    .HasForeignKey(p => p.MatriculaId)
                    .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.Nome).HasMaxLength(120).IsRequired();
            usuario.Property(u => u.Email).HasMaxLength(160).IsRequired();
            usuario.Property(u => u.SenhaHash).HasMaxLength(256).IsRequired();
            usuario.Property(u => u.Papel).HasConversion<string>().HasMaxLength(20);
            usuario.HasIndex(u => u.Email).IsUnique();

            // uma conta por pessoa: sem isso, duas credenciais diferentes
            // enxergavam o mesmo aluno. O filtro permite varias contas sem
            // vinculo (admins), que ficam com a coluna nula.
            usuario.HasIndex(u => u.AlunoId).IsUnique().HasFilter("\"AlunoId\" IS NOT NULL");
            usuario.HasIndex(u => u.ProfessorId).IsUnique().HasFilter("\"ProfessorId\" IS NOT NULL");

            // a conta aponta para a pessoa do dominio; apagar a pessoa nao pode
            // deixar a conta orfa apontando pro nada
            usuario.HasOne(u => u.Professor).WithMany()
                   .HasForeignKey(u => u.ProfessorId).OnDelete(DeleteBehavior.Restrict);
            usuario.HasOne(u => u.Aluno).WithMany()
                   .HasForeignKey(u => u.AlunoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            token.Property(t => t.SubstituidoPor).HasMaxLength(128);
            // a busca no refresh e sempre por hash: sem indice, cada renovacao
            // varreria a tabela inteira
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.Ignore(t => t.Ativo);

            token.HasOne(t => t.Usuario).WithMany(u => u.RefreshTokens)
                 .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
