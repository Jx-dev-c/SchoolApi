using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolApi.Models;

namespace SchoolApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/usuarios
        [HttpGet]
        public async Task<ActionResult<List<Usuario>>> GetAll()
        {
            var usuarios = await _context.Usuarios.ToListAsync();
            return Ok(usuarios);
        }

        // GET: api/usuarios/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetById(int id)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound();

            return Ok(usuario);
        }

        // POST: api/usuarios
        [HttpPost]
        public async Task<ActionResult<Usuario>> Create(Usuario usuario)
        {
            usuario.DataCriacao = DateTime.UtcNow;
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
        }

        // PUT: api/usuarios/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id, Usuario usuario)
        {
            var existingUser = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

            if (existingUser == null)
                return NotFound();

            existingUser.Nome = usuario.Nome;
            existingUser.Email = usuario.Email;
            existingUser.Cargo = usuario.Cargo;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/usuarios/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}